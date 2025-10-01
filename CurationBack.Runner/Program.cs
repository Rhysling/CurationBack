using CurationBack.Models;
using CurationBack.Services;

var aps = new AppSettings { 
	Polson = new AS_Polson { IsProductionString = "false", AdminPw = "placeholder" },
	Jwt = new AS_Jwt { Key = "placeholder_key_please_change_please_change", Issuer = "polson.com" }
};

var udb = new UsersDb(aps);


var ur = new UserRegister {
	FullName = "Bob Test User",
	Email = "bob@tester2.com",
	Pw = "Password123!"
};

var uOps = new CurationBack.Runner.Ops.UserDbOps(aps, udb);
string res = uOps.RegisterUser(ur);

Console.WriteLine(res);
Console.WriteLine("Done.");
Console.ReadKey();
