﻿namespace XFramework.Domain.Generic.BusinessObjects;

public record ServiceRequestLog<TRequest, TResponse>(TRequest Request, TResponse Response);
public record ServiceRequestLog<TRequest>(TRequest Request);