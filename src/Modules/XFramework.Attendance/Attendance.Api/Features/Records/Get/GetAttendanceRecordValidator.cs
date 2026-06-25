using FluentValidation;

namespace Attendance.Api.Features.Records.Get;

public sealed class GetAttendanceRecordValidator : AbstractValidator<GetAttendanceRecordRequest>
{
    public GetAttendanceRecordValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Attendance session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("Attendance participant ID is required");
    }
}

