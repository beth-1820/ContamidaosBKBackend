using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace ContaminaDOS.Domain
{
    public class Game
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Owner { get; set; } = string.Empty;

        public string Status { get; set; } = "lobby";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool Password { get; set; } = false;

        public List<string> Players { get; set; } = new();

        public List<string> Enemies { get; set; } = new();

        public string CurrentRound { get; set; } = "0000000000000000000000000";

        public List<Round> Rounds { get; set; } = new();
    }
}
