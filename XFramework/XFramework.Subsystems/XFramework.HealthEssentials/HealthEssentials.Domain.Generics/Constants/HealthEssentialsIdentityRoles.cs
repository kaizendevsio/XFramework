namespace HealthEssentials.Domain.Generics.Constants;

public static partial class HealthEssentialsConstants
{
    public static class IdentityRole
    {
        public static readonly Guid Doctor = new("71ff81ed-7c6b-4e76-8ff2-cec0a7b250d7");
        public static readonly Guid Pharmacy = new("a05c5ec7-e90e-4520-9b3f-43779bdf1459");
        public static readonly Guid Laboratory = new("8edf8c77-9b95-4311-aae9-3f6e50f427a0");
        public static readonly Guid Logistics = new("b3487cfa-aa60-44a9-9174-9103b0aeff6a");
        public static readonly Guid Community = new("e0d21aec-e51b-4988-887a-98f1891e2249");
        public static readonly Guid Hospital = new("60dec7f3-c573-4f45-8798-50e93f1c872d");
        public static readonly Guid Patient = new("0789ad33-da94-4c2a-8a54-dfdda2e8b7f5");
        public static readonly Guid Administrator = new("1c830881-44cf-4069-8c70-b92f9ce55aa1");
    }
}