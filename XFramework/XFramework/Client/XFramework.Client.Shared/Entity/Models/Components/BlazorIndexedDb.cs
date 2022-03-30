using XFramework.Client.Shared.Core.Features.Cache;

namespace XFramework.Client.Shared.Entity.Models.Components;

public class BlazorIndexedDb : IndexedDb
{
    public BlazorIndexedDb(IJSRuntime jSRuntime, string name, int version) : base(jSRuntime, name, version) { }
    public IndexedSet<IndexedDbModel> StateCache { get; set; }
}