namespace POS.Domain.Shared.Contracts;

public static class PosAuthorizationCapabilities
{
    public const string RegistersView = "pos.registers:view";
    public const string RegistersCreate = "pos.registers:create";
    public const string RegistersUpdate = "pos.registers:update";
    public const string SalesView = "pos.sales:view";
    public const string SalesCreate = "pos.sales:create";
    public const string SalesUpdate = "pos.sales:update";
    public const string SalesDelete = "pos.sales:delete";
    public const string CartsView = "pos.carts:view";
    public const string CartsCreate = "pos.carts:create";
    public const string CartsUpdate = "pos.carts:update";
    public const string CartsDelete = "pos.carts:delete";
    public const string ReturnsView = "pos.returns:view";
    public const string ReturnsCreate = "pos.returns:create";
    public const string ReturnsUpdate = "pos.returns:update";
}
