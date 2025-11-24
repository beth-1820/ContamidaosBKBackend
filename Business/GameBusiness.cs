using ContaminaDOS.Data;
using ContaminaDOS.Domain;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContaminaDOS.Business
{
    public class GameBusiness
    {
        private readonly GameData _data;

        // Constantes para estados del juego
        private const string STATUS_LOBBY = "lobby";
        private const string STATUS_ROUNDS = "rounds";
        private const string STATUS_ENDED_PSYCHOPATHS_WIN = "ended-psychopaths-win";
        private const string STATUS_ENDED_CITIZENS_WIN = "ended-citizens-win";

        // Constantes para fases y estados de ronda
        private const string PHASE_VOTE1 = "vote1";
        private const string PHASE_ACTION = "action";
        private const string PHASE_VOTE1_ENDED = "vote1-ended";
        private const string PHASE_ACTION_ENDED = "action-ended";
        
        private const string ROUND_STATUS_WAITING_LEADER = "waiting-on-leader";
        private const string ROUND_STATUS_VOTING = "voting";
        private const string ROUND_STATUS_WAITING_GROUP = "waiting-on-group";
        private const string ROUND_STATUS_ENDED = "ended";

        // Constantes para resultados
        private const string RESULT_NONE = "none";
        private const string RESULT_ENEMIES = "enemies";
        private const string RESULT_CITIZENS = "citizens";

        // Tabla: cantidad de jugadores -> [D1, D2, D3, D4, D5]
        private readonly Dictionary<int, int[]> _decadeGroupSizes = new()
        {
            {5, new[] {2, 3, 2, 3, 3}},
            {6, new[] {2, 3, 4, 3, 4}},
            {7, new[] {2, 3, 3, 4, 4}},
            {8, new[] {3, 4, 4, 5, 5}},
            {9, new[] {3, 4, 4, 5, 5}},
            {10, new[] {3, 4, 5, 5, 5}}
        };

        // Tabla: cantidad de jugadores -> (ejemplares, psicópatas)
        private readonly Dictionary<int, (int ejemplares, int psicopatas)> _roleDistribution = new()
        {
            {5, (ejemplares: 3, psicopatas: 2)},
            {6, (ejemplares: 4, psicopatas: 2)},
            {7, (ejemplares: 4, psicopatas: 3)},
            {8, (ejemplares: 5, psicopatas: 3)},
            {9, (ejemplares: 5, psicopatas: 4)},
            {10, (ejemplares: 6, psicopatas: 4)}
        };

        public GameBusiness(GameData data)
        {
            _data = data;
        }

        public async Task<Game> CreateGameAsync(string name, string owner, string? password)
        {
            var exists = await _data.GetByNameAsync(name);
            if (exists != null)
                throw new Exception("Asset already exists");

            var game = new Game
            {
                Name = name,
                Owner = owner,
                Password = !string.IsNullOrWhiteSpace(password),
                Players = new List<string> { owner },
                Enemies = new List<string>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = STATUS_LOBBY,
                CurrentRound = "000000000000000000000000", // sentinel: 24 ceros
                Rounds = new List<Round>()
            };

            // Forzar Id para compatibilidad con MongoDB
            if (string.IsNullOrWhiteSpace(game.Id))
            {
                game.Id = ObjectId.GenerateNewId().ToString();
            }

            await _data.InsertAsync(game);
            return game;
        }

        public async Task<Game> JoinGameAsync(string gameId, string player, string? password)
        {
            var game = await _data.GetByIdAsync(gameId);
            if (game == null)
                throw new KeyNotFoundException("The specified resource was not found");

            ValidateGamePassword(game, password);
            
            if (game.Players.Contains(player))
                throw new InvalidOperationException("Asset already exists");

            if (game.Status != STATUS_LOBBY)
                throw new ApplicationException("This action is not allowed at this time");

            if (game.Players.Count >= 10)
                throw new InvalidOperationException("Game is full.");

            game.Players.Add(player);
            game.UpdatedAt = DateTime.UtcNow;
            
            await _data.UpdateAsync(game.Id, game);
            return game;
        }

        public async Task<List<Game>> SearchGamesAsync(string? name, string? status, int page, int limit)
        {
            return await _data.SearchGamesAsync(name, status, page, limit);
        }

        public async Task<Game?> GetGameByIdAsync(string id)
        {
            return await _data.GetGameByIdAsync(id);
        }

        public async Task StartGameAsync(string gameId, string player, string? password)
        {
            var game = await _data.GetByIdAsync(gameId);

            if (game == null)
                throw new KeyNotFoundException("Game not found.");

            // Si tiene password, exigimos que venga algo
            if (game.Password && string.IsNullOrWhiteSpace(password))
                throw new UnauthorizedAccessException("Unauthorized: Incorrect password.");

            // Solo el owner puede iniciar
            if (!string.Equals(game.Owner, player, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Forbidden: Player not part of the game.");

            // Ya empezó
            if (game.Status != "lobby")
                throw new Exception("Game already started.");

            // Mínimo 5 jugadores
            if (game.Players.Count < 5)
                throw new InvalidOperationException("Need 5 players to start.");

            // Máximo 10 jugadores
            if (game.Players.Count > 10)
                throw new InvalidOperationException("Game cannot exceed 10 players.");

            // -----------------------------
            //   ASIGNACIÓN DE ROLES
            // -----------------------------
            if (!_roleDistribution.ContainsKey(game.Players.Count))
                throw new Exception("Unsupported players count for role distribution.");

            var playersShuffled = game.Players.OrderBy(x => Guid.NewGuid()).ToList();
            int numPsychos = _roleDistribution[game.Players.Count].psicopatas;
            var psicopatas = playersShuffled.Take(numPsychos).ToList();

            // Guardar enemigos (psicópatas) - CORREGIDO: ahora guardamos todos los psicópatas
            game.Enemies = psicopatas;

            // -----------------------------
            //   CREAR PRIMERA RONDA (DÉCADA 1)
            // -----------------------------
            game.Status = "rounds";

            var firstRound = new Round
            {
                Leader = game.Owner,
                Status = "waiting-on-leader",
                Result = "none",
                Phase = "vote1",
                Group = new List<string>(),
                Votes = new List<bool>(),
                // aseguramos nuevos contenedores
                ActionVotes = new List<bool>(),
                FailedVotes = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            game.Rounds.Add(firstRound);
            game.CurrentRound = firstRound.Id;
            game.UpdatedAt = DateTime.UtcNow;

            await _data.UpdateAsync(game.Id, game);
        }

        public async Task<(Game game, List<Round> rounds)> GetRoundsAsync(string gameId)
        {
            var game = await _data.GetByIdAsync(gameId);
            if (game == null)
                throw new KeyNotFoundException("The specified resource was not found");

            return (game, game.Rounds);
        }

        public async Task<(Game game, Round round)> GetRoundAsync(string gameId, string roundId)
        {
            var game = await _data.GetByIdAsync(gameId);
            if (game == null)
                throw new KeyNotFoundException("Game Not Found");

            var round = game.Rounds.FirstOrDefault(r => r.Id == roundId);
            if (round == null)
                throw new KeyNotFoundException("Round not found");

            return (game, round);
        }

        public async Task<(Game game, Round round)> ProposeGroupAsync(
            string gameId, string roundId, string leader, List<string> group)
        {
            var (game, round) = await GetRoundAsync(gameId, roundId);
            
            if (leader != round.Leader)
                throw new UnauthorizedAccessException("Only the current leader can propose a group");

            ValidateGroupProposal(game, round, group);

            // Establecer propuesta (fase de votación)
            round.Leader = leader;
            round.Group = group;
            round.Status = ROUND_STATUS_VOTING;
            round.Phase = PHASE_VOTE1;
            round.Votes = new List<bool>(); // reset votos de propuesta
            round.UpdatedAt = DateTime.UtcNow;
            game.UpdatedAt = DateTime.UtcNow;

            await _data.UpdateAsync(game.Id, game);
            return (game, round);
        }

        public async Task<(Game game, Round round)> SubmitVoteAsync(
            string gameId, string roundId, string player, bool vote)
        {
            var (game, round) = await GetRoundAsync(gameId, roundId);
            
            ValidatePlayerInGame(game, player);
            ValidateVotingPhase(round);

            // Guardar voto de propuesta
            round.Votes.Add(vote);
            round.UpdatedAt = DateTime.UtcNow;

            // Si ya votaron todos, resolver la propuesta
            if (round.Votes.Count == game.Players.Count)
            {
                await ResolveProposalVote(game, round);
            }
            else
            {
                game.UpdatedAt = DateTime.UtcNow;
                await _data.UpdateAsync(game.Id, game);
            }

            return (game, round);
        }

        public async Task<(Game game, Round round)> SubmitActionAsync(
            string gameId, string roundId, string player, bool action)
        {
            var (game, round) = await GetRoundAsync(gameId, roundId);
            
            ValidatePlayerInGame(game, player);
            ValidatePlayerInGroup(round, player);
            ValidateActionSubmission(game, player, action);
            ValidateGroupActionPhase(round);

            // Registrar voto de acción
            round.ActionVotes ??= new List<bool>();
            round.ActionVotes.Add(action);
            round.UpdatedAt = DateTime.UtcNow;

            // Si todos los miembros del grupo enviaron su acción, evaluar
            if (round.ActionVotes.Count == round.Group.Count)
            {
                await EvaluateGroupActions(game, round);
            }
            else
            {
                game.UpdatedAt = DateTime.UtcNow;
                await _data.UpdateAsync(game.Id, game);
            }

            return (game, round);
        }

        // Métodos privados de validación
        private void ValidateGamePassword(Game game, string? password)
        {
            if (game.Password && string.IsNullOrWhiteSpace(password))
                throw new UnauthorizedAccessException("Invalid credentials");
        }

        private void ValidateGameOwnership(Game game, string player)
        {
            if (!string.Equals(game.Owner, player, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Forbidden: Player not part of the game.");
        }

        private void ValidateGameStartConditions(Game game)
        {
            if (game.Status != STATUS_LOBBY)
                throw new Exception("Game already started.");

            if (game.Players.Count < 5)
                throw new InvalidOperationException("Need 5 players to start.");

            if (game.Players.Count > 10)
                throw new InvalidOperationException("Game cannot exceed 10 players.");
        }

        private void ValidatePlayerInGame(Game game, string player)
        {
            if (!game.Players.Contains(player))
                throw new UnauthorizedAccessException("Not part of the game");
        }

        private void ValidateVotingPhase(Round round)
        {
            if (round.Status != ROUND_STATUS_VOTING)
                throw new ApplicationException("This action is not allowed at this time");
        }

        private void ValidatePlayerInGroup(Round round, string player)
        {
            if (!round.Group.Contains(player))
                throw new ArgumentException("Player is not part of the current group");
        }

        private void ValidateActionSubmission(Game game, string player, bool action)
        {
            if (!game.Enemies.Contains(player) && action == false)
                throw new ArgumentException("Only enemies can sabotage");
        }

        private void ValidateGroupActionPhase(Round round)
        {
            if (round.Status != ROUND_STATUS_WAITING_GROUP)
                throw new ApplicationException("This action is not allowed at this time");
        }

        private void ValidateGroupProposal(Game game, Round round, List<string> group)
        {
            int totalPlayers = game.Players.Count;
            int decadeIndex = game.Rounds.IndexOf(round);

            if (!_decadeGroupSizes.ContainsKey(totalPlayers))
                throw new ArgumentException("Invalid total players for this game.");

            if (decadeIndex < 0 || decadeIndex >= _decadeGroupSizes[totalPlayers].Length)
                throw new ArgumentException("Invalid decade index.");

            int requiredSize = _decadeGroupSizes[totalPlayers][decadeIndex];

            if (group.Count != requiredSize)
                throw new ArgumentException($"Group size must be exactly {requiredSize} players for decade {decadeIndex + 1}.");

            if (group.Any(p => !game.Players.Contains(p)))
                throw new ArgumentException("Invalid player in group");

            if (round.Status != ROUND_STATUS_WAITING_LEADER)
                throw new ApplicationException("This action is not allowed at this time");
        }

        // Métodos privados de lógica de negocio
        private void AssignRoles(Game game)
        {
            if (!_roleDistribution.ContainsKey(game.Players.Count))
                throw new Exception("Unsupported players count for role distribution.");

            var playersShuffled = game.Players.OrderBy(x => Guid.NewGuid()).ToList();
            int numPsychos = _roleDistribution[game.Players.Count].psicopatas;
            var psicopatas = playersShuffled.Take(numPsychos).ToList();

            game.Enemies = psicopatas;
        }

        private void InitializeFirstRound(Game game)
        {
            game.Status = STATUS_ROUNDS;
            
            var firstRound = new Round
            {
                Leader = game.Owner,
                Status = ROUND_STATUS_WAITING_LEADER,
                Result = RESULT_NONE,
                Phase = PHASE_VOTE1,
                Group = new List<string>(),
                Votes = new List<bool>(),
                ActionVotes = new List<bool>(),
                FailedVotes = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            game.Rounds.Add(firstRound);
            game.CurrentRound = firstRound.Id;
            game.UpdatedAt = DateTime.UtcNow;
        }

        private async Task ResolveProposalVote(Game game, Round round)
        {
            int approvals = round.Votes.Count(v => v);
            int rejections = round.Votes.Count(v => !v);

            // Mayoría simple (approvals > rejections) => aprueba
            if (approvals > rejections)
            {
                // Propuesta aceptada -> pasar a fase de acción del grupo
                round.Status = ROUND_STATUS_WAITING_GROUP;
                round.Phase = PHASE_ACTION;
                round.ActionVotes = new List<bool>();
                round.FailedVotes = 0;
                round.Votes = new List<bool>();
            }
            else
            {
                // Propuesta rechazada -> incrementar intentos fallidos
                round.FailedVotes = (round.FailedVotes < int.MaxValue) ? round.FailedVotes + 1 : round.FailedVotes;

                // Si falla 3 veces -> la década es perdida (punto para psicópatas)
                if (round.FailedVotes >= 3)
                {
                    round.Result = RESULT_ENEMIES;
                    round.Status = ROUND_STATUS_ENDED;
                    round.Phase = PHASE_VOTE1_ENDED;
                    
                    await _data.UpdateAsync(game.Id, game);
                    await NextRoundAsync(game.Id);
                    return;
                }
                else
                {
                    // Permitir al líder proponer nuevamente
                    round.Status = ROUND_STATUS_WAITING_LEADER;
                    round.Phase = PHASE_VOTE1;
                    round.Group = new List<string>();
                    round.Votes = new List<bool>();
                }
            }

            game.UpdatedAt = DateTime.UtcNow;
            round.UpdatedAt = DateTime.UtcNow;
            await _data.UpdateAsync(game.Id, game);
        }

        private async Task EvaluateGroupActions(Game game, Round round)
        {
            round.Result = round.ActionVotes.Any(v => !v) ? RESULT_ENEMIES : RESULT_CITIZENS;
            round.Status = ROUND_STATUS_ENDED;
            round.Phase = PHASE_ACTION_ENDED;
            
            game.UpdatedAt = DateTime.UtcNow;
            await _data.UpdateAsync(game.Id, game);

            // Avanzar a la siguiente ronda o terminar el juego
            await NextRoundAsync(game.Id);
        }

        public async Task<(Game game, Round newRound)> NextRoundAsync(string gameId)
        {
            var game = await _data.GetByIdAsync(gameId);
            if (game == null)
                throw new KeyNotFoundException("Game not found");

            var lastRound = game.Rounds.LastOrDefault();
            if (lastRound == null)
                throw new Exception("No rounds exist");

            if (lastRound.Status != ROUND_STATUS_ENDED)
                throw new ApplicationException("The current round has not finished yet");

            int nextDecadeIndex = game.Rounds.Count;

            // Si ya se jugaron 5 décadas → finalizar juego
            if (nextDecadeIndex >= 5)
            {
                await EndGameAsync(game);
                return (game, lastRound);
            }

            var newRound = CreateNextRound(game, lastRound);
            game.Rounds.Add(newRound);
            game.CurrentRound = newRound.Id;
            game.UpdatedAt = DateTime.UtcNow;

            await _data.UpdateAsync(game.Id, game);
            return (game, newRound);
        }

        private Round CreateNextRound(Game game, Round lastRound)
        {
            // Rotación del líder
            int leaderIndex = game.Players.IndexOf(lastRound.Leader);
            if (leaderIndex < 0) leaderIndex = 0; // fallback
            string nextLeader = game.Players[(leaderIndex + 1) % game.Players.Count];

            return new Round
            {
                Leader = nextLeader,
                Status = ROUND_STATUS_WAITING_LEADER,
                Phase = PHASE_VOTE1,
                Result = RESULT_NONE,
                Group = new List<string>(),
                Votes = new List<bool>(),
                ActionVotes = new List<bool>(),
                FailedVotes = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private async Task EndGameAsync(Game game)
        {
            int sabotageCount = game.Rounds.Count(r => r.Result == RESULT_ENEMIES);
            int cleanCount = game.Rounds.Count(r => r.Result == RESULT_CITIZENS);
            
            game.Status = sabotageCount >= 3 ? STATUS_ENDED_PSYCHOPATHS_WIN : STATUS_ENDED_CITIZENS_WIN;
            game.UpdatedAt = DateTime.UtcNow;
            
            await _data.UpdateAsync(game.Id, game);
        }

        private void PopulateEnemiesForPlayer(Game game, string player)
        {
            if (game.PsychopathsByPlayer == null || game.PsychopathsByPlayer.Count == 0)
            {
                game.Enemies = new List<string>();
                return;
            }

            if (game.PsychopathsByPlayer.ContainsKey(player))
            {
                game.Enemies = game.PsychopathsByPlayer[player];
            }
            else
            {
                game.Enemies = new List<string>();
            }
        }

    }
}