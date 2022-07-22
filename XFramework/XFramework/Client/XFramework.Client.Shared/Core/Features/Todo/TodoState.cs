using XFramework.Client.Shared.Entity.Models;

namespace XFramework.Client.Shared.Core.Features.Todo;

public partial class TodoState : State<TodoState>
{
    public override void Initialize()
    {
        
    }

    public List<SampleModel> SampleModelList { get; set; } = new();
    public SampleModel SelectedSampleModel { get; set; } = new();

    public string Text { get; set; } = string.Empty;
}