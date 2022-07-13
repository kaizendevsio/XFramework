using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create.Storage;

public class CreateFileRequest : RequestBase
{
    public string ContentPath { get; set; }
    public Guid? EntityGuid { get; set; }
    public Guid? IdentifierGuid { get; set; }
    public decimal? FileSize { get; set; }
    public DateTime? ExpireAt { get; set; }
    public string Hash { get; set; }
    public string Name { get; set; }
    public string ContentType { get; set; }
    public string BlobContainer { get; set; }
    public byte[] FileBytes { get; set; }
    public Guid? StorageFileIdentifierGuid { get; set; }
}