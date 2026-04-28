namespace NarrativeTool.Data
{
    public interface IFolderableItem
    {
        string Id { get; set; }
        string FolderPath { get; set; }
    }
}