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
                UpdatedAt = DateTime.UtcNow
            };

            await _data.InsertAsync(game);

            return game;
        }


        public async Task<Game> JoinGameAsync(string gameId, string player, string? password)
        {
            // Busca el juego
            var game = await _data.GetByIdAsync(gameId);
            if (game == null)
                throw new Exception("Game not found");

            // Si tiene password pero no enviaron password
            if (game.Password && string.IsNullOrWhiteSpace(password))
                throw new Exception("Unauthorized");

            // Si ya está el jugador en la partida
            if (game.Players.Contains(player))
                throw new Exception("Player already in game");

            // Agrega el jugador
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

            if (game.Password && game.Owner != player)
                throw new UnauthorizedAccessException("Invalid credentials");

            if (game.Owner != player)
                throw new UnauthorizedAccessException("Invalid credentials");

            if (game.Status == "rounds")
                throw new Exception("Game already started.");

            if (game.Players.Count < 5)
                throw new InvalidOperationException("Need 5 players to start.");

            game.Status = "rounds";

            game.UpdatedAt = DateTime.UtcNow;

            await _data.UpdateAsync(game.Id, game);

        }


    }
}
