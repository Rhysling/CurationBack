namespace CurationBack.Models;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public class AppSettings
{
	public AS_Polson Polson { get; set; }
	public AS_Jwt Jwt { get; set; }
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
