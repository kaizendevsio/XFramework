namespace XFramework.Client.Shared.Core.Features;

#pragma warning disable TW0001
public record StateAction : IAction
{
    public Action? OnSuccess { get; set; }
    public Action? OnFailure { get; set; }
}

public record StateAction<T> : IRequest<T>
{
    public Action? OnSuccess { get; set; }
    public Action? OnFailure { get; set; }
}