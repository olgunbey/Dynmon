namespace Dynmon
{
    public class Exceptions
    {
        public class FilterOperatorException(string msg) : Exception(msg);
        public class LookupFromNotFoundException(string msg) : Exception(msg);
    }
}
