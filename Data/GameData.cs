using MongoDB.Driver;
using ContaminaDOS.Domain;

namespace ContaminaDOS.Data
{
    public class GameData
    {
        private readonly IMongoCollection<Game> _games;

        public GameData(IMongoDatabase db)
        {
            _games = db.GetCollection<Game>("games");
        }


        public async Task InsertAsync(Game game)
        {
            await _games.InsertOneAsync(game);
        }

        public async Task<Game?> GetByNameAsync(string name)
        {
            return await _games.Find(g => g.Name == name).FirstOrDefaultAsync();
        }

        public async Task<Game?> GetByIdAsync(string id)
        {
            return await _games.Find(g => g.Id == id).FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(string id, Game game)
        {
            await _games.ReplaceOneAsync(g => g.Id == id, game);
        }


        public async Task<List<Game>> SearchGamesAsync(string? name, string? status, int page, int limit)
        {
            var filter = Builders<Game>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(name))
                filter &= Builders<Game>.Filter.Regex(
                    g => g.Name,
                    new MongoDB.Bson.BsonRegularExpression(name, "i"));

            if (!string.IsNullOrWhiteSpace(status))
                filter &= Builders<Game>.Filter.Eq(g => g.Status, status);

            return await _games.Find(filter)
                               .Skip(page)
                               .Limit(limit)
                               .ToListAsync();
        }

        public async Task<Game?> GetGameByIdAsync(string id)
        {
            var filter = Builders<Game>.Filter.Eq("_id", id);
            return await _games.Find(filter).FirstOrDefaultAsync();
        }

        public async Task AddPlayerAsync(string gameId, string player)
        {
            var update = Builders<Game>.Update
                .AddToSet(g => g.Players, player)
                .Set(g => g.UpdatedAt, DateTime.UtcNow);

            await _games.UpdateOneAsync(g => g.Id == gameId, update);
        }

        public async Task<bool> PlayerExistsAsync(string gameId, string player)
        {
            var game = await GetByIdAsync(gameId);
            return game != null && game.Players.Contains(player);
        }
    }
}
