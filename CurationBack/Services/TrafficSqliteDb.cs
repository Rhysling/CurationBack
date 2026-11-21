using CurationBack.Models;
using Microsoft.Data.Sqlite;

namespace CurationBack.Services;

// Traffic analytics tables (Events, Salts), in their own file traffic.db next to curation.db.
// Backups are traffic_yyyy-MM-dd_HH-mm-ss.db. Every SQL value is a parameter; the only
// interpolated SQL comes from hard-coded tables.
public class TrafficSqliteDb(AppSettings aps) : BaseSqliteDb(aps, "Events", "traffic")
{
	private const string SchemaSql = """
		CREATE TABLE IF NOT EXISTS Events (
		  Id            INTEGER PRIMARY KEY,
		  Ts            INTEGER NOT NULL,
		  SchemaVersion INTEGER NOT NULL DEFAULT 1,
		  EventType     TEXT    NOT NULL,
		  EventName     TEXT    NULL,
		  PageviewId    TEXT    NULL,
		  VisitorHash   TEXT    NOT NULL,
		  SessionId     TEXT    NOT NULL,
		  Path          TEXT    NOT NULL,
		  Page          TEXT    NULL,
		  PicSlug       TEXT    NULL,
		  Keyword       TEXT    NULL,
		  IsEntry       INTEGER NOT NULL DEFAULT 0,
		  ReferrerHost  TEXT    NULL,
		  UtmSource     TEXT    NULL,
		  UtmMedium     TEXT    NULL,
		  UtmCampaign   TEXT    NULL,
		  Browser       TEXT    NULL,
		  Os            TEXT    NULL,
		  DeviceType    TEXT    NULL,
		  ScreenBucket  TEXT    NULL,
		  Lang          TEXT    NULL,
		  Country       TEXT    NULL,
		  DurationMs    INTEGER NULL,
		  Props         TEXT    NULL
		);
		CREATE INDEX IF NOT EXISTS IX_Events_Ts       ON Events(Ts);
		CREATE INDEX IF NOT EXISTS IX_Events_Visitor  ON Events(VisitorHash, Ts);
		CREATE INDEX IF NOT EXISTS IX_Events_Session  ON Events(SessionId);
		CREATE INDEX IF NOT EXISTS IX_Events_Pageview ON Events(PageviewId);

		CREATE TABLE IF NOT EXISTS Salts (
		  Day  TEXT PRIMARY KEY,
		  Salt BLOB NOT NULL
		);
		""";

	// Called at startup and after a restore (an older backup may not have these tables).
	public void EnsureSchema()
	{
		using var conn = Open();

		// WAL is deliberately not used: the production Db folder is on network storage, where
		// SQLite WAL is unsupported. journal_mode persists in the file, so force the normal
		// rollback-journal mode in case the file was ever switched to WAL.
		var jm = conn.CreateCommand();
		jm.CommandText = "PRAGMA journal_mode=DELETE;";
		jm.ExecuteNonQuery();

		var cmd = conn.CreateCommand();
		cmd.CommandText = SchemaSql;
		cmd.ExecuteNonQuery();
	}

	// *** Collection ***

	public byte[] GetOrCreateSalt(string day)
	{
		using var conn = Open();

		var ins = conn.CreateCommand();
		ins.CommandText = "INSERT OR IGNORE INTO Salts (Day, Salt) VALUES ($day, $salt);";
		ins.Parameters.AddWithValue("$day", day);
		ins.Parameters.AddWithValue("$salt", System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
		ins.ExecuteNonQuery();

		var sel = conn.CreateCommand();
		sel.CommandText = "SELECT Salt FROM Salts WHERE Day = $day;";
		sel.Parameters.AddWithValue("$day", day);
		return (byte[])sel.ExecuteScalar()!;
	}

	public string? FindRecentSession(string visitorHash, long now)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			SELECT SessionId FROM Events
			 WHERE VisitorHash = $hash AND Ts >= $since
			 ORDER BY Ts DESC LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$hash", visitorHash);
		cmd.Parameters.AddWithValue("$since", now - 1_800_000);
		return cmd.ExecuteScalar() as string;
	}

	public (string SessionId, string Path, string? Page)? GetPageviewContext(string pvId, string visitorHash)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			SELECT SessionId, Path, Page FROM Events
			 WHERE PageviewId = $pvId AND VisitorHash = $hash AND EventType = 'pageview'
			 ORDER BY Ts DESC LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$pvId", pvId);
		cmd.Parameters.AddWithValue("$hash", visitorHash);
		using var r = cmd.ExecuteReader();
		if (!r.Read()) return null;
		return (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
	}

