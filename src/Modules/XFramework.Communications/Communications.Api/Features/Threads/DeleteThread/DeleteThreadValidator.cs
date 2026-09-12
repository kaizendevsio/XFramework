using Communications.Domain.Shared.Contracts.Requests.Threads;
using FluentValidation;

namespace Communications.Api.Features.Threads.DeleteThread;

public sealed class DeleteThreadValidator : AbstractValidator<DeleteThreadRequest>
{
    public DeleteThreadValidator() => RuleFor(x => x.ThreadId).NotEmpty();
}
