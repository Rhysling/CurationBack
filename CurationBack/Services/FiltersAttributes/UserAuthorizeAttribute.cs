using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CurationBack.Services.FiltersAttributes
{
	public class UserAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
	{
		public string? Permissions { get; set; } //Permission string to get from controller

		public void OnAuthorization(AuthorizationFilterContext context)
		{
			var isUserFromToken = !string.IsNullOrWhiteSpace(context.HttpContext.User?.Claims?.Where(a => a.Type == "UserId").Select(a => a.Value).FirstOrDefault());

			if (!isUserFromToken)
				context.Result = new UnauthorizedResult();

			return;
		}
	}
}
