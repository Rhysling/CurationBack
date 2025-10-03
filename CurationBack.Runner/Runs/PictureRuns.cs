using CurationBack.Models;
using CurationBack.Services;

namespace CurationBack.Runner.Runs;

public class PictureRuns(AppSettings aps)
{
	private readonly PicturesDb _pdb = new(aps);

	public void LoadPicturesFromDir()
	{
		List<PictureItem> pics = [];

		var dirSource = @"D:\yy\tp6";
		var dirDest = @"D:\UserData\Documents\AppDev\CurationFront\public\pics";


		var infos = Directory.GetFiles(dirSource, "*.*", SearchOption.TopDirectoryOnly)
			.Where(f => f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
			)
			.Select(a => new FileInfo(a))
			.OrderByDescending(a => a.CreationTime)
			//.Take(4)
			.ToList();

		int ix;

		var dbPics = _pdb.GetAll();
		if (dbPics.Count == 0)
			ix = 100;
		else
			ix = dbPics.Max(a => a.Seq) + 10;

		foreach (var f in infos)
		{
			var pic = new PictureItem
			{
				FileName = f.Name,
				Seq = ix,
				Keywords = [.. f.Name[0..f.Name.IndexOf(f.Extension)].Split('-')],
				Description = "Sample picture",
				IsDeleted = false,
				IsMissing = false
			};

			ix += 10;
			pics.Add(pic);

			f.CopyTo(Path.Combine(dirDest, f.Name), true);
		}

		_pdb.SaveBatch(pics);
	}
}
