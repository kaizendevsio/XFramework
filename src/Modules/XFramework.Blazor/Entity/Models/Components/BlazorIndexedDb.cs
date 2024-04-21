namespace XFramework.Blazor.Entity.Models.Components;

public class BlazorIndexedDb : IndexedDb
{
    public BlazorIndexedDb(IJSRuntime jSRuntime, string name, int version) : base(jSRuntime, name, version) { }
    public IndexedSet<IndexedDbModel> StateCache { get; set; }
}