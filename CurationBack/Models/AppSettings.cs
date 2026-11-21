namespace CurationBack.Models;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public class AppSettings
{
	public AS_Polson Polson { get; set; }
	public AS_Jwt Jwt { get; set; }
	public AS_Traffic Traffic { get; set; } = new();
}

public class AS_Traffic
{
	public string SiteKey { get; set; } = "polson.com";
	// Empty means the default ("polson.com", "localhost"). Not defaulted here because the
	// config binder appends to an existing list instead of replacing it.
	public List<string> OwnHosts { get; set; } = [];
	public int RetentionDays { get; set; } = 400;
	public int CollectPermitsPerMinute { get; set; } = 60;
	public List<string> AllowedEventNames { get; set; } = [];
}

public class AS_Polson
{
	public string IsProductionString { get; set; }
	public bool IsProduction => IsProductionString == "true";
}

public class AS_Jwt
{
	public string Key { get; set; }
	public string Issuer { get; set; }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
