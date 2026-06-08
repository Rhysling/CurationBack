using CurationBack.Models;
using CurationBack.Utilities;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace CurationBack.Services;

public class PicturesSqliteDb(AppSettings aps) : BaseSqliteDb(aps, "Pictures")
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
		cmd.CommandText = $"SELECT * FROM Pictures WHERE FileName LIKE '{CleanInput(slug)}%' LIMIT 1;";
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
		int id = Upsert(conn, item);
		item.Id = id;
		conn.Close();
		return item;
	}

	public List<PictureItem> SaveBatch(List<PictureItem> items)
	{
		List<PictureItem> result = [];
		using var conn = Open();

		foreach (var item in items)
		{
			int id = Upsert(conn, item);
			item.Id = id;
			result.Add(item);
		}

		conn.Close();
		return result;
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
		var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Pictures WHERE IsMissing = 1; SELECT changes();";
		int mc = Convert.ToInt32(cmd.ExecuteScalar());
		conn.Close();

		return mc;
	}

	public List<PictureItem> ReSequence()
	{
		var list = GetAll(includeMissing: true, includeDeleted: true).OrderBy(a => a.Seq).ToList();

		int seq = 100;
		foreach (var item in list)
		{
			item.Seq = seq;
			seq += 10;
		}

		SaveBatch(list);
		return list;
	}

	// **** Private ****
	private int Upsert(SqliteConnection conn, PictureItem item)
	{
		string txt;
		bool isNew = (item.Id == 0);

		if (isNew)
			txt = """
				INSERT INTO Pictures (FileName, Seq, Ts, Keywords, Description, Link, IsMissing, IsDeleted)
				VALUES ($fn, $seq, $ts, $kw, $desc, $link, $miss, $del)
				RETURNING id;
				""";
		else
			txt = """
				UPDATE Pictures
				SET
					FileName    = $fn,
					Seq         = $seq,
					Ts          = $ts,
					Keywords    = $kw,
					Description = $desc,
					Link        = $link,
					IsMissing   = $miss,
					IsDeleted   = $del
				WHERE
					(Id = $id)
				RETURNING id;
				""";

		var cmd = conn.CreateCommand();
		cmd.CommandText = txt;

		if (!isNew)
			cmd.Parameters.AddWithValue("$id", item.Id);

		cmd.Parameters.AddWithValue("$fn", item.FileName);
		cmd.Parameters.AddWithValue("$seq", item.Seq);
		cmd.Parameters.AddWithValue("$ts", item.Ts);
		cmd.Parameters.AddWithValue("$kw", JsonConvert.SerializeObject(item.Keywords));
		cmd.Parameters.AddWithValue("$desc", item.Description ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$link", item.Link ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$miss", item.IsMissing ? 1 : 0);
		cmd.Parameters.AddWithValue("$del", item.IsDeleted ? 1 : 0);
		return Convert.ToInt32((cmd.ExecuteScalar() ?? 0));
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
