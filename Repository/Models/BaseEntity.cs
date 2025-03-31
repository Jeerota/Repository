
namespace Repository.Models
{
    public abstract class BaseEntity
    {
        public DateTime ModifiedOn { get; set; }
        public Guid ModifiedBy { get; set; }
        public bool IsArchived { get; set; }
    }
}
