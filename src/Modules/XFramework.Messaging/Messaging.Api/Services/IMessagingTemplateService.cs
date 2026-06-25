using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Messaging.Api.Services;

public interface IMessagingTemplateService
{
    Task<Result<GetMessageTemplatesResponse>> GetTemplatesAsync(
        GetMessageTemplatesRequest request,
        CancellationToken ct = default);

    Task<Result<MessageTemplateResponse>> GetTemplateAsync(
        GetMessageTemplateRequest request,
        CancellationToken ct = default);

    Task<Result<MessageTemplateResponse>> CreateTemplateAsync(
        CreateMessageTemplateRequest request,
        CancellationToken ct = default);

    Task<Result<MessageTemplateResponse>> UpdateTemplateAsync(
        UpdateMessageTemplateRequest request,
        CancellationToken ct = default);

    Task<Result<CmdResponse>> DeleteTemplateAsync(
        DeleteMessageTemplateRequest request,
        CancellationToken ct = default);

    Task<Result<MessageTemplateResponse>> CloneTemplateAsync(
        CloneMessageTemplateRequest request,
        CancellationToken ct = default);

    Task<Result<RenderMessageTemplateResponse>> RenderTemplateAsync(
        RenderMessageTemplateRequest request,
        CancellationToken ct = default);
}
