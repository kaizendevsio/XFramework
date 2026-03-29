using Bolt.Protocol;

namespace Bolt.Media.Server;

/// <summary>
/// Server-side media processing hook. Implementations receive copies of media frames
/// for recording, transcription, AI analysis, or other server-side processing.
/// Frames are delivered via a bounded channel — processing should be non-blocking.
/// </summary>
public interface IMediaProcessor
{
    /// <summary>
    /// Filter: return true if this processor wants frames for the given call and media type.
    /// Called once per stream registration to avoid unnecessary copies.
    /// </summary>
    bool Accepts(Guid callId, MediaType mediaType);

    /// <summary>
    /// Process a media frame. Called on a background thread from the tap channel.
    /// Implementations should not block — offload heavy work to a separate pipeline.
    /// </summary>
    ValueTask ProcessFrameAsync(Guid callId, Guid streamId, ReadOnlyMemory<byte> frameData, uint timestamp, uint sequenceNumber);

    /// <summary>
    /// Notification that a call has transitioned to Active state.
    /// Use for initializing recording sessions, allocating buffers, etc.
    /// </summary>
    ValueTask OnCallStartedAsync(Guid callId);

    /// <summary>
    /// Notification that a call has ended (Ended, Rejected, or Missed).
    /// Use for finalizing recordings, flushing buffers, releasing resources.
    /// </summary>
    ValueTask OnCallEndedAsync(Guid callId);
}
