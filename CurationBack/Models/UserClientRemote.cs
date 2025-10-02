using CurationBack.Services.FiltersAttributes;

namespace CurationBack.Models
{
	[TypeScriptModel]
	public class UserClientRemote : IDbItem
	{
		public int Id { get; set; }
		public required string Email { get; set; }
		public required string FullName { get; set; }
		public string? Token { get; set; }
		public bool IsAdmin { get; set; }
		public bool IsDisabled { get; set; }
		public bool IsDeleted { get; set; }
	}
}
