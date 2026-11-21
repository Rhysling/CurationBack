using CurationBack.Models;
using System.Globalization;

namespace CurationBack.Services;

// Deletes old salts (so old visitor hashes can't be linked or reversed) and expired events.
// Runs shortly after startup, then every 24 hours. Never throws into the host.
public class TrafficHousekeeping(TrafficSqliteDb tDb, AppSettings aps, ILogger<TrafficHousekeeping> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
			RunOnce();

			using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
			while (await timer.WaitForNextTickAsync(stoppingToken))
				RunOnce();
		}
		catch (OperationCanceledException) { }
	}

	public void RunOnce()
	{
		try
		{
			var now = DateTimeOffset.UtcNow;
			int retentionDays = Math.Max(1, aps.Traffic?.RetentionDays ?? 400);
			string today = now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			int salts = tDb.DeleteSaltsBefore(today);
			int events = tDb.DeleteEventsBefore(now.ToUnixTimeMilliseconds() - retentionDays * 86_400_000L);
			tDb.Optimize();

			logger.LogInformation("Traffic housekeeping: deleted {Salts} salts, {Events} events (retention {Days} days)",
				salts, events, retentionDays);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Traffic housekeeping failed");
		}
	}
}
