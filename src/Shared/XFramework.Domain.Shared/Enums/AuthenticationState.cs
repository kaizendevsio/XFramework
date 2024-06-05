namespace XFramework.Domain.Shared.Enums;

public enum AuthenticationState
{
    NotAuthenticated = 0,
    Locked = 1,
    Blocked = 2,
    SystemError = 3,
    PendingActivation = 4,
    SessionExpired = 5,
    WrongPin = 6,
    WrongPassword = 7,
    InvalidUser = 8,
    Authenticated = 9,
    SystemMaintenance = 10,
    PendingVerification = 11,
    Unauthorized = 12
}