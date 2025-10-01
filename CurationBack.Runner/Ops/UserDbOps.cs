using CurationBack.Controllers;
using CurationBack.Models;
using CurationBack.Services;

namespace CurationBack.Runner.Ops;

public class UserDbOps(AppSettings aps, UsersDb udb)
{
	public string RegisterUser(UserRegister ur)
	{
		var uc = new UsersController(aps, udb);
		var result = uc.Register(ur);

		return result?.ToString() ?? "No result";
	}

}
