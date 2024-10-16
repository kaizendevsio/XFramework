﻿namespace IdentityServer.Domain.Shared;


public static class IdentityConstants
{
    public static class ContactType
    {
        public static readonly Guid Phone = new("cdc88887-c7e7-415e-9d43-cc0050d523d3");
        public static readonly Guid Email = new("03f26cc1-e4c2-424f-9d5b-b22d006ae45b");
        public static readonly Guid Facebook = new("d89c4b4a-2077-44ea-958e-4327d191a14c");
        public static readonly Guid Instagram = new("4e5edd0d-5c16-4955-9323-3c6e86b54f0b");
        public static readonly Guid Twitter = new("17583df0-c1b2-47a7-875b-2d9b44f55249");
        public static readonly Guid LinkedIn = new("2fa27f70-d083-4327-b04e-74e1295cb4be");
    }
    
    public static class ContactGroup
    {
        public static readonly Guid Home = new("5d6f29ff-9779-44df-9900-40550bdf9d19");
        public static readonly Guid Personal = new("b4bda700-03c1-4a8a-bf6d-6043704cf767");
        public static readonly Guid Business = new("067b21a1-1cba-4c57-b357-43a6fab0a18b");
        public static readonly Guid Work = new("08fb17f1-f4ae-4540-b7ae-03dad680f9ea");
    }
    
    public static class VerificationType
    {
        public static readonly Guid Sms = new("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4");
        public static readonly Guid Email = new("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be");
        public static readonly Guid Kyc = new("41b5d12c-ce50-4af6-b68f-79443bd5c489");
    }
    
    public static class AddressType
    {
        public static readonly Guid Home = new("c9136227-f5dc-4147-984d-70aa855090e4");
        public static readonly Guid Personal = new("23c13259-1e24-427d-ba89-a4d2506c7464");
        public static readonly Guid Business = new("54ab2c38-be75-4572-916b-72019d676162");
        public static readonly Guid Work = new("66c8ab89-f24d-4aea-af1a-9ac6a8263575");
        public static readonly Guid Billing = new("4eec62eb-08ef-406c-9ea2-2ac2d6e0f206");
        public static readonly Guid Shipping = new("337ee33d-445f-4e6e-bc61-8709170b0ee4");
    }
    
    public static class SessionType
    {
        public static readonly Guid User = new("70b44b35-bf8e-43fc-af1a-38bdb816d51f");
        public static readonly Guid Service = new("1e3ab070-386a-410d-823f-4f225e07a69c");
        public static readonly Guid Rpc = new("d71cda39-4192-4d7b-af22-1c6c9289b913");
    }

    public static class RoleType
    {
        public static readonly Guid Admin = new("6e7b6bf5-6ad6-49fb-80b0-38e967fc35f3");
        public static readonly Guid User = new("10da7d3d-cbd3-4f53-99f9-d680e4e19092");
    }


}


