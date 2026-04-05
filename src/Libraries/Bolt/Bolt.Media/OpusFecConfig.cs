using System.Buffers.Binary;

namespace Bolt.Media;

/// <summary>
/// Configuration for Opus in-band Forward Error Correction.
///
/// Opus has built-in redundancy: when enabled, each encoded packet includes
/// a lower-bitrate copy of the previous frame's data. If a packet is lost,
/// the decoder can recover an approximation from the next packet.
///
/// This costs ~5-10% extra bandwidth but provides smoother audio recovery
/// than the XOR-based external FEC (which costs 25% overhead).
///
/// Configuration is communicated via the MediaConfig extension field:
/// [1:opusFecEnabled] [1:opusFecPacketLossPercent]
///
/// - opusFecEnabled: 0 or 1
/// - opusFecPacketLossPercent: expected loss rate for the encoder (0-100).
///   Higher values = more redundancy embedded in each packet.
/// </summary>
public static class OpusFecConfig
{
    /// <summary>Size of the Opus FEC extension in bytes.</summary>
    public const int ExtensionSize = 2;

    /// <summary>Create a MediaConfig extension enabling Opus in-band FEC.</summary>
    public static byte[] CreateExtension(bool enabled, byte expectedLossPercent = 10)
    {
        return [(byte)(enabled ? 1 : 0), expectedLossPercent];
    }

    /// <summary>Parse the Opus FEC extension from a MediaConfig.</summary>
    public static (bool Enabled, byte ExpectedLossPercent) ParseExtension(ReadOnlySpan<byte> extension)
    {
        if (extension.Length < ExtensionSize)
            return (false, 0);

        return (extension[0] != 0, extension[1]);
    }
}
