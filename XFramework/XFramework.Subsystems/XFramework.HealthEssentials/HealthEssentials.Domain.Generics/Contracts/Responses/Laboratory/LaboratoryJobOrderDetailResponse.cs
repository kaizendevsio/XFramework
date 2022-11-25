﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryJobOrderDetailResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Quantity { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? Guid { get; set; }

    public LaboratoryServiceResponse? LaboratoryService { get; set; }
    public LaboratoryJobOrderResponse? LaboratoryJobOrder { get; set; }
}