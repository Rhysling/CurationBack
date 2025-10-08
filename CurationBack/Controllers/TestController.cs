using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TestController(PicturesDb tDb) : ControllerBase
{
	private readonly PicturesDb tDb = tDb;

	[HttpGet("[action]")]
	public string GetUnsecuredValue()
	{
		//tDb.Create();
		//return tDb.FilePath;
		return "foo";
	}

	[HttpGet("[action]")]
	[UserAuthorize()]
	public string GetSecuredValue()
	{
		//var user = HttpContext.User;
		//var claims = user.Claims;
		//var x = user.Identity;
		//var y = 2;

		return "This is a secured value.";
	}

	[HttpGet("[action]")]
	[AdminAuthorize()]
	public string GetAdminValue()
	{
		//var user = HttpContext.User;
		//var claims = user.Claims;
		//var x = user.Identity;
		//var y = 2;

		return "This is an admin value.";
	}

	[HttpGet("[action]")]
	public ActionResult Throw()
	{
		throw new Exception("Boom!");

		//return View();
	}

}
