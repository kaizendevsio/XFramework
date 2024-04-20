namespace XFramework.Domain.Shared.Contracts.Requests;

public record FileUploadRequest
{
    public Guid? FileType { get; set; }
    public Guid Type { get; set; }
    public Guid? Identifier { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? FileExtension { get; set; }
    public string? ContentType { get; set; }
    public string? FileSize { get; set; }
    public byte[]? FileBytes { get; set; }
}