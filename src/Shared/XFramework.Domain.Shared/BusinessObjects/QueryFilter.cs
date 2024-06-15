using System.Text;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.BusinessObjects;

[MemoryPackable]
public partial class QueryFilter
{
    public string? PropertyName { get; set; }
    public QueryFilterOperation Operation { get; set; } // e.g., "Equals", "Contains", etc.

    [MemoryPackIgnore]
    public object? Value
    {
        get
        {
            if (InternalValue == null)
            {
                return null;
            }

            return Type switch
            {
                "System.String" => Encoding.UTF8.GetString(InternalValue),
                "System.TimeSpan" => TimeSpan.FromTicks(BitConverter.ToInt64(InternalValue, 0)),
                "System.Guid" => new Guid(InternalValue),
                "System.Int32" => BitConverter.ToInt32(InternalValue, 0),
                "System.Int64" => BitConverter.ToInt64(InternalValue, 0),
                "System.Double" => BitConverter.ToDouble(InternalValue, 0),
                "System.Single" => BitConverter.ToSingle(InternalValue, 0),
                "System.Decimal" => (decimal) BitConverter.ToDouble(InternalValue, 0),
                "System.Boolean" => BitConverter.ToBoolean(InternalValue, 0),
                "System.DateTime" => new DateTime(BitConverter.ToInt64(InternalValue, 0)),
                _ => null
            };
        } 
        set
        {
            if (value is null)
            {
                InternalValue = null;
                return;
            }

            Type = value switch
            {
                string s => "System.String",
                TimeSpan ts => "System.TimeSpan",
                Guid g => "System.Guid",
                int i => "System.Int32",
                long l => "System.Int64",
                double d => "System.Double",
                float f => "System.Single",
                decimal dec => "System.Decimal",
                bool b => "System.Boolean",
                DateTime dt => "System.DateTime",
                Enum e => Enum.GetUnderlyingType(e.GetType()).FullName,
                _ => null
            } ?? string.Empty;
                
            InternalValue = value switch
            {
                string s => Encoding.UTF8.GetBytes(s),
                TimeSpan ts => BitConverter.GetBytes(ts.Ticks),
                Guid g => g.ToByteArray(),
                int i => BitConverter.GetBytes(i),
                long l => BitConverter.GetBytes(l),
                double d => BitConverter.GetBytes(d),
                float f => BitConverter.GetBytes(f),
                decimal dec => BitConverter.GetBytes((double) dec),
                bool b => BitConverter.GetBytes(b),
                DateTime dt => BitConverter.GetBytes(dt.Ticks),
                Enum e => GetEnumBytes(e),
                _ => null
            };
        }
    }
    
    public string Type { get; set; }
    
    public byte[]? InternalValue { get; set; }

    byte[] GetEnumBytes(Enum e)
    {
        var underlyingType = Enum.GetUnderlyingType(e.GetType());
        object enumValue = Convert.ChangeType(e, underlyingType);

        var y = underlyingType switch
        {
            Type t when t == typeof(int) => BitConverter.GetBytes((int)enumValue),
            Type t when t == typeof(uint) => BitConverter.GetBytes((uint)enumValue),
            Type t when t == typeof(long) => BitConverter.GetBytes((long)enumValue),
            Type t when t == typeof(ulong) => BitConverter.GetBytes((ulong)enumValue),
            Type t when t == typeof(short) => BitConverter.GetBytes((short)enumValue),
            Type t when t == typeof(ushort) => BitConverter.GetBytes((ushort)enumValue),
            Type t when t == typeof(byte) => new[] { (byte)enumValue },
            Type t when t == typeof(sbyte) => new[] { (byte)(sbyte)enumValue },
            _ => throw new ArgumentException("Unknown enum underlying type")
        };

        return y;
    }

}