using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record AddProduct(Product Product) : StateAction;

    public class AddProductHandler : StateActionHandler<AddProduct>
    {
        private CartState CurrentState => Store.GetState<CartState>();

        public AddProductHandler(HandlerServices handlerServices, IStore store) : base(handlerServices, store) {}

        public override async Task Handle(AddProduct action, CancellationToken cancellationToken)
        {
            CurrentState.Products.Add(action.Product);
            await Task.CompletedTask;
        }
    }
}