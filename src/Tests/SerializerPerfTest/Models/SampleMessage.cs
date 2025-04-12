using MemoryPack;

namespace SerializerPerfTest.Models
{
    [MemoryPackable]
    public partial class SampleMessage
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsActive { get; set; }
        public int[] Values { get; set; }
        public NestedData Nested { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; }
    }

    [MemoryPackable]
    public partial class NestedData
    {
        public string Description { get; set; }
        public double Amount { get; set; }
        public List<string> Tags { get; set; }
    }
}