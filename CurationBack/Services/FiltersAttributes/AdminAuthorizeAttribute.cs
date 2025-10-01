using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CurationBack.Services.FiltersAttributes
{
	public class AdminAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
	{
		public string? Permissions { get; set; } //Permission string to get from controller

		public void OnAuthorization(AuthorizationFilterContext context)
		{
			var isAminFromToken = bool.Parse(context.HttpContext.User.Claims.Where(a => a.Type == "IsAdmin").Select(a => a.Value).FirstOrDefault() ?? "false");

			if (!isAminFromToken)
				context.Result = new UnauthorizedResult();

			return;
		}
	}
}
