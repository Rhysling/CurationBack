using CurationBack.Models;
using CurationBack.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PicturesController(PicturesDb db) : ControllerBase
{
	// GET: api/Pictures/GetPublicList
	[HttpGet("[action]")]
	public List<PictureItem> GetPublicList()
	{
		return db.GetPublicList();
	}

}
