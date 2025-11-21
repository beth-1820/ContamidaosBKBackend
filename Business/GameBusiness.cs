using ContaminaDOS.Data;
using ContaminaDOS.Domain;

namespace ContaminaDOS.Business
{
    public class GameBusiness
    {
        private readonly GameData _data;

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
                Status = "lobby",
                CurrentRound = "0000000000000000000000000",
                Rounds = new List<Round>()
            };

            await _data.InsertAsync(game);

            return game;
        }

        public async Task<Game> JoinGameAsync(string gameId, string player, string? password)
        {
            var game = await _data.GetByIdAsync(gameId);
            if (game == null)
                throw new KeyNotFoundException("The specified resource was not found");

            // Si la partida tiene password, exigimos que se envíe algo en el header.
            if (game.Password && string.IsNullOrWhiteSpace(password))
                throw new UnauthorizedAccessException("Invalid credentials");

            if (game.Players.Contains(player))
                throw new InvalidOperationException("Asset already exists");

            if (game.Status != "lobby")
                throw new ApplicationException("This action is not allowed at this time");

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

            // Cambiamos estado y creamos primera ronda
            game.Status = "rounds";

            var firstRound = new Round
            {
                Leader = game.Owner,
                Status = "waiting-on-leader",
                Result = "none",
                Phase = "vote1",
                Group = new List<string>(),
                Votes = new List<bool>(),
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
            string gameId,
            string roundId,
            string leader,
            List<string> group)
        {
            var (game, round) = await GetRoundAsync(gameId, roundId);

            // Valida que el líder sea un jugador del juego
            if (!game.Players.Contains(leader))
                throw new UnauthorizedAccessException("Invalid credentials");

            // Validaciones de cantidad de grupo
            if (group.Count < 2 || group.Count > 6)
                throw new ArgumentException("Group size must be between 2 and 6");

            // Todos deben ser jugadores válidos del juego
            if (group.Any(p => !game.Players.Contains(p)))
                throw new ArgumentException("Invalid player in group");

            // El round debe estar esperando líder
            if (round.Status != "waiting-on-leader")
                throw new ApplicationException("This action is not allowed at this time");

            round.Leader = leader;
            round.Group = group;
            round.Status = "voting";
            round.Phase = "vote1";
            round.UpdatedAt = DateTime.UtcNow;

            game.UpdatedAt = DateTime.UtcNow;

            await _data.UpdateAsync(game.Id, game);

            return (game, round);
        }

        public async Task<(Game game, Round round)> SubmitVoteAsync(
            string gameId,
            string roundId,
            string player,
            bool vote)
        {
            var (game, round) = await GetRoundAsync(gameId, roundId);

            if (!game.Players.Contains(player))
                throw new UnauthorizedAccessException("Not part of the game");

            if (round.Status != "voting")
                throw new ApplicationException("This action is not allowed at this time");

            // Nota: No guardamos quién votó, solo cuántos votos y sus valores.
            round.Votes.Add(vote);
            round.UpdatedAt = DateTime.UtcNow;
            game.UpdatedAt = DateTime.UtcNow;

            await _data.UpdateAsync(game.Id, game);

            return (game, round);
        }

    
        public async Task<(Game game, Round round)> SubmitActionAsync(
            string gameId,
            string roundId,
            string player,
            bool action)
        {
            var (game, round) = await GetRoundAsync(gameId, roundId);

            if (!game.Players.Contains(player))
                throw new UnauthorizedAccessException("Not part of the game");

            if (!round.Group.Contains(player))
                throw new ArgumentException("Player is not part of the current group");

            // Si intenta sabotear (false) y NO es enemigo → error
            if (!game.Enemies.Contains(player) && action == false)
                throw new ArgumentException("Only enemies can sabotage");

            if (round.Status != "waiting-on-group")
                throw new ApplicationException("This action is not allowed at this time");

            round.Votes.Add(action);
            round.UpdatedAt = DateTime.UtcNow;

            // Si ya accionaron todos los del grupo, calculamos resultado
            if (round.Votes.Count == round.Group.Count)
            {
                if (round.Votes.Any(v => v == false))
                    round.Result = "enemies";
                else
                    round.Result = "citizens";

                round.Status = "ended";
            }

            game.UpdatedAt = DateTime.UtcNow;
            await _data.UpdateAsync(game.Id, game);

            return (game, round);
        }
    }
}
