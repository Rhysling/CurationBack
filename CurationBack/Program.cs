using CurationBack.Controllers;
using CurationBack.Models;
using CurationBack.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
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

builder.Services.AddSingleton<PicturesSqliteDb>();
builder.Services.AddSingleton<UsersSqliteDb>();
builder.Services.AddSingleton<PicFileOps>();
builder.Services.AddSingleton<TrafficSqliteDb>();
builder.Services.AddSingleton<TrafficCollector>();
builder.Services.AddHostedService<TrafficHousekeeping>();

// Traffic collect: fixed window per client IP. Rejections answer 204 (the beacon never reads it).
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status204NoContent;
	int permits = Math.Max(1, aps?.Traffic?.CollectPermitsPerMinute ?? 60);
	options.AddPolicy(TrafficController.CollectRateLimitPolicy, context =>
		RateLimitPartition.GetFixedWindowLimiter(
			TrafficCollector.ClientIp(context.Connection.RemoteIpAddress),
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = permits,
				Window = TimeSpan.FromMinutes(1),
				QueueLimit = 0,
			}));
});

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

// Traffic tables live in traffic.db (created on first open); create them if missing. Never block startup.
try
{
	app.Services.GetRequiredService<TrafficSqliteDb>().EnsureSchema();
}
catch (Exception ex)
{
	app.Logger.LogError(ex, "Traffic schema init failed");
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();
else
	app.UseHttpsRedirection();

//app.UseDeveloperExceptionPage();

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

app.UseRateLimiter();

app.MapControllers();

app.Run();
