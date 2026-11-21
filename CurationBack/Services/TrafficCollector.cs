using CurationBack.Models;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UAParser;

namespace CurationBack.Services;

// Turns a raw /api/Traffic/Collect body into stored rows.
// Privacy: the IP and User-Agent are used only in memory (hash + parsed families) and are never
// stored or logged. Callers must swallow every exception: tracking must never affect the API.
public class TrafficCollector
{
	public const int MaxBodyBytes = 4096;
	private const int MaxField = 256;
	private const int MaxProps = 2048;
	private const long MaxDurationMs = 1_800_000;

	private static readonly Regex BotRegex = new(
		"bot|crawl|spider|slurp|headless|lighthouse|preview|curl|wget|python|httpclient|okhttp",
		RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex TabletRegex = new("iPad|Tablet", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex AndroidRegex = new("Android", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex MobileWordRegex = new("Mobile", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex MobileRegex = new("Mobi|iPhone|Android", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly Parser UaParser = Parser.GetDefault();

	private readonly TrafficSqliteDb tDb;
	private readonly byte[] siteKey;
	private readonly HashSet<string> ownHosts;
	private readonly HashSet<string> allowedEventNames;

	private readonly Lock saltLock = new();
	private string? saltDay;
	private byte[]? salt;

	public TrafficCollector(TrafficSqliteDb tDb, AppSettings aps)
	{
		this.tDb = tDb;
		var t = aps.Traffic ?? new AS_Traffic();
		siteKey = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(t.SiteKey) ? "polson.com" : t.SiteKey);

		var hosts = t.OwnHosts is { Count: > 0 } ? t.OwnHosts : ["polson.com", "localhost"];
		ownHosts = new HashSet<string>(hosts.Select(NormalizeHost), StringComparer.OrdinalIgnoreCase);
		allowedEventNames = new HashSet<string>(t.AllowedEventNames ?? [], StringComparer.Ordinal);
	}

	// Returns what happened, for diagnostics/tests only; the HTTP response is always 204.
	public string Process(string body, string? ip, string? userAgent, long now)
	{
		if (string.IsNullOrWhiteSpace(userAgent) || BotRegex.IsMatch(userAgent)) return "drop:ua";

		TrafficEventIn? ev;
		try { ev = JsonSerializer.Deserialize<TrafficEventIn>(body, JsonOptions); }
		catch (JsonException) { return "drop:json"; }
		if (ev is null) return "drop:json";

		string? type = ev.Type;
		if (type is not ("pageview" or "leave" or "event")) return "drop:type";

		string? pvId = Trunc(ev.PvId);
		if (string.IsNullOrEmpty(pvId)) return "drop:pvId";

		string hash = VisitorHash(ip ?? "", userAgent, now);

		return type switch
		{
			"pageview" => Pageview(ev, pvId, hash, userAgent, now),
			"leave" => Leave(ev, pvId, hash),
			_ => Event(ev, pvId, hash, userAgent, now),
		};
	}

	private string Pageview(TrafficEventIn ev, string pvId, string hash, string ua, long now)
	{
		if (ev.Path is null || !ev.Path.StartsWith('/') || ev.Path.Length > MaxField) return "drop:path";

		var row = NewRow(ua, now, hash);
		row.EventType = "pageview";
		row.PageviewId = pvId;
		row.Path = ev.Path;
		row.Page = Trunc(ev.Page);
		row.PicSlug = Trunc(ev.Pic);
		row.Keyword = Trunc(ev.Keyword)?.ToLowerInvariant();
		row.ReferrerHost = ReferrerHost(ev.Ref);
		row.UtmSource = Trunc(ev.UtmSource);
		row.UtmMedium = Trunc(ev.UtmMedium);
		row.UtmCampaign = Trunc(ev.UtmCampaign);
		row.ScreenBucket = ScreenBucket(ev.Sw);
		row.Lang = Lang(ev.Lang);

		string? sid = tDb.FindRecentSession(hash, now);
		row.SessionId = sid ?? Guid.NewGuid().ToString("N");
		row.IsEntry = sid is null;

		tDb.InsertEvent(row);
		return "pageview";
	}

	private string Leave(TrafficEventIn ev, string pvId, string hash)
	{
		if (ev.DurationMs is not double d || double.IsNaN(d)) return "drop:duration";
		long ms = (long)Math.Clamp(d, 0, MaxDurationMs);
		int n = tDb.UpdateDuration(pvId, hash, ms);
		return n > 0 ? "leave" : "leave:nomatch";
	}

	private string Event(TrafficEventIn ev, string pvId, string hash, string ua, long now)
	{
		string? name = Trunc(ev.Name);
		if (string.IsNullOrEmpty(name) || !allowedEventNames.Contains(name)) return "drop:event";

		var row = NewRow(ua, now, hash);
		row.EventType = "event";
		row.EventName = name;
		row.PageviewId = pvId;

		if (ev.Props is JsonElement p && p.ValueKind != JsonValueKind.Undefined && p.ValueKind != JsonValueKind.Null)
		{
			string raw = p.GetRawText();
			row.Props = Encoding.UTF8.GetByteCount(raw) <= MaxProps ? raw : null;
		}

		var ctx = tDb.GetPageviewContext(pvId, hash);
		if (ctx is { } c)
		{
			row.SessionId = c.SessionId;
			row.Path = c.Path;
			row.Page = c.Page;
		}
		else
		{
			row.SessionId = tDb.FindRecentSession(hash, now) ?? Guid.NewGuid().ToString("N");
			row.Path = "/";
		}

		tDb.InsertEvent(row);
		return "event";
	}

	private static TrafficEventRow NewRow(string ua, long now, string hash)
	{
		var c = UaParser.Parse(ua);
		return new TrafficEventRow
		{
			Ts = now,
			SchemaVersion = 1,
			VisitorHash = hash,
			Browser = Trunc(c.UA.Family),
			Os = Trunc(c.OS.Family),
			DeviceType = DeviceType(ua),
		};
	}

	// *** Derivations ***

	private string VisitorHash(string ip, string ua, long now)
	{
		byte[] s = GetSalt(now);
		byte[] ipBytes = Encoding.UTF8.GetBytes(ip);
		byte[] uaBytes = Encoding.UTF8.GetBytes(ua);

		byte[] buf = new byte[s.Length + siteKey.Length + ipBytes.Length + uaBytes.Length];
		int o = 0;
		s.CopyTo(buf, o); o += s.Length;
		siteKey.CopyTo(buf, o); o += siteKey.Length;
		ipBytes.CopyTo(buf, o); o += ipBytes.Length;
		uaBytes.CopyTo(buf, o);

		return Convert.ToHexString(SHA256.HashData(buf))[..32];
	}

	private byte[] GetSalt(long now)
	{
		string day = DateTimeOffset.FromUnixTimeMilliseconds(now).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		lock (saltLock)
		{
			if (saltDay == day && salt != null) return salt;
			salt = tDb.GetOrCreateSalt(day);
			saltDay = day;
			return salt;
		}
	}

	// Called by restore: the cached salt may no longer exist in the restored DB.
	public void ResetSaltCache()
	{
		lock (saltLock) { saltDay = null; salt = null; }
	}

	public static string ClientIp(IPAddress? addr)
	{
		if (addr is null) return "";
		if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
		return addr.ToString();
	}

	private static string DeviceType(string ua)
	{
		if (TabletRegex.IsMatch(ua) || (AndroidRegex.IsMatch(ua) && !MobileWordRegex.IsMatch(ua))) return "tablet";
		if (MobileRegex.IsMatch(ua)) return "mobile";
		return "desktop";
	}

	private string? ReferrerHost(string? r)
	{
		if (string.IsNullOrWhiteSpace(r)) return null;
		if (!Uri.TryCreate(r.Trim(), UriKind.Absolute, out var uri)) return null;
		if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
		if (string.IsNullOrEmpty(uri.Host)) return null;

		string host = NormalizeHost(uri.Host);
		if (ownHosts.Contains(host)) return null;
		return Trunc(host);
	}

	private static string NormalizeHost(string host)
	{
		host = host.Trim().ToLowerInvariant();
		return host.StartsWith("www.") ? host[4..] : host;
	}

	private static string? ScreenBucket(double? sw)
	{
		if (sw is not double w || double.IsNaN(w) || w <= 0) return null;
		if (w < 640) return "<640";
		if (w < 1024) return "640-1024";
		if (w < 1440) return "1024-1440";
		return ">1440";
	}

	private static string? Lang(string? lang)
	{
		if (lang is null) return null;
		lang = lang.Trim();
		if (lang.Length == 0) return null;
		return lang.Length > 16 ? lang[..16] : lang;
	}

	// Optional strings: blank → NULL, otherwise at most 256 characters.
	private static string? Trunc(string? s) =>
		string.IsNullOrWhiteSpace(s) ? null : s.Length > MaxField ? s[..MaxField] : s;
}
