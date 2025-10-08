using CurationBack.Models;
using CurationBack.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration);

var aps = builder.Configuration.Get<AppSettings>();

if (aps != null)
{
	builder.Services.AddSingleton(aps);
}

builder.Services.AddSingleton<PicturesDb>();
builder.Services.AddSingleton<UsersDb>();

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = true,
		ValidateIssuerSigningKey = true,
		ValidIssuer = aps?.Jwt.Issuer,
		ValidAudience = aps?.Jwt.Issuer,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(aps?.Jwt.Key ?? ""))
	};
});

builder.Services.AddControllers();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
else
{
	app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
	await next();

	var path = context.Request.Path.Value ?? "/";
	bool notApi = !path.StartsWith("/api");
	bool noExt = !Path.HasExtension(path);

	if (notApi && noExt)
	{
		context.Request.Path = "/index.html";
		await next();
	}
});

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".html"] = "text/html";
provider.Mappings[".webmanifest"] = "application/manifest+json";

app.UseStaticFiles(new StaticFileOptions()
{
	ContentTypeProvider = provider
});

app.UseRouting();

if (app.Environment.IsDevelopment())
	app.UseCors(builder =>
	{
		builder
		//.AllowAnyOrigin()
		//.AllowAnyMethod()
		.WithOrigins("http://localhost:5050", "http://127.0.0.1:5050")
		.WithMethods("POST", "GET", "OPTIONS", "PUT")
		.AllowAnyHeader()
		.AllowCredentials();
	});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
