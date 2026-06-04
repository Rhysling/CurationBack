using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Mvc;

namespace CurationBack.Controllers;

[Route("api/[controller]")]
[ApiController]
[AdminAuthorize]
public class DbController(UsersSqliteDb uDb, PicturesSqliteDb pDb) : ControllerBase
{
	private readonly UsersSqliteDb uDb = uDb;
	private readonly PicturesSqliteDb pDb = pDb;

	// GET: api/Db/GetBackupList(dbName)
	[HttpGet("[action]")]
	public IActionResult GetBackupList()
	{
		return Ok(uDb.BackupFileList());
	}

	// GET: api/Db/GetFile(fileName)
	[HttpGet("[action]")]
	public IActionResult GetFile(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest();

		var fo = pDb.DownloadFile(fileName);
		if (fo is null) return BadRequest();

		return File(fo.FileBytes, fo.ContentType, fo.FileName);
	}

	// POST: api/Db/Backup()
	[HttpPost("[action]")]
	public IActionResult Backup()
	{
		return Ok(pDb.BackupFile());
	}

	// POST: api/Db/Restore(string fileName)
	[HttpPost("[action]")]
	public IActionResult Restore(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest("FileName Missing");

		if (fileName.IndexOf("curation_") < 0)
			return BadRequest("Not a Backup");

		bool res = pDb.RestoreFile(fileName);

		if (!res) return BadRequest("File not Found");

		return Ok();
	}

	// POST: api/Db/Delete(string fileName)
	[HttpPost("[action]")]
	public IActionResult Delete(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest("FileName Missing");

		if (fileName.IndexOf('_') < 1)
			return BadRequest("Not a Backup");

		pDb.DeleteFile(fileName);
		return Ok();
	}
}
