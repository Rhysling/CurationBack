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
				FullName = "Bob User",
				Email = "rpkummer@gmail.com",
				Pw = "vestal"
			};

			var uOps = new Ops.UserDbOps(aps, _udb);
			uOps.RegisterUser(ur);
		}
	}
}
