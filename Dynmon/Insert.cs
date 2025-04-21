using MongoDB.Bson;
using MongoDB.Driver;

namespace Dynmon
{
    public class Insert
    {
        public required IMongoCollection<BsonDocument> Data { get; set; }
        public List<Filter>? Filter { get; set; }
        public required string JsonData { get; set; }
        public string? InsertProperty { get; set; }
    }
}
