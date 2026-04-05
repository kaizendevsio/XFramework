using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Files.GetList;

public sealed class GetContentFilesValidator : AbstractValidator<GetContentFilesRequest>
{
    public GetContentFilesValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");
    }
}
