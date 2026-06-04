using CurationBack.Models;
using Microsoft.Data.Sqlite;

namespace CurationBack.Services;

public class BaseSqliteDb
{
	protected readonly string cs;
	protected readonly string tableName;
	protected readonly string dbPath;
	protected readonly string dbFullPath;

	public BaseSqliteDb(AppSettings aps, string tableName)
	{
		string dir = Directory.GetCurrentDirectory();
		this.tableName = tableName;

		if (aps.Polson.IsProduction)
			dbPath = Path.Combine(dir, @"Db\");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dbPath = dir[0..ix] + @"CurationBack\CurationBack\Db\";
		}

		dbFullPath = Path.Combine(dbPath, "curation.db");
		cs = $"Data Source={dbFullPath}";
	}

	public void SetDeleted(int id, bool isDeleted)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = $"UPDATE {tableName} SET IsDeleted = $d WHERE Id = $id";
		cmd.Parameters.AddWithValue("$d", isDeleted ? 1 : 0);
		cmd.Parameters.AddWithValue("$id", id);
		cmd.ExecuteNonQuery();
	}

	public void Destroy(int id)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = $"DELETE FROM {tableName} WHERE Id = $id";
		cmd.Parameters.AddWithValue("$id", id);
		cmd.ExecuteNonQuery();
	}

	protected SqliteConnection Open()
	{
		var conn = new SqliteConnection(cs);
		conn.Open();
		return conn;
	}

	protected static string CleanInput(string input)
	{
		return input.Replace("'", "").Replace(";", "");
	}

	// *** File Operations ***

	public string BackupFile()
	{
		string fn = $"curation_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";

		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = $"VACUUM INTO '{Path.Combine(dbPath, fn)}';";
		cmd.ExecuteNonQuery();
		conn.Close();
		return fn;
	}

	public bool RestoreFile(string fileName)
	{
		string fullPath = Path.Combine(dbPath, fileName);

		if (!IsWithinDbPath(fullPath)) return false;

		if (!File.Exists(fullPath)) return false;

		File.Copy(fullPath, dbFullPath, true);
		return true;
	}

	public void DeleteFile(string fileName)
	{
		if (!fileName.StartsWith("curation_", StringComparison.CurrentCultureIgnoreCase))
			return;

		string fullPath = Path.Combine(dbPath, fileName);

		if (!IsWithinDbPath(fullPath)) return;

		File.Delete(fullPath);
	}

	public FileObj? DownloadFile(string? fileName = null)
	{
		fileName ??= "curation.db";
		byte[] fileBytes;

		if (fileName == "curation.db")
		{
			string tempPath = Path.Combine(dbPath, "temp.db");
			File.Delete(tempPath);

			using var conn = Open();
			var cmd = conn.CreateCommand();
			cmd.CommandText = $"VACUUM INTO '{tempPath}';";
			cmd.ExecuteNonQuery();
			conn.Close();

			fileBytes = File.ReadAllBytes(tempPath);
		}
		else
		{
			string fullPath = Path.Combine(dbPath, fileName);

			if (!File.Exists(fullPath))
				return null;

			fileBytes = File.ReadAllBytes(fullPath);
		}
		return new FileObj
		{
			FileBytes = fileBytes,
			ContentType = "application/octet-stream",
			FileName = fileName
		};
	}

	private bool IsWithinDbPath(string fullPath) =>
		Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(dbPath), StringComparison.OrdinalIgnoreCase);

	public List<string> BackupFileList()
	{
		string[] files = Directory.GetFiles(dbPath);
		return [
			"curation.db",
			..files.Where(a => Path.GetFileName(a).StartsWith("curation_") && Path.GetExtension(a).Equals(".db", StringComparison.CurrentCultureIgnoreCase))
					.Select(a => Path.GetFileName(a))
					.OrderByDescending(a => a)
			];
	}
}
