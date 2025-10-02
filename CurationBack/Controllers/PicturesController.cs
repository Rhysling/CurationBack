using CurationBack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PicturesController(PicturesDb db) : ControllerBase
{
	// GET: api/Pictures/GetAll
	[AllowAnonymous]
	[HttpGet("[action]")]
	public IActionResult GetAll()
	{
		return Ok(db.GetAll());
	}



}
