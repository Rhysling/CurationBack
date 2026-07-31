using CurationBack.Models;
using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using CurationBack.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PicturesController(AppSettings aps, PicturesSqliteDb db, PicFileOps pfOps) : ControllerBase
{
	// GET: api/Pictures/GetPublicList
	[HttpGet("[action]")]
	public List<PictureItem> GetPublicList()
	{
		return db.GetAll(includeMissing: false, includeDeleted: false);
	}

	// GET: api/Pictures/Rss
	[HttpGet("[action]")]
	public ContentResult Rss()
	{
		var items = db.GetAll(includeMissing: false, includeDeleted: false)
			.OrderByDescending(p => p.Ts)
			.ToList();

		string baseUrl = $"{Request.Scheme}://{Request.Host}";

		XNamespace media = "http://search.yahoo.com/mrss/";
		XNamespace content = "http://purl.org/rss/1.0/modules/content/";

		var channel = new XElement("channel",
			new XElement("title", "Polson Pictures"),
			new XElement("link", baseUrl),
			new XElement("description", "Latest pictures from the Polson curated collection."),
			new XElement("language", "en-us"),
			new XElement("lastBuildDate", DateTime.UtcNow.ToString("R"))
		);

		foreach (var pic in items)
		{
			string slug = Path.GetFileNameWithoutExtension(pic.FileName);
			string pageUrl = $"{baseUrl}/picture?p={Uri.EscapeDataString(slug)}";
			string imgUrl = $"{baseUrl}/pics/{Uri.EscapeDataString(pic.FileName)}";
			string title = string.IsNullOrWhiteSpace(pic.Description) ? slug : pic.Description;
			string summary = string.IsNullOrWhiteSpace(pic.Description) ? pic.FileName : pic.Description;
			string pubDate = DateTimeOffset.FromUnixTimeSeconds(pic.Ts).UtcDateTime.ToString("R");
			string mimeType = GetImageMimeType(pic.FileName);
			long fileSize = pfOps.GetFileSize(pic.FileName);

			string encodedAlt = System.Net.WebUtility.HtmlEncode(title);

			//string htmlBody = $"<p>{System.Net.WebUtility.HtmlEncode(summary)}</p><img src=\"{imgUrl}\" alt=\"{encodedAlt}\" />";
			string htmlBody = $"<img src=\"{imgUrl}\" alt=\"{encodedAlt}\" />";

			var item = new XElement("item",
				new XElement("title", title),
				new XElement("link", pageUrl),
				new XElement("guid", new XAttribute("isPermaLink", "true"), pageUrl),
				new XElement("pubDate", pubDate),
				new XElement("description", new XCData(htmlBody)),
				new XElement(content + "encoded", new XCData(htmlBody)),
				new XElement("enclosure",
					new XAttribute("url", imgUrl),
					new XAttribute("type", mimeType),
					new XAttribute("length", fileSize)
				),
				new XElement(media + "content",
					new XAttribute("url", imgUrl),
					new XAttribute("type", mimeType),
					new XAttribute("medium", "image"),
					new XAttribute("fileSize", fileSize)
				),
				new XElement(media + "thumbnail", new XAttribute("url", imgUrl))
			);

			foreach (var kw in pic.Keywords)
				item.Add(new XElement("category", kw));

			channel.Add(item);
		}

		var doc = new XDocument(
			new XDeclaration("1.0", "utf-8", null),
			new XElement("rss",
				new XAttribute("version", "2.0"),
				new XAttribute(XNamespace.Xmlns + "media", media),
				new XAttribute(XNamespace.Xmlns + "content", content),
				channel
			)
		);

		return Content(doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.DisableFormatting), "application/rss+xml", Encoding.UTF8);
	}

	// GET: api/Pictures/GetBySlug
	[HttpGet("[action]")]
	public PictureItem GetBySlug(string slug)
	{
		return db.FindBySlug(slug);
	}

	// GET: api/Pictures/GetById
	[HttpGet("[action]")]
	public PictureItem GetById(int id)
	{
		return db.GetById(id) ?? new PictureItem();
	}

	// GET: api/Pictures/GetAll
	[HttpGet("[action]")]
	[AdminAuthorize()]
	public List<PictureItem> GetAll()
	{
		return db.GetAll(includeMissing: true, includeDeleted: true);
	}

	// GET: api/Pictures/GetAuditList
	[HttpGet("[action]")]
	[AdminAuthorize()]
	public ActionResult<Object> GetAuditList()
	{
		string dir = Directory.GetCurrentDirectory();
		if (aps.Polson.IsProduction)
			dir = Path.Combine(dir, @$"wwwroot\pics");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dir = dir[0..ix] + @$"CurationFront\public\pics";
		}

		var fileNames = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
			.Where(f => f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
			)
			.Select(a => new FileInfo(a).Name)
			.OrderBy(a => a)
			.ToList();

		var result = db.GetAuditLists(fileNames);
		return new { Missing = result.missing, Orphans = result.orphans };
	}

	// POST api/Pictures/Save
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public ActionResult<PictureItem> Save(PictureItem picItem)
	{
		if (picItem.Id == 0)
			return Ok(db.SaveItem(picItem));

		var oldPicItem = db.GetById(picItem.Id, includeDeleted: true);

		if (oldPicItem == null)
			return BadRequest("Picture not found.");

		if (oldPicItem.FileName.Equals(picItem.FileName, StringComparison.Ordinal))
			return Ok(db.SaveItem(picItem));

		// Rename the file
		string newFn = picItem.FileName;

		if (string.IsNullOrWhiteSpace(newFn))
			return BadRequest("Filename cannot be empty.");

		if (Regex.IsMatch(newFn, @"[^A-Za-z0-9\-_\.]"))
			return BadRequest("Filename cannot have invalid characters.");

		if (!(newFn.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
				|| newFn.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
				|| newFn.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
				|| newFn.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
			) return BadRequest("Filename must have a valid image extension.");

		pfOps.RenameFile(oldPicItem.FileName, newFn);
		return Ok(db.SaveItem(picItem));
	}

	// POST api/Pictures/SaveWithImg
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public async Task<ActionResult<PictureItem>> SaveWithImg()
	{
		try
		{
			var file = Request.Form.Files[0];
			PictureItem picItem = JsonConvert.DeserializeObject<PictureItem>(Request.Form["picItemJSON"].FirstOrDefault() ?? "{}")!;

			if (file is null || file.Length == 0)
				return BadRequest("FormData missing");

			string fn = picItem.FileName;

			if (string.IsNullOrWhiteSpace(fn))
				return BadRequest("Filename cannot be empty.");

			if (Regex.IsMatch(fn, @"[^A-Za-z0-9\-_\.]"))
				return BadRequest("Filename cannot have invalid characters.");

			if (!(fn.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
					|| fn.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
					|| fn.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
					|| fn.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
				) return BadRequest("Filename must have a valid image extension.");

			byte[] fileBytes;
			using (var stream = new MemoryStream())
			{
				await file.CopyToAsync(stream);
				fileBytes = stream.ToArray();
			}

			pfOps.SaveFile(fn, fileBytes);

			picItem.Ts = (int)DateTime.Now.ToUnixTime();
			picItem.IsMissing = false;

			return Ok(db.SaveItem(picItem));
		}
		catch (Exception ex)
		{
			return StatusCode(500, $"Internal server error: {ex}");
		}
	}

	// POST: api/Pictures/CleanPics
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public List<PictureItem> CleanPics()
	{
		string dir = Directory.GetCurrentDirectory();
		if (aps.Polson.IsProduction)
			dir = Path.Combine(dir, @$"wwwroot\pics");
		else
		{
			int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
			dir = dir[0..ix] + @$"CurationFront\public\pics";
		}

		var fileNames = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
			.Where(f => f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
				|| f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
			)
			.Select(a => new FileInfo(a).Name)
			.OrderBy(a => a)
			.ToList();

		db.SyncFromFileList(fileNames);
		return db.GetAll(includeMissing: true, includeDeleted: true);
	}

	// POST: api/Pictures/ResequencePics
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public List<PictureItem> ResequencePics()
	{
		return db.ReSequence();
	}

	// POST api/Pictures/RemoveMissing
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public ActionResult<int> RemoveMissing()
	{
		return Ok(db.RemoveMissing());
	}

	// POST api/Pictures/Destroy
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public ActionResult Destroy(PictureItem picItem)
	{
		if (!string.IsNullOrWhiteSpace(picItem.FileName) && db.CountByFileName(picItem.FileName) == 1)
			pfOps.DeleteFile(picItem.FileName);

		db.Destroy(picItem.Id);

		return Ok();
	}

	// **** Private ****

	private static string GetImageMimeType(string fileName)
	{
		string ext = Path.GetExtension(fileName).ToLowerInvariant();
		return ext switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			_ => "application/octet-stream",
		};
	}
}
