namespace XFramework.Blazor.Entity.Models.Components;

public class IndexedDbModel
{
    [System.ComponentModel.DataAnnotations.Key]
    public long Id { get; set; }

    public string Key { get; set; }
    public string Value { get; set; }
    public string Signature { get; set; }
}