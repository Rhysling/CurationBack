using CurationBack.Models;
using CurationBack.Utilities;
using Newtonsoft.Json;

namespace CurationBack.Services;

public class PicturesDb(AppSettings aps) : BaseDb<PictureItem>(aps, "PicturesDb")
{
	

	public void SetDeleted(string fileName, bool isDeleted)
	{
		int ix = db.FindIndex(a => a.FileName == fileName);
		if (ix >= 0)
		{
			SetDeleted(db[ix].Id, isDeleted);
		}

	}

	public void SyncFromFileList(List<PictureItem> piFromFiles)
	{
		var master = db.FullOuterJoin(piFromFiles, a => a.FileName, b => b.FileName, (a, b, k) => (a, b)).ToList();
		var missing = master.Where(t => t.a != null && t.b == null).Select(t => t.a!).ToList();
		var orphans = master.Where(t => t.a == null && t.b != null).Select(t => t.b!).ToList();

		foreach (var a in missing)
			a.IsMissing = true;

		SaveBatch(missing);

		foreach (var a in orphans)
			a.IsMissing = false;

		SaveBatch(orphans);
	}
}
