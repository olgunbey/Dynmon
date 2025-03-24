using static Dynmon.Enums;

namespace Dynmon
{
    public class Filter
    {
        public required string Field { get; set; }
        public required object Value { get; set; }
        public MatchOperators MatchOperator { get; set; }
        public FilterOperators FilterOperator { get; set; }
    }
}
