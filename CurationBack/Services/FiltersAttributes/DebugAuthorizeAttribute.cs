using Microsoft.AspNetCore.Mvc.Filters;

namespace CurationBack.Services.FiltersAttributes;



public class DebugAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{


	public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
	{
		var user = context.HttpContext.User;

		await Task.FromResult(user);
	}
}