using System.Xml.Linq;

public class UserFileData
{
    public string? FilePath { get; set; }
    public XDocument? Document { get; set; }
    public int CurrentPage { get; set; }
}