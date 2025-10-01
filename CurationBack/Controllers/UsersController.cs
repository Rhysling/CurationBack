using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using CurationBack.Models;
using CurationBack.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace CurationBack.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public partial class UsersController(AppSettings aps, UsersDb db) : ControllerBase
	{
		private readonly AppSettings aps = aps;
		private readonly UsersDb db = db;


		// POST: api/Users/Login
		[AllowAnonymous]
		[HttpPost("[action]")]
		public IActionResult Login([FromBody] UserLogin login)
		{
			var ucr = AuthenticateUser(login);

			if (ucr is not null)
				return Ok(ucr);

			return Unauthorized();
		}

		// POST: api/Users/Register
		[AllowAnonymous]
		[HttpPost("[action]")]
		public IActionResult Register([FromBody] UserRegister reg)
		{
			(var ucr, var problems) = RegisterUser(reg);

			if (ucr is null)
				return BadRequest(problems);

			return Ok(ucr);
		}

		// POST: api/Users/GetAll
		[AdminAuthorize]
		[HttpGet("[action]")]
		public IActionResult GetAll()
		{
			return Ok(db.GetAll());
		}


		// PRIVATE

		private UserClientRemote? AuthenticateUser(UserLogin login)
		{
			if (String.IsNullOrWhiteSpace(login.Email))
				return null;

			var u = db.FindByEmail(login.Email);

			if (u is null)
				return null;

			if (u.IsDisabled)
				return null;

			if (!db.ValidatePassword(login.Email, login.Pw))
				return null;

			var ucr = new UserClientRemote
			{
				UserId = u.UserId,
				Email = u.Email,
				FullName = u.FullName,
				IsAdmin = u.IsAdmin,
				IsDisabled = u.IsDisabled
			};

			ucr.Token = GenerateJSONWebToken(ucr);
			db.SaveToken(ucr.Email, ucr.Token);

			return ucr;
		}

		private string GenerateJSONWebToken(UserClientRemote user)
		{
			var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(aps.Jwt.Key));
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

			var claims = new[] {
				new Claim("UserId", user.UserId.ToString()),
				new Claim("Email", user.Email),
				new Claim("FullName", user.FullName ?? ""),
				new Claim("IsAdmin", user.IsAdmin.ToString())
			};

			var token = new JwtSecurityToken(aps.Jwt.Issuer,
					aps.Jwt.Issuer,
					claims,
					//expires: DateTime.Now.AddSeconds(30),
					expires: DateTime.Now.AddDays(10),
					signingCredentials: credentials);

			return "Bearer " + new JwtSecurityTokenHandler().WriteToken(token);
		}

		private (UserClientRemote? user, List<string> problems) RegisterUser(UserRegister ur)
		{
			List<string> problemsList = [];

			// isValidEmail = /^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9_\-\.]+)\.([a-zA-Z]{2,5})$/.test(email);

			if (!Regex.Match(ur.Email, "^([a-zA-Z0-9_\\-\\.]+)@([a-zA-Z0-9_\\-\\.]+)\\.([a-zA-Z]{2,5})$").Success)
				problemsList.Add("Bad email");

			var u = db.FindByEmail(ur.Email);
			if (u is not null)
				problemsList.Add("Email already exists");

			ur.Pw ??= "";

			if (ur.Pw != ur.Pw.Trim())
				problemsList.Add("Password cannot begin or end with spaces");

			if (ur.Pw.Length < 6)
				problemsList.Add("Password must be at least 6 characters");

			if (problemsList.Count == 0)
			{
				var ucr = new UserClientRemote
				{
					UserId = 0,
					Email = ur.Email.ToLower(),
					FullName = ur.FullName ?? ""
				};
				ucr = db.SaveUser(ucr);

				ucr.Token = GenerateJSONWebToken(ucr);

				db.SaveToken(ucr.Email, ucr.Token);
				db.SavePassword(ucr.Email, ur.Pw);

				return (ucr, problemsList);
			}

			return (null, problemsList);
		}


	}
}
