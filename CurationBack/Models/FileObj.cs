namespace CurationBack.Models;

public class FileObj
{
	public required byte[] FileBytes { get; set; }
	public string ContentType { get; set; } = "application/octet-stream";
	public required string FileName { get; set; }
}
