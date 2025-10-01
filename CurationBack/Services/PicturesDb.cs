using CurationBack.Models;
using Newtonsoft.Json;

namespace CurationBack.Services;

public class PicturesDb
{
	private List<PictureItem> db;
	private readonly string filePath;

	public PicturesDb(AppSettings aps)
	{
		string dir = Directory.GetCurrentDirectory();
		//D:\UserData\Documents\AppDev\CurationBack\CurationBack
		if (aps.Polson.IsProduction)
			filePath = Path.Combine(dir, @"wwwroot\docs\PicturesDb.json");
		else
			filePath = dir.Replace(@"CurationBack\CurationBack", @"CurationFront\public\docs\PicturesDb.json");

		if (File.Exists(filePath))
		{
			string json = File.ReadAllText(filePath);
			db = JsonConvert.DeserializeObject<List<PictureItem>>(json) ?? [];
		}
		else
			db = [];

	}
	public string FilePath => filePath;

	public List<PictureItem> Items => db;

	public void SaveItem(PictureItem item)
	{
		db = [.. db.Where(a => a.FileName != item.FileName)];
		db.Add(item);
		File.WriteAllText(FilePath, JsonConvert.SerializeObject(db));
	}

	public void SetDeleted(PictureItem item, bool isDeleted)
	{
		SetDeleted(item.FileName, isDeleted);
	}

	public void SetDeleted(string fileName, bool isDeleted)
	{
		foreach (var a in db.Where(a => a.FileName == fileName))
			a.IsDeleted = isDeleted;

		File.WriteAllText(FilePath, JsonConvert.SerializeObject(db));
	}

	public void SyncFromFileList(List<PictureItem> piFromFiles)
	{
		//db = db.Where(a => a.FileName != fileName).ToList();

		// Mark all missing

		// Add the orphans


		File.WriteAllText(FilePath, JsonConvert.SerializeObject(db));
	}
}
