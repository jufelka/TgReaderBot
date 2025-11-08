using System.Xml.Linq;

public class UserFileData
{
    public string? FilePath { get; set; }
    public XDocument? Document { get; set; }         // for .fb2
    public List<string>? EpubParagraphs { get; set; } // for .epub
    public int CurrentPage { get; set; }
}