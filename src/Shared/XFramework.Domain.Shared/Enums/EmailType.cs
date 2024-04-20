namespace XFramework.Domain.Shared.Enums;

[Flags]
public enum EmailType : int
{
    EmailConfirmation = 0,
    AccountRegistration = 1,
    PackagePurchaseConfirmation = 2
}