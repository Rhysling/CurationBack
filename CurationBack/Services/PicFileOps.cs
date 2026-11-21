using CurationBack.Models;

namespace CurationBack.Services;

public class PicFileOps
{
	private readonly bool isProduction;
	private readonly string basePath;

	public PicFileOps(AppSettings aps)
	{
		isProduction = aps.Polson.IsProduction;

		string dir = Directory.GetCurrentDirectory();
		if (isProduction)
			basePath = Path.Combine(dir, @"wwwroot\pics\");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			basePath = dir[0..ix] + @"CurationFront\public\pics\";
		}
	}

	public void SaveFile(string fileName, byte[] fileData)
	{
		string fullPath = Path.Combine(basePath, fileName);
		File.WriteAllBytes(fullPath, fileData);
	}

	public void RenameFile(string oldFileName, string newFileName)
	{
		if (oldFileName.Equals(newFileName, StringComparison.Ordinal))
			return;

		string oldFn = Path.Combine(basePath, oldFileName);
		string newFn = Path.Combine(basePath, newFileName);
		File.Move(oldFn, newFn);
	}

	public void DeleteFile(string fileName)
	{
		File.Delete(Path.Combine(basePath, fileName));
	}

	public List<string> GetImageFileNames()
	{
		return Directory.GetFiles(basePath, "*.*", SearchOption.TopDirectoryOnly)
			.Where(f => f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
			)
			.Select(a => new FileInfo(a).Name)
			.OrderBy(a => a)
			.ToList();
	}

	public long GetFileSize(string fileName)
	{
		string fullPath = Path.Combine(basePath, fileName);
		return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
	}
}
