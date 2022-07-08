namespace XFramework.Client.Shared.Core.Features.Todo;

public partial class TodoState
{
    public class CreateTodo : IAction
    {
        public bool Silent { get; set; }
    }
}