	public void InsertEvent(TrafficEventRow e)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT INTO Events (Ts, SchemaVersion, EventType, EventName, PageviewId, VisitorHash, SessionId,
			                    Path, Page, PicSlug, Keyword, IsEntry, ReferrerHost, UtmSource, UtmMedium,
			                    UtmCampaign, Browser, Os, DeviceType, ScreenBucket, Lang, Country, DurationMs, Props)
			VALUES ($ts, $sv, $type, $name, $pv, $hash, $sid,
			        $path, $page, $pic, $kw, $entry, $ref, $us, $um,
			        $uc, $br, $os, $dev, $scr, $lang, $country, $dur, $props);
			""";
		cmd.Parameters.AddWithValue("$ts", e.Ts);
		cmd.Parameters.AddWithValue("$sv", e.SchemaVersion);
		cmd.Parameters.AddWithValue("$type", e.EventType);
		cmd.Parameters.AddWithValue("$name", Db(e.EventName));
		cmd.Parameters.AddWithValue("$pv", Db(e.PageviewId));
		cmd.Parameters.AddWithValue("$hash", e.VisitorHash);
		cmd.Parameters.AddWithValue("$sid", e.SessionId);
		cmd.Parameters.AddWithValue("$path", e.Path);
		cmd.Parameters.AddWithValue("$page", Db(e.Page));
		cmd.Parameters.AddWithValue("$pic", Db(e.PicSlug));
		cmd.Parameters.AddWithValue("$kw", Db(e.Keyword));
		cmd.Parameters.AddWithValue("$entry", e.IsEntry ? 1 : 0);
		cmd.Parameters.AddWithValue("$ref", Db(e.ReferrerHost));
		cmd.Parameters.AddWithValue("$us", Db(e.UtmSource));
		cmd.Parameters.AddWithValue("$um", Db(e.UtmMedium));
		cmd.Parameters.AddWithValue("$uc", Db(e.UtmCampaign));
		cmd.Parameters.AddWithValue("$br", Db(e.Browser));
		cmd.Parameters.AddWithValue("$os", Db(e.Os));
		cmd.Parameters.AddWithValue("$dev", Db(e.DeviceType));
		cmd.Parameters.AddWithValue("$scr", Db(e.ScreenBucket));
		cmd.Parameters.AddWithValue("$lang", Db(e.Lang));
		cmd.Parameters.AddWithValue("$country", Db(e.Country));
		cmd.Parameters.AddWithValue("$dur", e.DurationMs.HasValue ? e.DurationMs.Value : DBNull.Value);
		cmd.Parameters.AddWithValue("$props", Db(e.Props));
		cmd.ExecuteNonQuery();
	}

	public int UpdateDuration(string pvId, string visitorHash, long durationMs)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			UPDATE Events SET DurationMs = MAX(COALESCE(DurationMs, 0), $dur)
			 WHERE PageviewId = $pvId AND VisitorHash = $hash AND EventType = 'pageview';
			""";
		cmd.Parameters.AddWithValue("$dur", durationMs);
		cmd.Parameters.AddWithValue("$pvId", pvId);
		cmd.Parameters.AddWithValue("$hash", visitorHash);
		return cmd.ExecuteNonQuery();
	}

	// *** Housekeeping ***

	public int DeleteSaltsBefore(string day)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Salts WHERE Day < $day;";
		cmd.Parameters.AddWithValue("$day", day);
		return cmd.ExecuteNonQuery();
	}

	public int DeleteEventsBefore(long ts)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Events WHERE Ts < $ts;";
		cmd.Parameters.AddWithValue("$ts", ts);
		return cmd.ExecuteNonQuery();
	}

	public void Optimize()
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "PRAGMA optimize;";
		cmd.ExecuteNonQuery();
	}

	// *** Reports ***

	public TrafficStats GetStats(long from, long to)
	{
		using var conn = Open();

		var vCmd = conn.CreateCommand();
		vCmd.CommandText = """
			SELECT COUNT(DISTINCT VisitorHash) FROM Events
			 WHERE EventType = 'pageview' AND Ts >= $from AND Ts < $to;
			""";
		AddRange(vCmd, from, to);
		long visitors = Convert.ToInt64(vCmd.ExecuteScalar() ?? 0L);

		var sCmd = conn.CreateCommand();
		sCmd.CommandText = """
			WITH s AS (
			  SELECT SessionId,
			         COUNT(*)                          AS Pv,
			         MIN(Ts)                           AS StartTs,
			         MAX(Ts + COALESCE(DurationMs, 0)) AS EndTs
			  FROM Events
			  WHERE EventType = 'pageview' AND Ts >= $from AND Ts < $to
			  GROUP BY SessionId)
			SELECT COUNT(*)                                               AS Visits,
			       COALESCE(SUM(Pv), 0)                                   AS Pageviews,
			       COALESCE(AVG(CASE WHEN Pv = 1 THEN 1.0 ELSE 0 END), 0) AS BounceRate,
			       COALESCE(AVG(EndTs - StartTs), 0)                      AS AvgVisitMs
			FROM s;
			""";
		AddRange(sCmd, from, to);
		using var r = sCmd.ExecuteReader();
		r.Read();
		return new TrafficStats(visitors, r.GetInt64(0), r.GetInt64(1), r.GetDouble(2), Math.Round(r.GetDouble(3)));
	}

	// hour/day buckets: returns bucket start (UTC ms) → counts.
	public Dictionary<long, (long Visitors, long Pageviews)> GetFixedBuckets(long from, long to, long offMs, long bucketMs)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			SELECT (($ts_off + Ts) / $bucket) * $bucket - $ts_off AS T,
			       COUNT(DISTINCT VisitorHash) AS Visitors,
			       COUNT(*) AS Pageviews
			FROM Events
			WHERE EventType = 'pageview' AND Ts >= $from AND Ts < $to
			GROUP BY T ORDER BY T;
			""";
		AddRange(cmd, from, to);
		cmd.Parameters.AddWithValue("$ts_off", offMs);
		cmd.Parameters.AddWithValue("$bucket", bucketMs);

		var result = new Dictionary<long, (long, long)>();
		using var r = cmd.ExecuteReader();
		while (r.Read())
			result[r.GetInt64(0)] = (r.GetInt64(1), r.GetInt64(2));
		return result;
	}

	// month buckets: returns local 'yyyy-MM' → counts.
	public Dictionary<string, (long Visitors, long Pageviews)> GetMonthBuckets(long from, long to, long offMs)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			SELECT strftime('%Y-%m', (Ts + $ts_off) / 1000, 'unixepoch') AS M,
			       COUNT(DISTINCT VisitorHash) AS Visitors,
			       COUNT(*) AS Pageviews
			FROM Events
			WHERE EventType = 'pageview' AND Ts >= $from AND Ts < $to
			GROUP BY M ORDER BY M;
			""";
		AddRange(cmd, from, to);
		cmd.Parameters.AddWithValue("$ts_off", offMs);

		var result = new Dictionary<string, (long, long)>();
		using var r = cmd.ExecuteReader();
		while (r.Read())
			result[r.GetString(0)] = (r.GetInt64(1), r.GetInt64(2));
		return result;
	}

	// Request text never reaches SQL: dimension names map to these fixed fragments.
	private static readonly Dictionary<string, (string Key, string Extra)> Dimensions = new()
	{
		["page"]        = ("Path", ""),
		["entry"]       = ("Path", "AND IsEntry = 1"),
		["picture"]     = ("PicSlug", "AND PicSlug IS NOT NULL"),
		["keyword"]     = ("Keyword", "AND Keyword IS NOT NULL"),
		["referrer"]    = ("COALESCE(ReferrerHost, '(direct)')", "AND IsEntry = 1"),
		["utmSource"]   = ("UtmSource", "AND UtmSource IS NOT NULL"),
		["utmMedium"]   = ("UtmMedium", "AND UtmMedium IS NOT NULL"),
		["utmCampaign"] = ("UtmCampaign", "AND UtmCampaign IS NOT NULL"),
		["browser"]     = ("COALESCE(Browser, 'Unknown')", ""),
		["os"]          = ("COALESCE(Os, 'Unknown')", ""),
		["device"]      = ("COALESCE(DeviceType, 'Unknown')", ""),
		["screen"]      = ("COALESCE(ScreenBucket, 'Unknown')", ""),
		["lang"]        = ("COALESCE(Lang, 'Unknown')", ""),
		["country"]     = ("Country", "AND Country IS NOT NULL"),
	};

	public static bool IsKnownDimension(string? dimension) =>
		dimension != null && Dimensions.ContainsKey(dimension);

	public List<TrafficBreakdownRow> GetBreakdown(long from, long to, string dimension, int limit)
	{
		var (key, extra) = Dimensions[dimension];

		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = $"""
			SELECT {key} AS Key, COUNT(DISTINCT VisitorHash) AS Visitors, COUNT(*) AS Pageviews
			FROM Events
			WHERE EventType = 'pageview' AND Ts >= $from AND Ts < $to {extra}
			GROUP BY Key
			ORDER BY Visitors DESC, Pageviews DESC
			LIMIT $limit;
			""";
		AddRange(cmd, from, to);
		cmd.Parameters.AddWithValue("$limit", limit);

		var result = new List<TrafficBreakdownRow>();
		using var r = cmd.ExecuteReader();
		while (r.Read())
			result.Add(new TrafficBreakdownRow(r.IsDBNull(0) ? "" : r.GetString(0), r.GetInt64(1), r.GetInt64(2)));
		return result;
	}

	public long GetRealtimeVisitors(long now)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT COUNT(DISTINCT VisitorHash) FROM Events WHERE Ts >= $since;";
		cmd.Parameters.AddWithValue("$since", now - 300_000);
		return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
	}

	private static void AddRange(SqliteCommand cmd, long from, long to)
	{
		cmd.Parameters.AddWithValue("$from", from);
		cmd.Parameters.AddWithValue("$to", to);
	}

	private static object Db(string? s) => s is null ? DBNull.Value : s;
}
