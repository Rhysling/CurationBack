using CurationBack.Models;
using CurationBack.Services;

namespace CurationBack.Runner.Runs
{
	public class UserRuns(AppSettings aps)
	{
		private readonly UsersDb _udb = new(aps);


		public void RegisterUser()
		{
			var ur = new UserRegister
			{
				FullName = "Bob Test User",
				Email = "bob@tester2.com",
				Pw = "Password123!"
			};

			var uOps = new Ops.UserDbOps(aps, _udb);
			string res = uOps.RegisterUser(ur);
			Console.WriteLine(res);
		}
	}
}
