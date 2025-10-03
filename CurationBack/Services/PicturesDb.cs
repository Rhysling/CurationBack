using CurationBack.Models;
using CurationBack.Utilities;

namespace CurationBack.Services;

public class PicturesDb(AppSettings aps) : BaseDb<PictureItem>(aps, "PicturesDb")
{
	public override void SaveBatch(List<PictureItem> items)
	{
		int ix, i = 0;
		int minSeq = db.Count != 0 ? db.Min(a => a.Seq) - 10 : 100;
		int ic = items.Count;
		int[] itemsSeq = [.. Enumerable.Range(minSeq, ic).Select(a => a - ic)];

		foreach (var item in items)
		{
			ix = db.FindIndex(a => a.FileName == item.FileName);
			if (ix >= 0)
			{
				item.Id = db[ix].Id;
				item.Seq = db[ix].Seq;
				db.RemoveAt(ix);
			}
			else
			{
				if (item.Id == 0)
					item.Id = db.Count != 0 ? db.Max(a => a.Id) + 1 : 1;

				item.Seq = itemsSeq[i++];
			}

			db.Add(item);

		}
		ReSequence();
		SaveFile();
	}

	public void SetDeleted(string fileName, bool isDeleted)
	{
		int ix = db.FindIndex(a => a.FileName == fileName);
		if (ix >= 0)
		{
			SetDeleted(db[ix].Id, isDeleted);
		}
		SaveFile();
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



	private void ReSequence()
	{
		int seq = 100;
		foreach (var item in db.OrderBy(a => a.Seq))
		{
			item.Seq = seq;
			seq += 10;
		}
	}
}
