using CurationBack.Models;
using Microsoft.Data.Sqlite;

namespace CurationBack.Services;

public class BaseSqliteDb
{
	protected readonly string cs;
	protected readonly string tableName;
	protected readonly string dbPath;
	protected readonly string dbFullPath;
	protected readonly string dbFileName;    // e.g. "curation.db"
	protected readonly string backupPrefix;  // e.g. "curation_"
	private readonly string tempFileName;

	// dbName is the file's base name: "curation" (Pictures, Users) or "traffic" (Events, Salts).
	public BaseSqliteDb(AppSettings aps, string tableName, string dbName = "curation")
	{
		dbFileName = $"{dbName}.db";
		backupPrefix = $"{dbName}_";
		tempFileName = dbName == "curation" ? "temp.db" : $"temp-{dbName}.db";

		string dir = Directory.GetCurrentDirectory();
		this.tableName = tableName;

		if (aps.Polson.IsProduction)
			dbPath = Path.Combine(dir, @"Db\");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dbPath = dir[0..ix] + @"CurationBack\CurationBack\Db\";
		}

		dbFullPath = Path.Combine(dbPath, dbFileName);
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

	// True for this DB's live file ("curation.db") or one of its backups ("curation_*").
	public bool OwnsFile(string fileName) =>
		fileName.Equals(dbFileName, StringComparison.OrdinalIgnoreCase) ||
		fileName.StartsWith(backupPrefix, StringComparison.OrdinalIgnoreCase);

	public bool IsBackupName(string fileName) =>
		fileName.StartsWith(backupPrefix, StringComparison.OrdinalIgnoreCase);

	public string BackupFile()
	{
		string fn = $"{backupPrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";

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

		// Online backup API instead of a file copy: safe with open pooled connections and with
		// WAL mode (a raw copy would leave a stale -wal file to be replayed over the restored data).
		using (var src = new SqliteConnection($"Data Source={fullPath};Mode=ReadOnly;Pooling=False"))
		{
			src.Open();
			using var dst = Open();
			src.BackupDatabase(dst);
		}
		return true;
	}

	public void DeleteFile(string fileName)
	{
		if (!IsBackupName(fileName))
			return;

		string fullPath = Path.Combine(dbPath, fileName);

		if (!IsWithinDbPath(fullPath)) return;

		File.Delete(fullPath);
	}

	public FileObj? DownloadFile(string? fileName = null)
	{
		fileName ??= dbFileName;
		byte[] fileBytes;

		if (fileName.Equals(dbFileName, StringComparison.OrdinalIgnoreCase))
		{
			string tempPath = Path.Combine(dbPath, tempFileName);
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

			if (!IsWithinDbPath(fullPath) || !File.Exists(fullPath))
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
			dbFileName,
			..files.Where(a => Path.GetFileName(a).StartsWith(backupPrefix) && Path.GetExtension(a).Equals(".db", StringComparison.CurrentCultureIgnoreCase))
					.Select(a => Path.GetFileName(a))
					.OrderByDescending(a => a)
			];
	}
}
