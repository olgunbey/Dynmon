namespace Dynmon
{
    public class MongoQueryRequest
    {
        public IEnumerable<Filter> Filters { get; set; }
        public List<Lookup> Lookups { get; set; }
        public List<string> SelectedFields { get; set; }
    }
}
