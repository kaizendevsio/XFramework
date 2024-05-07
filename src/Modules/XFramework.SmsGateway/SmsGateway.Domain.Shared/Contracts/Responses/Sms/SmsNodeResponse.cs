namespace SmsGateway.Domain.Shared.Contracts.Responses.Sms;

public class SmsNodeResponse
{
    public Guid Id { get; set; }
    public string? Recipient { get; set; }
    public string? Message { get; set; }
}