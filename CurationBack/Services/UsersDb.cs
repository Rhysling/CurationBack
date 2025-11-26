using CurationBack.Models;

namespace CurationBack.Services;

public class UsersDb(AppSettings aps) : BaseDb<UserClient>(aps, "UsersDb")
{
	public override List<UserClient> GetAll(bool IsDeleted = false) => throw new NotImplementedException();

	public override UserClient? GetById(int id, bool includeDeleted = false) => throw new NotImplementedException();

	public List<UserClientRemote> GetAllRemote(bool includeDeleted = false)
	{
		var q = db.Select(a=> (UserClientRemote)a).AsQueryable();
		if (!includeDeleted)
			q = q.Where(a => !a.IsDeleted);

		return [.. q];
	}

	public UserClientRemote? GetByIdRemote(int id) => db.FirstOrDefault(a => a.Id == id && !a.IsDeleted);

	public UserClientRemote? FindByEmail(string email) => FindUcByEmail(email);

	public UserClientRemote SaveItem(UserClientRemote ucr)
	{
		UserClient uc = new()
		{
			Id = ucr.Id,
			Email  = ucr.Email,
			FullName = ucr.FullName,
			Token = ucr.Token,
			IsAdmin = ucr.IsAdmin,
			IsDisabled = ucr.IsDisabled,
			IsDeleted = ucr.IsDeleted
		};

	var ucExisting = FindUcByEmail(ucr.Email);

		if (ucExisting is not null)
			uc.PwHash = ucExisting.PwHash;
		
		return base.SaveItem(uc);
	}

	public void SavePassword(string email, string pw)
	{
		var uc = FindUcByEmail(email);

		if (uc is not null)
		{
			uc.PwHash = BCrypt.Net.BCrypt.EnhancedHashPassword(pw, 13);
			SaveFile();
		}
	}

	public void SaveToken(string email, string token)
	{
		var uc = FindUcByEmail(email);

		if (uc is not null)
		{
			uc.Token = token;
			SaveFile();
		}
	}

	public bool ValidatePassword(string email, string pw)
	{
		var uc = FindUcByEmail(email);

		if (uc is null || uc.PwHash is null)
			return false;

		return BCrypt.Net.BCrypt.EnhancedVerify(pw, uc.PwHash);
	}

	private UserClient? FindUcByEmail(string email) => db.FirstOrDefault(a => a.Email.Equals(email, StringComparison.CurrentCultureIgnoreCase));
}