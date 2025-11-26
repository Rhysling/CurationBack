using CurationBack.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CurationBack.Services;

public class BaseDb<TItem> where TItem : IDbItem
{
	protected List<TItem> db;
	protected readonly string dbFullPath;

	public BaseDb(AppSettings aps, string dbName)
	{
		string dir = Directory.GetCurrentDirectory();
		//D:\UserData\Documents\AppDev\NextSemiBack\NextSemiBack
		if (aps.Polson.IsProduction)
			dbFullPath = Path.Combine(dir, @$"Db\{dbName}.json");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dbFullPath = dir[0..ix] + @$"CurationBack\CurationBack\Db\{dbName}.json";
			// @$"CurationFront\public\docs\{dbName}.json";
			//D:\UserData\Documents\AppDev\CurationFront\db
		}

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
		File.WriteAllText(FilePath, JsonConvert.SerializeObject(db));
		SaveFile();
	}


	protected void SaveFile()
	{
		File.WriteAllText(FilePath, JsonConvert.SerializeObject(db));
	}
}
