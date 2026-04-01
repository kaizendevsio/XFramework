namespace Community.Domain.Shared;

public static class CommunityIdentityFileTypes
{
    public static readonly Guid ProfilePhoto = new("996dd417-170c-4ac9-b565-62caf4ab5ccf");
    public static readonly Guid CoverPhoto = new("8716ec30-b061-45cc-ad5b-77bda960d90e");
}

public static class CommunityStorageFileTypes
{
    public static readonly Guid Png = new("af6b9396-ba01-4f88-a5d0-e0cfbc038146");
}

public static class CommunityConnectionTypes
{
    public static readonly Guid Follow = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid Block = new("a0000000-0000-0000-0000-000000000002");
}
