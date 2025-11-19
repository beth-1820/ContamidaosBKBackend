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
    }
}
