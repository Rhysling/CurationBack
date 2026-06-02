using CurationBack.Models;
using Microsoft.Data.Sqlite;

namespace CurationBack.Services;

public class UsersSqliteDb(AppSettings aps) : BaseSqliteDb(aps)
{
	public List<UserClientRemote> GetAllRemote(bool includeDeleted = true)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM Users" + (includeDeleted ? "" : " WHERE IsDeleted = 0");
		return ReadAll(cmd);
	}

	public UserClientRemote? GetByIdRemote(int id)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM Users WHERE Id = $id AND IsDeleted = 0";
		cmd.Parameters.AddWithValue("$id", id);
		return ReadAll(cmd).FirstOrDefault();
	}

	public UserClientRemote? FindByEmail(string email)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM Users WHERE LOWER(Email) = LOWER($email) AND IsDeleted = 0";
		cmd.Parameters.AddWithValue("$email", email);
		return ReadAll(cmd).FirstOrDefault();
	}

	public UserClientRemote SaveItem(UserClientRemote ucr)
	{
		using var conn = Open();

		if (ucr.Id == 0)
		{
			var maxCmd = conn.CreateCommand();
			maxCmd.CommandText = "SELECT COALESCE(MAX(Id), 0) FROM Users";
			ucr.Id = Convert.ToInt32(maxCmd.ExecuteScalar()) + 1;
		}

		string pwHash = "";
		var existCmd = conn.CreateCommand();
		existCmd.CommandText = "SELECT PwHash FROM Users WHERE Id = $id";
		existCmd.Parameters.AddWithValue("$id", ucr.Id);
		var existing = existCmd.ExecuteScalar();
		if (existing != null && existing != DBNull.Value)
			pwHash = (string)existing;

		var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT INTO Users (Id, Email, FullName, PwHash, Token, IsAdmin, HasPw, IsDisabled, IsDeleted)
			VALUES ($id, $email, $name, $pw, $token, $admin, $haspw, $dis, $del)
			ON CONFLICT(Id) DO UPDATE SET
				Email      = excluded.Email,
				FullName   = excluded.FullName,
				PwHash     = excluded.PwHash,
				Token      = excluded.Token,
				IsAdmin    = excluded.IsAdmin,
				HasPw      = excluded.HasPw,
				IsDisabled = excluded.IsDisabled,
				IsDeleted  = excluded.IsDeleted
			""";
		cmd.Parameters.AddWithValue("$id", ucr.Id);
		cmd.Parameters.AddWithValue("$email", ucr.Email);
		cmd.Parameters.AddWithValue("$name", ucr.FullName);
		cmd.Parameters.AddWithValue("$pw", pwHash);
		cmd.Parameters.AddWithValue("$token", ucr.Token ?? "");
		cmd.Parameters.AddWithValue("$admin", ucr.IsAdmin ? 1 : 0);
		cmd.Parameters.AddWithValue("$haspw", string.IsNullOrWhiteSpace(pwHash) ? 0 : 1);
		cmd.Parameters.AddWithValue("$dis", ucr.IsDisabled ? 1 : 0);
		cmd.Parameters.AddWithValue("$del", ucr.IsDeleted ? 1 : 0);
		cmd.ExecuteNonQuery();

		ucr.HasPw = !string.IsNullOrWhiteSpace(pwHash);
		return ucr;
	}

	public void SetDeleted(int id, bool isDeleted)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Users SET IsDeleted = $d WHERE Id = $id";
		cmd.Parameters.AddWithValue("$d", isDeleted ? 1 : 0);
		cmd.Parameters.AddWithValue("$id", id);
		cmd.ExecuteNonQuery();
	}

	public void Destroy(int id)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "DELETE FROM Users WHERE Id = $id";
		cmd.Parameters.AddWithValue("$id", id);
		cmd.ExecuteNonQuery();
	}

	public void SavePassword(string email, string pw)
	{
		string hash = BCrypt.Net.BCrypt.EnhancedHashPassword(pw, 13);
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Users SET PwHash = $hash, HasPw = 1 WHERE LOWER(Email) = LOWER($email)";
		cmd.Parameters.AddWithValue("$hash", hash);
		cmd.Parameters.AddWithValue("$email", email);
		cmd.ExecuteNonQuery();
	}

	public void SaveToken(string email, string token)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "UPDATE Users SET Token = $token WHERE LOWER(Email) = LOWER($email)";
		cmd.Parameters.AddWithValue("$token", token);
		cmd.Parameters.AddWithValue("$email", email);
		cmd.ExecuteNonQuery();
	}

	public bool ValidatePassword(string email, string pw)
	{
		using var conn = Open();
		var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT PwHash FROM Users WHERE LOWER(Email) = LOWER($email) AND IsDeleted = 0";
		cmd.Parameters.AddWithValue("$email", email);
		var hash = cmd.ExecuteScalar();
		if (hash == null || hash == DBNull.Value) return false;
		return BCrypt.Net.BCrypt.EnhancedVerify(pw, (string)hash);
	}

	private static List<UserClientRemote> ReadAll(SqliteCommand cmd)
	{
		var result = new List<UserClientRemote>();
		using var r = cmd.ExecuteReader();
		while (r.Read())
		{
			string pwHash = r.IsDBNull(r.GetOrdinal("PwHash")) ? "" : r.GetString(r.GetOrdinal("PwHash"));
			result.Add(new UserClientRemote
			{
				Id         = r.GetInt32(r.GetOrdinal("Id")),
				Email      = r.GetString(r.GetOrdinal("Email")),
				FullName   = r.GetString(r.GetOrdinal("FullName")),
				Token      = r.IsDBNull(r.GetOrdinal("Token")) ? "" : r.GetString(r.GetOrdinal("Token")),
				IsAdmin    = r.GetInt32(r.GetOrdinal("IsAdmin")) == 1,
				HasPw      = !string.IsNullOrWhiteSpace(pwHash),
				IsDisabled = r.GetInt32(r.GetOrdinal("IsDisabled")) == 1,
				IsDeleted  = r.GetInt32(r.GetOrdinal("IsDeleted")) == 1,
			});
		}
		return result;
	}
}
