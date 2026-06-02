using CurationBack.Models;
using CurationBack.Utilities;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace CurationBack.Services;

public class PicturesSqliteDb(AppSettings aps) : BaseSqliteDb(aps)
{
	public List<PictureItem> GetAll(bool includeMissing = false, bool includeDeleted = false)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		var where = new List<string>();
		if (!includeMissing) where.Add("IsMissing = 0");
		if (!includeDeleted) where.Add("IsDeleted = 0");
		cmd.CommandText = "SELECT * FROM Pictures" +
			(where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "");
		return ReadAll(cmd);
	}

	public PictureItem? GetById(int id, bool includeDeleted = false)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM Pictures WHERE Id = $id" +
			(includeDeleted ? "" : " AND IsDeleted = 0");
		cmd.Parameters.AddWithValue("$id", id);
		return ReadAll(cmd).FirstOrDefault();
	}

	public PictureItem FindBySlug(string slug)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM Pictures WHERE FileName LIKE $slug || '%' LIMIT 1";
		cmd.Parameters.AddWithValue("$slug", slug);
		return ReadAll(cmd).FirstOrDefault() ?? new PictureItem();
	}

	public int CountByFileName(string fileName)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM Pictures WHERE LOWER(FileName) = LOWER($fn)";
		cmd.Parameters.AddWithValue("$fn", fileName);
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	public PictureItem SaveItem(PictureItem item)
	{
		using var conn = Open();
		if (item.Id == 0)
		{
			var maxCmd = conn.CreateCommand();
			maxCmd.CommandText = "SELECT COALESCE(MAX(Id), 0) FROM Pictures";
			item.Id = Convert.ToInt32(maxCmd.ExecuteScalar()) + 1;
		}
		Upsert(conn, item);
		return item;
	}

	public List<PictureItem> SaveBatch(List<PictureItem> items)
	{
		if (items.Count == 0) return [];

		using var conn = Open();
		using var tx = conn.BeginTransaction();

		var statsCmd = conn.CreateCommand();
		statsCmd.CommandText = "SELECT COALESCE(MIN(Seq), 110) AS MinSeq, COALESCE(MAX(Id), 0) AS MaxId FROM Pictures";
		int minSeq, maxId;
		using (var r = statsCmd.ExecuteReader())
		{
			r.Read();
			minSeq = r.GetInt32(0);
			maxId = r.GetInt32(1);
		}

		int ic = items.Count;
		int[] itemsSeq = [.. Enumerable.Range(minSeq - 10, ic).Select(a => a - ic)];
		int i = 0;
		var result = new List<PictureItem>();

		foreach (var item in items)
		{
			var existCmd = conn.CreateCommand();
			existCmd.CommandText = "SELECT Id, Seq FROM Pictures WHERE FileName = $fn";
			existCmd.Parameters.AddWithValue("$fn", item.FileName);
			using (var r = existCmd.ExecuteReader())
			{
				if (r.Read())
				{
					item.Id = r.GetInt32(0);
					item.Seq = r.GetInt32(1);
				}
				else
				{
					if (item.Id == 0) item.Id = ++maxId;
					item.Seq = itemsSeq[i++];
				}
			}
			Upsert(conn, item);
			result.Add(item);
		}

		ReSequence(conn);
		tx.Commit();

		var seqCmd = conn.CreateCommand();
		seqCmd.CommandText = "SELECT Seq FROM Pictures WHERE Id = $id";
		var seqParam = seqCmd.Parameters.Add("$id", SqliteType.Integer);
		foreach (var item in result)
		{
			seqParam.Value = item.Id;
			item.Seq = Convert.ToInt32(seqCmd.ExecuteScalar());
		}

		return result;
	}

	public void SetDeleted(int id, bool isDeleted)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Pictures SET IsDeleted = $d WHERE Id = $id";
		cmd.Parameters.AddWithValue("$d", isDeleted ? 1 : 0);
		cmd.Parameters.AddWithValue("$id", id);
		cmd.ExecuteNonQuery();
	}

	public void SetDeleted(string fileName, bool isDeleted)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Pictures SET IsDeleted = $d WHERE FileName = $fn";
		cmd.Parameters.AddWithValue("$d", isDeleted ? 1 : 0);
		cmd.Parameters.AddWithValue("$fn", fileName);
		cmd.ExecuteNonQuery();
	}

	public void Destroy(int id)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Pictures WHERE Id = $id";
		cmd.Parameters.AddWithValue("$id", id);
		cmd.ExecuteNonQuery();
	}

	public (List<PictureItem> missing, List<PictureItem> orphans) GetAuditLists(List<string> fileNames)
	{
		var dbPics = GetAll(includeMissing: true, includeDeleted: true);
		var piFromFiles = fileNames.Select(a => new PictureItem { FileName = a });

		var master = dbPics.FullOuterJoin(piFromFiles, a => a.FileName, b => b.FileName, (a, b, k) => (a, b)).ToList();
		var missing = master.Where(t => t.a != null && t.b == null).Select(t => t.a!).ToList();
		var orphans = master.Where(t => t.a == null && t.b != null).Select(t => t.b!).ToList();

		foreach (var a in missing) a.IsMissing = true;
		foreach (var a in orphans) a.IsMissing = false;

		return (missing, orphans);
	}

	public void SyncFromFileList(List<string> fileNames)
	{
		using (var conn = Open())
		{
			var cmd = conn.CreateCommand();
			cmd.CommandText = "UPDATE Pictures SET IsMissing = 0";
			cmd.ExecuteNonQuery();
		}

		var (missing, orphans) = GetAuditLists(fileNames);
		if (missing.Count > 0) SaveBatch(missing);
		if (orphans.Count > 0) SaveBatch(orphans);
	}

	public int RemoveMissing()
	{
		using var conn = Open();
		var countCmd = conn.CreateCommand();
		countCmd.CommandText = "SELECT COUNT(*) FROM Pictures WHERE IsMissing = 1";
		int mc = Convert.ToInt32(countCmd.ExecuteScalar());

		var delCmd = conn.CreateCommand();
		delCmd.CommandText = "DELETE FROM Pictures WHERE IsMissing = 1";
		delCmd.ExecuteNonQuery();

		return mc;
	}

	private void Upsert(SqliteConnection conn, PictureItem item)
	{
		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT INTO Pictures (Id, FileName, Seq, Ts, Keywords, Description, Link, IsMissing, IsDeleted)
			VALUES ($id, $fn, $seq, $ts, $kw, $desc, $link, $miss, $del)
			ON CONFLICT(Id) DO UPDATE SET
				FileName    = excluded.FileName,
				Seq         = excluded.Seq,
				Ts          = excluded.Ts,
				Keywords    = excluded.Keywords,
				Description = excluded.Description,
				Link        = excluded.Link,
				IsMissing   = excluded.IsMissing,
				IsDeleted   = excluded.IsDeleted
			""";
		cmd.Parameters.AddWithValue("$id", item.Id);
		cmd.Parameters.AddWithValue("$fn", item.FileName);
		cmd.Parameters.AddWithValue("$seq", item.Seq);
		cmd.Parameters.AddWithValue("$ts", item.Ts);
		cmd.Parameters.AddWithValue("$kw", JsonConvert.SerializeObject(item.Keywords));
		cmd.Parameters.AddWithValue("$desc", item.Description ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$link", item.Link ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$miss", item.IsMissing ? 1 : 0);
		cmd.Parameters.AddWithValue("$del", item.IsDeleted ? 1 : 0);
		cmd.ExecuteNonQuery();
	}

	private static void ReSequence(SqliteConnection conn)
	{
		var listCmd = conn.CreateCommand();
		listCmd.CommandText = "SELECT Id FROM Pictures ORDER BY Seq";
		var ids = new List<int>();
		using (var r = listCmd.ExecuteReader())
			while (r.Read()) ids.Add(r.GetInt32(0));

		var upCmd = conn.CreateCommand();
		upCmd.CommandText = "UPDATE Pictures SET Seq = $seq WHERE Id = $id";
		var seqParam = upCmd.Parameters.Add("$seq", SqliteType.Integer);
		var idParam = upCmd.Parameters.Add("$id", SqliteType.Integer);

		int seq = 100;
		foreach (var id in ids)
		{
			seqParam.Value = seq;
			idParam.Value = id;
			upCmd.ExecuteNonQuery();
			seq += 10;
		}
	}

	private static List<PictureItem> ReadAll(SqliteCommand cmd)
	{
		var result = new List<PictureItem>();
		using var r = cmd.ExecuteReader();
		while (r.Read())
		{
			result.Add(new PictureItem
			{
				Id          = r.GetInt32(r.GetOrdinal("Id")),
				FileName    = r.GetString(r.GetOrdinal("FileName")),
				Seq         = r.GetInt32(r.GetOrdinal("Seq")),
				Ts          = r.GetInt32(r.GetOrdinal("Ts")),
				Keywords    = JsonConvert.DeserializeObject<List<string>>(r.GetString(r.GetOrdinal("Keywords"))) ?? [],
				Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
				Link        = r.IsDBNull(r.GetOrdinal("Link")) ? null : r.GetString(r.GetOrdinal("Link")),
				IsMissing   = r.GetInt32(r.GetOrdinal("IsMissing")) == 1,
				IsDeleted   = r.GetInt32(r.GetOrdinal("IsDeleted")) == 1,
			});
		}
		return result;
	}
}
