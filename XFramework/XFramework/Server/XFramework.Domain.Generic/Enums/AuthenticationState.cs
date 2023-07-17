namespace XFramework.Domain.Generic.Enums;

public enum AuthenticationState
{
    NotAuthenticated = 0,
    Locked = 1,
    Blocked = 2,
    SystemError = 3,
    PendingActivation = 4,
    UserDisabled = 5,
    SessionReused = 6,
    SessionExpired = 7,
    WrongPin = 8,
    WrongPassword = 9,
    InvalidUser = 10,
    Success = 11,
    Authenticated = 12,
    SystemMaintenance = 13,
    PendingVerification = 14,
}