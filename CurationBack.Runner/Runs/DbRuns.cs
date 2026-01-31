using CurationBack.Models;
using CurationBack.Services;

namespace CurationBack.Runner.Runs;

public class DbRuns(AppSettings aps)
{
	private readonly PicturesDb pdb = new(aps);

	public void BackupPicDb()
	{
		string fn = pdb.BackupFile();
		Console.WriteLine(fn);
	}

	public void GetBackupListPicDb()
	{
		var list = pdb.BackupFileList();
		foreach(var fn in list)
			Console.WriteLine(fn);
	}
}