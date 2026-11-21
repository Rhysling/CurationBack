using CurationBack.Services;
using CurationBack.Services.FiltersAttributes;
using Microsoft.AspNetCore.Mvc;

namespace CurationBack.Controllers;

// Two SQLite files share the Db folder: curation.db (Pictures, Users) and traffic.db (Events, Salts).
// Live files have no "_"; backups are curation_*.db / traffic_*.db. The file name picks the DB.
[Route("api/[controller]")]
[ApiController]
[AdminAuthorize]
public class DbController(PicturesSqliteDb pDb, TrafficSqliteDb tDb, TrafficCollector collector) : ControllerBase
{
	private readonly PicturesSqliteDb pDb = pDb;
	private readonly TrafficSqliteDb tDb = tDb;
	private readonly TrafficCollector collector = collector;

	// GET: api/Db/GetBackupList
	[HttpGet("[action]")]
	public IActionResult GetBackupList()
	{
		return Ok((List<string>)[.. pDb.BackupFileList(), .. tDb.BackupFileList()]);
	}

	// GET: api/Db/GetFile(fileName)
	[HttpGet("[action]")]
	public IActionResult GetFile(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest();

		var db = DbFor(fileName);
		if (db is null) return BadRequest();

		var fo = db.DownloadFile(fileName);
		if (fo is null) return BadRequest();

		return File(fo.FileBytes, fo.ContentType, fo.FileName);
	}

	// POST: api/Db/Backup()
	// Backs up both files; returns the new backup names, comma-separated.
	[HttpPost("[action]")]
	public IActionResult Backup()
	{
		return Ok($"{pDb.BackupFile()}, {tDb.BackupFile()}");
	}

	// POST: api/Db/Restore(string fileName)
	[HttpPost("[action]")]
	public IActionResult Restore(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest("FileName Missing");

		var db = DbFor(fileName);
		if (db is null || !db.IsBackupName(fileName))
			return BadRequest("Not a Backup");

		bool res = db.RestoreFile(fileName);

		if (!res) return BadRequest("File not Found");

		if (db == tDb)
		{
			// The restored salt table may not hold today's salt anymore.
			tDb.EnsureSchema();
			collector.ResetSaltCache();
		}

		return Ok();
	}

	// POST: api/Db/Delete(string fileName)
	[HttpPost("[action]")]
	public IActionResult Delete(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return BadRequest("FileName Missing");

		var db = DbFor(fileName);
		if (db is null || !db.IsBackupName(fileName))
			return BadRequest("Not a Backup");

		db.DeleteFile(fileName);
		return Ok();
	}

	private BaseSqliteDb? DbFor(string fileName) =>
		pDb.OwnsFile(fileName) ? pDb : tDb.OwnsFile(fileName) ? tDb : null;
}
