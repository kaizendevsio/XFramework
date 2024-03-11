namespace HealthEssentials.Domain.Generics.Constants;

public static partial class HealthEssentialsConstants
{
    public class MedicineUnavailabilityOptions
    {
        public static readonly Guid CancelEntireOrder = new("58e6918e-86a0-44d3-bbba-8956faacbd89");
        public static readonly Guid RemoveItemIfUnavailable = new("18e44b7d-913f-4d01-ae0c-7239a5364647");
        public static readonly Guid ReplaceItemWithRecommendedItem = new("287112ef-6e47-4f53-b64e-37c39b186889");
    }
}