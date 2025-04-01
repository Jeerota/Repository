
namespace Repository.Models
{
    public class ColumnOptions(string? label = null,
        bool frozen = false)
    {
        public string? Label { get; set; } = label;

        public bool Frozen { get; set; } = frozen;
    }
}
