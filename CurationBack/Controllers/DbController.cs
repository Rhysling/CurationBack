using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Mvc;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
[AdminAuthorize]
public class DbController(UsersDb uDb, PicturesDb pDb) : ControllerBase
{
	private readonly UsersDb uDb = uDb;
	private readonly PicturesDb pDb = pDb;

	// GET: api/Db/GetBackupList(dbName)
	[HttpGet("[action]")]
	public IActionResult GetBackupList(string dbName)
	{
		if (dbName.Equals("UsersDb", StringComparison.CurrentCultureIgnoreCase))
			return Ok(uDb.BackupFileList());

		if (dbName.Equals("PicturesDb", StringComparison.CurrentCultureIgnoreCase))
			return Ok(pDb.BackupFileList());
		
		return BadRequest();
	}

	// GET: api/Db/GetFile(fileName)
	[HttpGet("[action]")]
	public IActionResult GetFile(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest();

		return Ok(pDb.DownloadFile(fileName));
	}

	// POST: api/Db/Backup(string dbName)
	[HttpPost("[action]")]
	public IActionResult Backup(string dbName)
	{
		if (dbName.Equals("UsersDb", StringComparison.CurrentCultureIgnoreCase))
			return Ok(uDb.BackupFile());

		if (dbName.Equals("PicturesDb", StringComparison.CurrentCultureIgnoreCase))
			return Ok(pDb.BackupFile());

		return BadRequest();
	}

	// POST: api/Db/Restore(string fileName)
	[HttpPost("[action]")]
	public IActionResult Restore(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest("FileName Missing");

		if (fileName.IndexOf('_') < 1)
			return BadRequest("Not a Backup");

		if (fileName.StartsWith("UsersDb", StringComparison.CurrentCultureIgnoreCase))
		{
			uDb.RestoreFile(fileName);
			return Ok();
		}

		if (fileName.StartsWith("PicturesDb", StringComparison.CurrentCultureIgnoreCase))
		{
			pDb.RestoreFile(fileName);
			return Ok();
		}

		return BadRequest();
	}

	// POST: api/Db/Delete(string fileName)
	[HttpPost("[action]")]
	public IActionResult Delete(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest("FileName Missing");

		if (fileName.IndexOf('_') < 1)
			return BadRequest("Not a Backup");

		if (fileName.StartsWith("UsersDb", StringComparison.CurrentCultureIgnoreCase))
		{
			uDb.DeleteFile(fileName);
			return Ok();
		}

		if (fileName.StartsWith("PicturesDb", StringComparison.CurrentCultureIgnoreCase))
		{ 
			pDb.DeleteFile(fileName);
			return Ok();
		}

		return BadRequest();
	}
}
