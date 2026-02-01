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

}
