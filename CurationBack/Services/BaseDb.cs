using CurationBack.Models;
using Newtonsoft.Json;

namespace CurationBack.Services;

public class BaseDb<TItem> where TItem : IDbItem
{
	protected List<TItem> db;
	protected readonly string dbName;
	protected readonly string dbPath;
	protected readonly string dbFullPath;

	public BaseDb(AppSettings aps, string dbName)
	{
		string dir = Directory.GetCurrentDirectory();
		//D:\UserData\Documents\AppDev\NextSemiBack\NextSemiBack
		this.dbName = dbName;

		if (aps.Polson.IsProduction)
		{
			dbPath = Path.Combine(dir, @$"Db\");
		}
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dbPath = dir[0..ix] + @$"CurationBack\CurationBack\Db\";
			// @$"CurationFront\public\docs\{dbName}.json";
			//D:\UserData\Documents\AppDev\CurationFront\db
		}
		dbFullPath = Path.Combine(dbPath, @$"{dbName}.json");

		if (File.Exists(dbFullPath))
		{
			string json = File.ReadAllText(dbFullPath);
			db = JsonConvert.DeserializeObject<List<TItem>>(json) ?? [];
		}
		else
			db = [];
	}

	protected string FilePath => dbFullPath;

	public virtual List<TItem> GetAll(bool includeDeleted = false) => [.. includeDeleted ? db : db.Where(a => !a.IsDeleted)];

	public virtual TItem? GetById(int id, bool includeDeleted = false) => db.FirstOrDefault(a => a.Id == id && (includeDeleted || !a.IsDeleted));

	public virtual TItem SaveItem(TItem item)
	{
		int ix;

		if (item.Id == 0)
			item.Id = db.Count != 0 ? db.Max(a => a.Id) + 1 : 1;

		ix = db.FindIndex(a => a.Id == item.Id);
		if (ix >= 0)
			db.RemoveAt(ix);

		db.Add(item);
		SaveFile();
		return item;
	}

	public virtual List<TItem> SaveBatch(List<TItem> items)
	{
		int ix;
		List<TItem> result = [];

		foreach (var item in items)
		{
			if (item.Id == 0)
				item.Id = db.Count != 0 ? db.Max(a => a.Id) + 1 : 1;

			ix = db.FindIndex(a => a.Id == item.Id);
			if (ix >= 0)
				db.RemoveAt(ix);

			db.Add(item);
			result.Add(item);
		}

		SaveFile();
		return result;
	}

	public void SetDeleted(int id, bool isDeleted)
	{
		db.Where(a => a.Id == id).ToList().ForEach(a => a.IsDeleted = isDeleted);
		SaveFile();
	}

	public void Destroy(int id)
	{
		db = [.. db.Where(a => a.Id != id)];
		SaveFile();
	}

	// *** File Operations ***

	protected void SaveFile()
	{
		File.WriteAllText(FilePath, JsonConvert.SerializeObject(db));
	}

	public string BackupFile()
	{
		string fn = @$"{dbName}_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.json";
		File.WriteAllText(Path.Combine(dbPath, fn), JsonConvert.SerializeObject(db));
		return fn;
	}

	public void RestoreFile(string fileName)
	{
		string fullPath = Path.Combine(dbPath, fileName);

		if (File.Exists(fullPath))
		{
			File.Copy(fullPath, dbFullPath, true);
		}
	}

	public void DeleteFile(string fileName)
	{
		if (
			!fileName.StartsWith(dbName,StringComparison.CurrentCultureIgnoreCase) ||
			fileName.IndexOf('_') < 1) return;

		string fullPath = Path.Combine(dbPath, fileName);
		File.Delete(fullPath);		
	}

	public string DownloadFile(string? fileName = null)
	{
		if (fileName is null)
			return JsonConvert.SerializeObject(db);

		string fullPath = Path.Combine(dbPath, fileName);

		if (File.Exists(fullPath))
			return File.ReadAllText(fullPath);

		return "[]";
	}

	public List<string> BackupFileList()
	{
		string[] files = Directory.GetFiles(dbPath);
		return files.Where(a => Path.GetFileName(a).StartsWith(dbName + "_") && Path.GetExtension(a).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
					.Select(a => Path.GetFileName(a))
					.OrderByDescending(a => a)
					.ToList();
	}
}
