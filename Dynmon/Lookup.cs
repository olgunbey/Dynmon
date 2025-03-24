using static Dynmon.Enums;

namespace Dynmon
{
    public class Lookup
    {
        public required string From { get; set; }
        public required string LocalField { get; set; }
        public required string ForeignKey { get; set; }
        public required string As { get; set; }
        public List<string>? Selects { get; set; }
        public List<Filter>? Filters { get; set; }
        public RelationTypes RelationType { get; set; }
        public bool PreserveNullAndEmptyArrays { get; set; } = false;
        public string RootPath { get; set; }
    }
}
