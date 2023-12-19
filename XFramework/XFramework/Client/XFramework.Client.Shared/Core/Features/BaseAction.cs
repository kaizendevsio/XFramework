namespace XFramework.Client.Shared.Core.Features;

#pragma warning disable TW0001
public record BaseAction : IAction
{
    public Action? OnSuccess { get; set; }
    public Action? OnFailure { get; set; }
}

public record BaseAction<T> : IAction, IRequest<T>
{
    public Action? OnSuccess { get; set; }
    public Action? OnFailure { get; set; }
}