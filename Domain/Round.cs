using MongoDB.Bson.Serialization.Attributes;

namespace ContaminaDOS.Domain
{
    public class Round
    {
        [BsonId]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Leader { get; set; } = string.Empty;

        public string Status { get; set; } = "waiting-on-leader";

        public string Result { get; set; } = "none";

        public string Phase { get; set; } = "vote1";

        public List<string> Group { get; set; } = new();

        public List<bool> Votes { get; set; } = new();

        public int FailedVotes { get; set; } = 0;
        public List<bool> ActionVotes { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
