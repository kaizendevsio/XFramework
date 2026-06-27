using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public interface ICommunicationsTemplateService
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
