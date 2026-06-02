using Microsoft.Data.Sqlite;
using CurationBack.Models;

namespace CurationBack.Services;

public class BaseSqliteDb
{
	protected readonly string _cs;

	public BaseSqliteDb(AppSettings aps)
	{
		string dir = Directory.GetCurrentDirectory();
		string dbPath;

		if (aps.Polson.IsProduction)
			dbPath = Path.Combine(dir, "Db", "curation.db");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dbPath = dir[0..ix] + @"CurationBack\CurationBack\Db\curation.db";
		}

		_cs = $"Data Source={dbPath}";
	}

	protected SqliteConnection Open()
	{
		var conn = new SqliteConnection(_cs);
		conn.Open();
		return conn;
	}
}
