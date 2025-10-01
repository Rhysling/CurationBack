using CurationBack.Services.FiltersAttributes;

namespace CurationBack.Models;

[TypeScriptModel]
public class PictureItem
{
	public required string FileName { get; set; }
	public int TimeStamp { get; set; }
	public List<string> Keywords { get; set; } = [];
	public string? Description { get; set; }
	public bool IsMissing { get; set; }
	public bool IsDeleted { get; set; }
}
