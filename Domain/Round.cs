using MongoDB.Bson;
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
    }
}
