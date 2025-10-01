using CurationBack.Services.FiltersAttributes;

namespace CurationBack.Models;

[TypeScriptModel]
public class UserLogin
{
	public required string Email { get; set; }
	public required string Pw { get; set; }
}
