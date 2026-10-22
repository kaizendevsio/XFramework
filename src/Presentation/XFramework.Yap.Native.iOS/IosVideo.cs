using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using Foundation;
using UIKit;
using VideoToolbox;

namespace Yap.Native.iOS;

public sealed class IosVideo : INativeVideo
{
    private readonly UIView previewView, remoteView;
    private readonly AVCaptureSession capture = new();
    private readonly DispatchQueue cameraQueue = new("yap.native.capture");
    private readonly DispatchQueue displayQueue = new("yap.native.display");
    private readonly AVSampleBufferDisplayLayer display = new() { VideoGravity = "AVLayerVideoGravityResizeAspect" };
    private AVCaptureVideoPreviewLayer? preview;
    private AVCaptureVideoDataOutput? output;
    private CaptureDelegate? captureDelegate;
    private VTCompressionSession? encoder;
    private CMVideoFormatDescription? format;
    private byte[]? oldSps, oldPps;
    private int forceKey = 1, pendingDisplay, remoteRotation, encodingInFlight, waitingForKey = 1;
    private volatile bool stopped;
    private int rotation = 90;
    private bool rear;
    public VideoCounters Counters { get; } = new();
    public event Action<VideoPicture>? Encoded;
    public event Action<string>? Failed;
    public IosVideo(UIView preview, UIView remote)
    {
        previewView = preview; remoteView = remote; remote.Layer.AddSublayer(display);
        UIDevice.CurrentDevice.BatteryMonitoringEnabled = true;
    }
    public Task StartAsync(VideoSettings settings) => Task.Run(() =>
    {
        settings.Validate(); rear = settings.Rear;
        var discovery = AVCaptureDeviceDiscoverySession.Create(new[] { AVCaptureDeviceType.BuiltInWideAngleCamera }, AVMediaTypes.Video,
            rear ? AVCaptureDevicePosition.Back : AVCaptureDevicePosition.Front);
        var camera = discovery.Devices.FirstOrDefault() ?? throw new InvalidOperationException("Requested camera not found.");
        var selected = camera.Formats.FirstOrDefault(f => ((CMVideoFormatDescription)f.FormatDescription).Dimensions.Width == VideoSettings.Width &&
            ((CMVideoFormatDescription)f.FormatDescription).Dimensions.Height == VideoSettings.Height && f.VideoSupportedFrameRateRanges.Any(r => r.MinFrameRate <= settings.Fps && r.MaxFrameRate >= settings.Fps))
            ?? throw new InvalidOperationException($"This camera does not support native 1080p{settings.Fps}.");
        capture.BeginConfiguration();
        try
        {
        using var input = AVCaptureDeviceInput.FromDevice(camera, out var error);
        if (input is null) throw new InvalidOperationException(error?.LocalizedDescription ?? "Camera input failed.");
        capture.AddInput(input);
        if (!camera.LockForConfiguration(out error)) throw new InvalidOperationException(error?.LocalizedDescription);
        try
        {
            camera.ActiveFormat = selected;
            camera.ActiveVideoMinFrameDuration = new CMTime(1, settings.Fps);
            camera.ActiveVideoMaxFrameDuration = new CMTime(1, settings.Fps);
        }
        finally { camera.UnlockForConfiguration(); }
        encoder = VTCompressionSession.Create(VideoSettings.Width, VideoSettings.Height, CMVideoCodecType.H264, Compressed,
            new VTVideoEncoderSpecification { EnableHardwareAcceleratedVideoEncoder = true, RequireHardwareAcceleratedVideoEncoder = true });
        if (encoder is null) throw new InvalidOperationException("Hardware VideoToolbox H264 encoder is unavailable.");
        var status = encoder.SetCompressionProperties(new VTCompressionProperties
        {
            RealTime = true, AllowFrameReordering = false, AverageBitRate = settings.Bitrate,
            ExpectedFrameRate = settings.Fps, MaxKeyFrameInterval = settings.Fps,
            ProfileLevel = VTProfileLevel.H264MainAutoLevel
        });
        if (status != VTStatus.Ok) throw new InvalidOperationException($"Encoder settings: {status}");
        encoder.PrepareToEncodeFrames();
        output = new AVCaptureVideoDataOutput { AlwaysDiscardsLateVideoFrames = true };
        output.UncompressedVideoSetting = new AVVideoSettingsUncompressed { PixelFormatType = CVPixelFormatType.CV420YpCbCr8BiPlanarFullRange };
        captureDelegate = new CaptureDelegate(this);
        output.SetSampleBufferDelegate(captureDelegate, cameraQueue);
        capture.AddOutput(output);
        // Preserve camera-native landscape buffers. Orientation is metadata; no pixel rotation.
        var connection = output.ConnectionFromMediaType(new NSString("vide"));
        if (connection is not null)
        {
            if (connection.IsVideoRotationAngleSupported(0)) connection.VideoRotationAngle = 0;
            if (connection.SupportsVideoMirroring) { connection.AutomaticallyAdjustsVideoMirroring = false; connection.VideoMirrored = false; }
        }
        }
        finally { capture.CommitConfiguration(); }
        UIApplication.SharedApplication.InvokeOnMainThread(() =>
        {
            preview = new AVCaptureVideoPreviewLayer(capture) { VideoGravity = AVLayerVideoGravity.ResizeAspect };
            previewView.Layer.AddSublayer(preview); Layout();
        });
        capture.StartRunning();
    });
    private void Capture(CMSampleBuffer sample)
    {
        if (stopped || encoder is null) return;
        Counters.Capture();
        using var image = sample.GetImageBuffer(); if (image is null) return;
        if (Interlocked.Increment(ref encodingInFlight) > 2)
        { Interlocked.Decrement(ref encodingInFlight); Interlocked.Increment(ref Counters.QueueDrops); return; }
        var options = new VTEncodeFrameOptions { ForceKeyFrame = Interlocked.Exchange(ref forceKey, 0) != 0 };
        var status = encoder.EncodeFrame(image, sample.PresentationTimeStamp, sample.Duration, options.Dictionary, IntPtr.Zero, out _);
        if (status != VTStatus.Ok) { Interlocked.Decrement(ref encodingInFlight); Failed?.Invoke($"Native encode failed: {status}"); }
    }
    private void Compressed(IntPtr source, VTStatus status, VTEncodeInfoFlags flags, CMSampleBuffer? sample)
    {
        Interlocked.Decrement(ref encodingInFlight);
        if (stopped) return;
        if (status != VTStatus.Ok || sample is null) { Failed?.Invoke($"Native encoder output failed: {status}"); return; }
        try
        {
            using var block = sample.GetDataBuffer(); if (block is null) return;
            block.CopyDataBytes(0, block.DataLength, out var data); if (data is null) return;
            var nals = H264.FromAvcc(data); var key = nals.Any(n => (n[0] & 31) == 5);
            if (key)
            {
                using var description = sample.GetVideoFormatDescription();
                var sps = description?.GetH264ParameterSet(0, out _, out _, out _);
                var pps = description?.GetH264ParameterSet(1, out _, out _, out _);
                if (sps is null || pps is null) throw new InvalidDataException("Missing H264 parameter sets.");
                nals.Insert(0, pps); nals.Insert(0, sps);
            }
            Encoded?.Invoke(new(H264.AnnexB(nals), (long)(sample.PresentationTimeStamp.Seconds * 1_000_000), key, Volatile.Read(ref rotation)));
        }
        catch (Exception ex) { Failed?.Invoke(ex.Message); }
    }
    public void RequestKeyframe() => Interlocked.Exchange(ref forceKey, 1);
    public void Decode(VideoPicture picture)
    {
        if (stopped) return;
        if (Interlocked.Increment(ref pendingDisplay) > 2)
        { Interlocked.Decrement(ref pendingDisplay); Interlocked.Increment(ref Counters.QueueDrops); Volatile.Write(ref waitingForKey, 1); return; }
        displayQueue.DispatchAsync(() =>
        {
            try
            {
                if (stopped) return;
                if (Volatile.Read(ref waitingForKey) != 0 && !picture.Keyframe) return;
                var nals = H264.Nals(picture.Data);
                if (picture.Keyframe)
                {
                    var sps = nals.FirstOrDefault(n => (n[0] & 31) == 7); var pps = nals.FirstOrDefault(n => (n[0] & 31) == 8);
                    if (sps is not null && pps is not null && (oldSps is null || !sps.SequenceEqual(oldSps) || !pps.SequenceEqual(oldPps!)))
                    {
                        display.SampleBufferRenderer.Flush(); format?.Dispose();
                        format = CMVideoFormatDescription.FromH264ParameterSets(new() { sps, pps }, 4, out var error)
                            ?? throw new InvalidDataException($"H264 description: {error}");
                        oldSps = sps; oldPps = pps;
                    }
                }
                if (format is null) return;
                if (display.SampleBufferRenderer.Status == AVQueuedSampleBufferRenderingStatus.Failed)
                { display.SampleBufferRenderer.Flush(); Volatile.Write(ref waitingForKey, 1); if (!picture.Keyframe) return; }
                if (!display.SampleBufferRenderer.ReadyForMoreMediaData)
                { Interlocked.Increment(ref Counters.QueueDrops); Volatile.Write(ref waitingForKey, 1); return; }
                var bytes = H264.Avcc(nals.Where(n => (n[0] & 31) is not (7 or 8)));
                using var block = CMBlockBuffer.FromMemoryBlock(bytes, 0, CMBlockBufferFlags.AssureMemoryNow, out _);
                if (block is null) throw new InvalidOperationException("Could not allocate compressed sample.");
                var timing = new CMSampleTimingInfo { PresentationTimeStamp = new CMTime(picture.TimestampUs, 1_000_000), DecodeTimeStamp = CMTime.Invalid, Duration = CMTime.Invalid };
                using var sample = CMSampleBuffer.CreateReady(block, format, 1, new[] { timing }, new nuint[] { (nuint)bytes.Length }, out var result);
                if (sample is null) throw new InvalidDataException($"H264 sample: {result}");
                foreach (var attachment in sample.GetSampleAttachments(true)) if (attachment is not null) attachment.DisplayImmediately = true;
                if (remoteRotation != picture.Rotation) { remoteRotation = picture.Rotation; UIApplication.SharedApplication.BeginInvokeOnMainThread(Layout); }
                display.SampleBufferRenderer.Enqueue(sample); Counters.Presented();
                if (picture.Keyframe) Volatile.Write(ref waitingForKey, 0);
            }
            catch (Exception ex) { Failed?.Invoke(ex.Message); }
            finally { Interlocked.Decrement(ref pendingDisplay); }
        });
    }
    public void Layout()
    {
        var orientation = previewView.Window?.WindowScene?.InterfaceOrientation ?? UIInterfaceOrientation.Portrait;
        // AVCapture's unrotated back-camera buffer is landscape-right. Front camera
        // has the opposite landscape relationship; local preview is not mirrored.
        rotation = orientation switch
        {
            UIInterfaceOrientation.PortraitUpsideDown => 270,
            UIInterfaceOrientation.LandscapeLeft => rear ? 180 : 0,
            UIInterfaceOrientation.LandscapeRight => rear ? 0 : 180,
            _ => 90
        };
        CATransaction.Begin(); CATransaction.DisableActions = true;
        if (preview is not null)
        {
            preview.Frame = previewView.Bounds;
            if (preview.Connection is { } pc && pc.IsVideoRotationAngleSupported(rotation)) pc.VideoRotationAngle = rotation;
        }
        display.AffineTransform = CGAffineTransform.MakeIdentity();
        var bounds = remoteView.Bounds; var swap = remoteRotation is 90 or 270;
        display.Bounds = new CGRect(0, 0, swap ? bounds.Height : bounds.Width, swap ? bounds.Width : bounds.Height);
        display.Position = new CGPoint(bounds.Width / 2, bounds.Height / 2);
        display.AffineTransform = CGAffineTransform.MakeRotation((nfloat)(remoteRotation * Math.PI / 180));
        CATransaction.Commit();
    }
    public DeviceState DeviceState() => new(NSProcessInfo.ProcessInfo.ThermalState.ToString(),
        UIDevice.CurrentDevice.BatteryLevel < 0 ? null : UIDevice.CurrentDevice.BatteryLevel * 100,
        null, "VideoToolbox H264 (hardware required)", "AVSampleBufferDisplayLayer (platform managed)", true, null);
    public void Dispose()
    {
        if (stopped) return; stopped = true;
        capture.StopRunning(); output?.SetSampleBufferDelegate(null, null);
        cameraQueue.DispatchSync(() => { }); displayQueue.DispatchSync(() => { });
        encoder?.CompleteFrames(CMTime.Invalid); encoder?.Dispose(); format?.Dispose();
        UIApplication.SharedApplication.InvokeOnMainThread(() => { preview?.RemoveFromSuperLayer(); display.RemoveFromSuperLayer(); });
        preview?.Dispose(); display.Dispose(); output?.Dispose(); capture.Dispose(); captureDelegate?.Dispose();
        cameraQueue.Dispose(); displayQueue.Dispose();
    }
    private sealed class CaptureDelegate(IosVideo owner) : AVCaptureVideoDataOutputSampleBufferDelegate
    {
        public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        { try { owner.Capture(sampleBuffer); } catch (Exception ex) { owner.Failed?.Invoke(ex.Message); } finally { sampleBuffer.Dispose(); } }
    }
}
