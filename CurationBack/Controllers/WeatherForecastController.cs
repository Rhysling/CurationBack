using CurationBack.Models;
using CurationBack.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurationBack.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherForecastController(AppSettings aps) : ControllerBase
{
	private static readonly string[] Summaries =
	[
		"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
	];

	

	[HttpGet]
	public IEnumerable<WeatherForecast> Get()
	{
		return [.. Enumerable.Range(1, 5).Select(index => new WeatherForecast
		{
			Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
			TemperatureC = Random.Shared.Next(-20, 55),
			Summary = Summaries[Random.Shared.Next(Summaries.Length)]
		})];
	}

	[HttpGet("[action]")]
	public string GetDir()
	{
		//string dir = Path.Combine(Directory.GetCurrentDirectory(), "Db");
		//string files = String.Join(',', Directory.GetFiles(dir));

		//return files;
		return $"JWT: {aps.Jwt.Key} | {aps.Jwt.Issuer}";
		;
	}
}
