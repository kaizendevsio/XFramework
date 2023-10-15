﻿namespace XFramework.Domain.Generic.Contracts.Responses;

public record PaginatedResult<T>
(
    long TotalItems,
    int PageIndex,
    int PageSize,
    IEnumerable<T> Items)
{
    public int PageCount => (int)Math.Ceiling(TotalItems / (decimal)PageSize);
}