namespace CurationBack.Utilities;

public static class ExtDateTime
{
	/// <summary>
	/// Get the current Unix Epoch time in seconds since 1/1/1970
	/// </summary>
	/// <param name="date"></param>
	/// <returns></returns>
	public static double ToUnixTime(this DateTime dateLocal)
	{
		var t = (dateLocal.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		return t.TotalSeconds;
	}

	public static DateTime FromUnixTime(this double secs)
	{
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secs).ToLocalTime();
	}

	public static DateTime UtcFromUnixTime(this double secs)
	{
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secs);
	}


	public static long ToJsTime(this DateTime dateLocal)
	{
		var t = (dateLocal.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		return (long)t.TotalMilliseconds;
	}

	public static DateTime FromJsTime(this long millisecs)
	{
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(millisecs).ToLocalTime();
	}

#region " Nice ToStrings "

	public static string ToShortPT(this DateTime dateIn)
	{
		TimeZoneInfo pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

		DateTime pstNow = TimeZoneInfo.ConvertTimeFromUtc(dateIn.ToUniversalTime(), pstZone);

		return pstNow.ToString("MM/dd-hh:mmtt") + (pstZone.IsDaylightSavingTime(pstNow) ? " PDT" : " PST");
	}

	public static string ToShortLocal(this DateTime dateInUTC, TimeZoneInfo localZone)
	{
		DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(dateInUTC, localZone);

		string lzid = localZone.Id;

		string sfx = lzid switch
		{
			"Hawaiian Standard Time" => "HST",
			"Alaskan Standard Time" => "AST",
			"Pacific Standard Time" => "PST",
			"Mountain Standard Time" => "MST",
			"Central Standard Time" => "CST",
			"Eastern Standard Time" => "EST",
			_ => "",
		};

		if (sfx.Length == 3 && localZone.IsDaylightSavingTime(localNow))
			sfx = string.Concat(sfx.AsSpan(0, 1), "DT");

		if (sfx.Length > 0)
			sfx = " " + sfx;

		return localNow.ToString("MM/dd-hh:mmtt") + sfx;
	}

	public static string ToFileNameStamp(this DateTime dtIn)
	{
		return dtIn.ToString("yyMMdd-HHmmss");
	}

#endregion

}