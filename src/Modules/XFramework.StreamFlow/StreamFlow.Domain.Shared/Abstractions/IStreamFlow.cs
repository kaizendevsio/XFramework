﻿namespace StreamFlow.Domain.Shared.Abstractions;

public interface IStreamFlow
{
    Task Subscribe();
    Task TelemetryCall();
    Task Register();
    Task Push();
    Task<bool> Ping();
    Task<bool> InvokeResponse();
    Task<bool> InvokeResponseHandler();
}