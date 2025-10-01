using CurationBack.Services.FiltersAttributes;

namespace CurationBack.Models;

[TypeScriptModel]
public class UserRegister : UserLogin
{
	public required string FullName { get; set; }
}
