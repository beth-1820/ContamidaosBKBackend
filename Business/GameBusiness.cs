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
    }
}
