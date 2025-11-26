using CurationBack.Models;
using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using CurationBack.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PicturesController(AppSettings aps, PicturesDb db) : ControllerBase
{
	// GET: api/Pictures/GetPublicList
	[HttpGet("[action]")]
	public List<PictureItem> GetPublicList()
	{
		return db.GetAll(includeMissing: false, includeDeleted: false);
	}

	// GET: api/Pictures/GetBySlug
	[HttpGet("[action]")]
	public PictureItem GetBySlug(string slug)
	{
		return db.FindBySlug(slug);
	}

	// GET: api/Pictures/GetBySlug
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

	// GET: api/Pictures/GetCleanPics
	[HttpGet("[action]")]
	[AdminAuthorize()]
	public List<PictureItem> GetCleanPics()
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

		if (string.Compare(oldPicItem.FileName, picItem.FileName, StringComparison.Ordinal) == 0)
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

		try
		{
			string dir = Directory.GetCurrentDirectory();
			if (aps.Polson.IsProduction)
				dir = Path.Combine(dir, @$"wwwroot\pics\");
			else
			{
				int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
				dir = dir[0..ix] + @$"CurationFront\public\pics\";
			}

			string oldFn = Path.Combine(dir, oldPicItem.FileName);
			newFn = Path.Combine(dir, newFn);

			System.IO.File.Move(oldFn, newFn);

			return Ok(db.SaveItem(picItem));
		}
		catch (Exception ex)
		{
			return StatusCode(500, $"Internal server error: {ex}");
		}
	}

	// POST api/Pictures/SaveWithImg
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public ActionResult<PictureItem> SaveWithImg()
	{
		try
		{
			var file = Request.Form.Files[0];
			PictureItem picItem = JsonConvert.DeserializeObject<PictureItem>(Request.Form["picItemJSON"].FirstOrDefault() ?? "{}")!;

			if (file is null || file.Length == 0)
				return BadRequest("FormData missing");


			string dir = Directory.GetCurrentDirectory();
			if (aps.Polson.IsProduction)
				dir = Path.Combine(dir, @$"wwwroot\pics\{picItem.FileName}");
			else
			{
				int ix = dir.IndexOf(@"CurationBack\CurationBack", StringComparison.CurrentCultureIgnoreCase);
				dir = dir[0..ix] + @$"CurationFront\public\pics\{picItem.FileName}";
			}

			using (var stream = new FileStream(dir, FileMode.Create))
				file.CopyTo(stream);

			picItem.Ts = (int)DateTime.Now.ToUnixTime();
			picItem.IsMissing = false;

			return Ok(db.SaveItem(picItem));
		}
		catch (Exception ex)
		{
			return StatusCode(500, $"Internal server error: {ex}");
		}
	}

}