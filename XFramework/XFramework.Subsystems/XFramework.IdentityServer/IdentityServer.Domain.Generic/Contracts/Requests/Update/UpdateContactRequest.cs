using System.Text.Json.Serialization;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Update;

public class UpdateContactRequest : RequestBase
{
    public string Value { get; set; }
    public Guid? Guid { get; set; }
}