using System.Text.Json;

namespace CurationBack.Models;

// Traffic analytics DTOs. Not marked [TypeScriptModel]: the frontend types are
// hand-written in CurationFront/src/types/traffic.d.ts.

// Body of POST /api/Traffic/Collect (camelCase, case-insensitive).
public class TrafficEventIn
{
	public string? Type { get; set; }
	public int? V { get; set; }
	public string? PvId { get; set; }
	public string? Path { get; set; }
	public string? Page { get; set; }
	public string? Pic { get; set; }
	public string? Keyword { get; set; }
	public string? Ref { get; set; }
	public string? UtmSource { get; set; }
	public string? UtmMedium { get; set; }
	public string? UtmCampaign { get; set; }
	public double? Sw { get; set; }
	public string? Lang { get; set; }
	public double? DurationMs { get; set; }
	public string? Name { get; set; }
	public JsonElement? Props { get; set; }
}

// A validated, derived row ready to insert into Events.
public class TrafficEventRow
{
	public long Ts { get; set; }
	public int SchemaVersion { get; set; } = 1;
	public string EventType { get; set; } = "pageview";
	public string? EventName { get; set; }
	public string? PageviewId { get; set; }
	public string VisitorHash { get; set; } = "";
	public string SessionId { get; set; } = "";
	public string Path { get; set; } = "/";
	public string? Page { get; set; }
	public string? PicSlug { get; set; }
	public string? Keyword { get; set; }
	public bool IsEntry { get; set; }
	public string? ReferrerHost { get; set; }
	public string? UtmSource { get; set; }
	public string? UtmMedium { get; set; }
	public string? UtmCampaign { get; set; }
	public string? Browser { get; set; }
	public string? Os { get; set; }
	public string? DeviceType { get; set; }
	public string? ScreenBucket { get; set; }
	public string? Lang { get; set; }
	public string? Country { get; set; }
	public long? DurationMs { get; set; }
	public string? Props { get; set; }
}

public record TrafficStats(long Visitors, long Visits, long Pageviews, double BounceRate, double AvgVisitMs);

public record TrafficSummary(TrafficStats Current, TrafficStats Previous);

public record TrafficPoint(long T, long Visitors, long Pageviews);

public record TrafficBreakdownRow(string Key, long Visitors, long Pageviews);

public record TrafficRealtime(long Visitors);
