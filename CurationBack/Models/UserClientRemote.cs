using CurationBack.Services.FiltersAttributes;

namespace CurationBack.Models
{
	[TypeScriptModel]
	public class UserClientRemote
	{
		public int UserId { get; set; }
		public required string Email { get; set; }
		public required string FullName { get; set; }
		public string? Token { get; set; }
		public bool IsAdmin { get; set; } = false;
		public bool IsDisabled { get; set; } = false;
	}
}
