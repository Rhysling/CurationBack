using CurationBack.Models;
using CurationBack.Services;
using CurationBack.Runner.Runs;

var aps = new AppSettings { 
	Polson = new AS_Polson { IsProductionString = "false", AdminPw = "placeholder" },
	Jwt = new AS_Jwt { Key = "placeholder_key_please_change_please_change", Issuer = "polson.com" }
};

var uRuns = new UserRuns(aps);
uRuns.RegisterUser();

//var picRuns = new PictureRuns(aps);
//picRuns.LoadPicturesFromDir();


Console.WriteLine("Done.");
Console.ReadKey();
