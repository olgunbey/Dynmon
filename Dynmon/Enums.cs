using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynmon
{
   public class Enums
    {
        public enum FilterOperators
        {
            Equals = 1,
            NotEquals,
            Contains,
            GreaterThan,
            LessThan,
            GreaterThanOrEqual,
            LessThanOrEqual,
            In,
            NotIn
        }

        public enum MatchOperators
        {
            None = 1,
            And,
            Or
        }

        public enum RelationTypes
        {
            One = 1,
            Many
        }
    }
}
