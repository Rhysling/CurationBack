namespace CurationBack.Models
{
	public interface IDbItem
	{
		int Id { get; set; }
		bool IsDeleted { get; set; }
	}
}
