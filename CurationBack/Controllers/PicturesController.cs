using CurationBack.Models;
using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

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
		// TODO: Get the file list from disk
		// Run SyncFromFileList
		//
		return db.GetAll(includeMissing: true, includeDeleted: true);
	}

	// POST api/Pictures/Save
	[HttpPost("[action]")]
	[AdminAuthorize()]
	public ActionResult<PictureItem> Save(PictureItem picItem)
	{
		return Ok(db.SaveItem(picItem));
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

			return Ok(db.SaveItem(picItem));
		}
		catch (Exception ex)
		{
			return StatusCode(500, $"Internal server error: {ex}");
		}
	}

}
