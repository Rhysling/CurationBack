using CurationBack.Models;
using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TrafficController(TrafficSqliteDb tDb, TrafficCollector collector, ILogger<TrafficController> logger) : ControllerBase
{
	public const string CollectRateLimitPolicy = "traffic-collect";

	private readonly TrafficSqliteDb tDb = tDb;
	private readonly TrafficCollector collector = collector;
	private readonly ILogger<TrafficController> logger = logger;

	// POST: api/Traffic/Collect
	// Anonymous beacon. The body is JSON sent as text/plain (sendBeacon / no-cors fetch), so it is
	// read by hand. Always 204, whatever happens. Never log the body, IP or User-Agent.
	[HttpPost("[action]")]
	[AllowAnonymous]
	[EnableRateLimiting(CollectRateLimitPolicy)]
	public async Task<IActionResult> Collect()
	{
		try
		{
			string? body = await ReadBodyAsync(Request, HttpContext.RequestAborted);
			if (body != null)
			{
				string ip = TrafficCollector.ClientIp(HttpContext.Connection.RemoteIpAddress);
				string ua = Request.Headers.UserAgent.ToString();
				string outcome = collector.Process(body, ip, ua, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
				logger.LogDebug("Traffic collect: {Outcome}", outcome);
			}
			else
				logger.LogDebug("Traffic collect: drop:body");
		}
		catch (Exception ex)
		{
			logger.LogDebug("Traffic collect failed: {Error}", ex.GetType().Name);
		}
		return NoContent();
	}

	// Reads up to MaxBodyBytes; returns null when the body is larger (or empty).
	private static async Task<string?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
	{
		if (request.ContentLength is long len && len > TrafficCollector.MaxBodyBytes) return null;

		var buf = new byte[TrafficCollector.MaxBodyBytes + 1];
		int total = 0;
		while (total < buf.Length)
		{
			int n = await request.Body.ReadAsync(buf.AsMemory(total, buf.Length - total), ct);
			if (n == 0) break;
			total += n;
		}
		if (total == 0 || total > TrafficCollector.MaxBodyBytes) return null;
		return Encoding.UTF8.GetString(buf, 0, total);
	}

	// *** Reports (admin only, same attribute as DbController) ***

	// GET: api/Traffic/Summary?from&to&tzOffsetMin
	[HttpGet("[action]")]
	[AdminAuthorize]
	public IActionResult Summary(long? from, long? to, int? tzOffsetMin)
	{
		if (!ValidRange(from, to)) return BadRequest("Invalid range");
		long f = from!.Value, t = to!.Value, span = t - f;

		return Ok(new TrafficSummary(tDb.GetStats(f, t), tDb.GetStats(f - span, f)));
	}

	// GET: api/Traffic/TimeSeries?from&to&tzOffsetMin&interval=hour|day|month
	[HttpGet("[action]")]
	[AdminAuthorize]
	public IActionResult TimeSeries(long? from, long? to, int? tzOffsetMin, string? interval)
	{
		if (!ValidRange(from, to)) return BadRequest("Invalid range");
		long f = from!.Value, t = to!.Value;
		long offMs = OffsetMs(tzOffsetMin);
		const int maxBuckets = 1000;

		var points = new List<TrafficPoint>();

		if (interval is "hour" or "day")
		{
			long bucketMs = interval == "hour" ? 3_600_000 : 86_400_000;
			long first = FloorDiv(f + offMs, bucketMs) * bucketMs - offMs;
			long last = FloorDiv(t - 1 + offMs, bucketMs) * bucketMs - offMs;
			if ((last - first) / bucketMs + 1 > maxBuckets) return BadRequest("Too many buckets");

			var data = tDb.GetFixedBuckets(f, t, offMs, bucketMs);
			for (long b = first; b <= last; b += bucketMs)
			{
				var (v, p) = data.TryGetValue(b, out var x) ? x : (0, 0);
				points.Add(new TrafficPoint(b, v, p));
			}
		}
		else if (interval == "month")
		{
			var start = DateTimeOffset.FromUnixTimeMilliseconds(f + offMs);
			var end = DateTimeOffset.FromUnixTimeMilliseconds(t - 1 + offMs);
			int months = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
			if (months > maxBuckets) return BadRequest("Too many buckets");

			var data = tDb.GetMonthBuckets(f, t, offMs);
			var m = new DateTimeOffset(start.Year, start.Month, 1, 0, 0, 0, TimeSpan.Zero);
			for (int i = 0; i < months; i++, m = m.AddMonths(1))
			{
				var (v, p) = data.TryGetValue(m.ToString("yyyy-MM"), out var x) ? x : (0, 0);
				points.Add(new TrafficPoint(m.ToUnixTimeMilliseconds() - offMs, v, p));
			}
		}
		else
			return BadRequest("Unknown interval");

		return Ok(points);
	}

	// GET: api/Traffic/Breakdown?from&to&tzOffsetMin&dimension&limit=50
	[HttpGet("[action]")]
	[AdminAuthorize]
	public IActionResult Breakdown(long? from, long? to, int? tzOffsetMin, string? dimension, int? limit)
	{
		if (!ValidRange(from, to)) return BadRequest("Invalid range");
		if (!TrafficSqliteDb.IsKnownDimension(dimension)) return BadRequest("Unknown dimension");

		int lim = Math.Clamp(limit ?? 50, 1, 500);
		return Ok(tDb.GetBreakdown(from!.Value, to!.Value, dimension!, lim));
	}

	// GET: api/Traffic/Realtime
	[HttpGet("[action]")]
	[AdminAuthorize]
	public IActionResult Realtime()
	{
		return Ok(new TrafficRealtime(tDb.GetRealtimeVisitors(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
	}

	private static bool ValidRange(long? from, long? to) =>
		from.HasValue && to.HasValue && to.Value > from.Value;

	private static long OffsetMs(int? tzOffsetMin) => Math.Clamp(tzOffsetMin ?? 0, -840, 840) * 60_000L;

	private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
