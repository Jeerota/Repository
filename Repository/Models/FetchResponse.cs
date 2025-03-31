namespace Repository.Models
{
    public class FetchResponse<TRecord>
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public IEnumerable<TRecord> Records { get; set; } = [];
    }
}
