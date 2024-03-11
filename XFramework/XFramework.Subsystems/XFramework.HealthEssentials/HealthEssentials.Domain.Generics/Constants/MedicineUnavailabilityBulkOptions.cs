namespace HealthEssentials.Domain.Generics.Constants;

public static partial class HealthEssentialsConstants
{
    public class MedicineUnavailabilityBulkOptions
    {
        public static readonly Guid CancelEntireOrder = new("e7846adc-00fb-4b06-93e2-3ddfe5a62903");
        public static readonly Guid DecideForEachItem = new("f78dbaaa-2e06-4424-8cc1-a210b81ed7e1");
        public static readonly Guid RemoveUnavailableItems = new("1b790bfd-cddd-4af0-a6f0-cdd9b207b357");
        public static readonly Guid ReplaceUnavailableItemsWithRecommendedItems = new("00a0b840-93b0-4cd8-b78b-a6ca24563e45");
    }
}