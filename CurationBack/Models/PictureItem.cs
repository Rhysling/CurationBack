using CurationBack.Services.FiltersAttributes;

namespace CurationBack.Models;

[TypeScriptModel]
public class PictureItem : IDbItem
{
	public int Id { get; set; }
	public string FileName { get; set; } = "";
	public int Seq { get; set; }
	public int Ts { get; set; }
	public List<string> Keywords { get; set; } = [];
	public string? Description { get; set; }
	public bool IsMissing { get; set; }
	public bool IsDeleted { get; set; }
}
