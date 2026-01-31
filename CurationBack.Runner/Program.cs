using CurationBack.Models;
using CurationBack.Services;
using CurationBack.Runner.Runs;

var aps = new AppSettings { 
	Polson = new AS_Polson { IsProductionString = "false" },
	Jwt = new AS_Jwt { Key = "placeholder_key_please_change_please_change", Issuer = "polson.com" }
};

//var uRuns = new UserRuns(aps);
//uRuns.RegisterUser();

//var picRuns = new PictureRuns(aps);
//picRuns.MakeTimestamps();

var dbRuns = new DbRuns(aps);
dbRuns.BackupPicDb();
dbRuns.GetBackupListPicDb();

Console.WriteLine("Done.");
Console.ReadKey();
