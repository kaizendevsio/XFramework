using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Views;
using Java.Nio;
using System.Collections.Concurrent;
using Yap.Native;

namespace Yap.Native.Android;

public sealed class AndroidVideo : Java.Lang.Object, INativeVideo
{
    private readonly Activity activity;
    private readonly TextureView preview, remote;
    private readonly HandlerThread thread = new("Yap native encoder");
    private readonly CancellationTokenSource lifetime = new();
    private readonly BlockingCollection<VideoPicture> incoming = new(2);
    private Handler handler = null!;
    private CameraDevice? camera;
    private CameraCaptureSession? capture;
    private MediaCodec? encoder, decoder;
    private Surface? input, previewSurface, outputSurface;
    private Task decodeTask = Task.CompletedTask;
    private byte[] parameterSets = [];
    private int sensorRotation, remoteRotation, waitKey = 1;
    private bool rear, stopped;
    private string encoderName = "", decoderName = "";
    public VideoCounters Counters { get; } = new();
    public event Action<VideoPicture>? Encoded;
    public event Action<string>? Failed;
    public AndroidVideo(Activity activity, TextureView preview, TextureView remote)
    { this.activity = activity; this.preview = preview; this.remote = remote; }

    public async Task StartAsync(VideoSettings settings)
    {
        settings.Validate(); rear = settings.Rear;
        thread.Start(); handler = new Handler(thread.Looper!);
        var manager = (CameraManager)activity.GetSystemService(Context.CameraService)!;
        var id = manager.GetCameraIdList().FirstOrDefault(id =>
            (int)(Java.Lang.Integer)manager.GetCameraCharacteristics(id).Get(CameraCharacteristics.LensFacing)! ==
            (int)(rear ? LensFacing.Back : LensFacing.Front)) ?? throw new InvalidOperationException("Requested camera not available.");
        var characteristics = manager.GetCameraCharacteristics(id);
        sensorRotation = (int)(Java.Lang.Integer)characteristics.Get(CameraCharacteristics.SensorOrientation)!;
        var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap)!;
        var size = new global::Android.Util.Size(VideoSettings.Width, VideoSettings.Height);
        var surfaceClass = Java.Lang.Class.FromType(typeof(SurfaceTexture));
        if (!(map.GetOutputSizes(surfaceClass)?.Any(s => s.Width == size.Width && s.Height == size.Height) ?? false))
            throw new InvalidOperationException("Camera does not expose 1920×1080 surface capture.");
        var minimum = map.GetOutputMinFrameDuration(surfaceClass, size);
        if (minimum > 1_000_000_000L / settings.Fps + 1_000_000)
            throw new InvalidOperationException($"Camera reports a {minimum / 1_000_000d:F1} ms minimum frame time at 1080p; requested {settings.Fps} FPS is unsupported.");
        var ranges = (global::Android.Util.Range[])characteristics.Get(CameraCharacteristics.ControlAeAvailableTargetFpsRanges)!;
        var range = ranges.Where(r => ((Java.Lang.Integer)r.Upper!).IntValue() == settings.Fps)
            .OrderByDescending(r => ((Java.Lang.Integer)r.Lower!).IntValue()).FirstOrDefault()
            ?? throw new InvalidOperationException($"This camera does not advertise a {settings.Fps} FPS recording range.");
        var codec = SelectCodec(true, settings.Fps);
        encoderName = codec.Name!;
        encoder = MediaCodec.CreateByCodecName(encoderName)!;
        using var format = MediaFormat.CreateVideoFormat("video/avc", size.Width, size.Height);
        format.SetInteger(MediaFormat.KeyColorFormat, (int)MediaCodecCapabilities.Formatsurface);
        format.SetInteger(MediaFormat.KeyBitRate, settings.Bitrate);
        format.SetInteger(MediaFormat.KeyFrameRate, settings.Fps);
        format.SetInteger(MediaFormat.KeyIFrameInterval, 1);
        format.SetInteger("max-bframes", 0);
        encoder.SetCallback(new EncoderCallback(this), handler);
        encoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        input = encoder.CreateInputSurface(); encoder.Start();
        preview.SurfaceTexture!.SetDefaultBufferSize(size.Width, size.Height);
        previewSurface = new Surface(preview.SurfaceTexture);
        outputSurface = new Surface(remote.SurfaceTexture!);
        preview.LayoutChange += LayoutChanged; remote.LayoutChange += LayoutChanged;
        UpdateTransforms();
        var opened = new TaskCompletionSource<CameraDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.OpenCamera(id, new CameraCallback(opened, this), handler);
        camera = await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var configured = new TaskCompletionSource<CameraCaptureSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.CreateCaptureSession(new[] { input!, previewSurface }, new SessionCallback(configured), handler);
        capture = await configured.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var request = camera.CreateCaptureRequest(CameraTemplate.Record);
        request.AddTarget(input!); request.AddTarget(previewSurface);
        request.Set(CaptureRequest.ControlAeTargetFpsRange!, range);
        capture.SetRepeatingRequest(request.Build(), new CaptureCallback(this), handler);
        decodeTask = Task.Run(DecodeLoop);
    }
    private static MediaCodecInfo SelectCodec(bool encode, int fps) => new MediaCodecList(MediaCodecListKind.AllCodecs)
        .GetCodecInfos().FirstOrDefault(c => c.IsEncoder == encode && c.IsHardwareAccelerated &&
            c.GetSupportedTypes().Any(t => t == "video/avc") &&
            c.GetCapabilitiesForType("video/avc")!.VideoCapabilities!.AreSizeAndRateSupported(VideoSettings.Width, VideoSettings.Height, fps))
        ?? throw new InvalidOperationException($"No hardware H264 {(encode ? "encoder" : "decoder")} supports 1080p{fps}.");
    public void RequestKeyframe()
    {
        handler.Post(() => { if (stopped || encoder is null) return; using var p = new Bundle(); p.PutInt("request-sync", 0); encoder.SetParameters(p); });
    }
    public void Decode(VideoPicture picture)
    {
        if (stopped) return;
        if (Volatile.Read(ref waitKey) != 0 && !picture.Keyframe) return;
        if (!incoming.TryAdd(picture)) { Interlocked.Increment(ref Counters.QueueDrops); Volatile.Write(ref waitKey, 1); }
        else if (picture.Keyframe) Volatile.Write(ref waitKey, 0);
    }
    private void DecodeLoop()
    {
        try
        {
            var info = new MediaCodec.BufferInfo();
            while (!lifetime.IsCancellationRequested)
            {
                if (incoming.TryTake(out var picture, 5, lifetime.Token))
                {
                    if (decoder is null)
                    {
                        var nals = H264.Nals(picture.Data); var sps = nals.FirstOrDefault(n => (n[0] & 31) == 7); var pps = nals.FirstOrDefault(n => (n[0] & 31) == 8);
                        if (sps is null || pps is null) { Volatile.Write(ref waitKey, 1); continue; }
                        decoderName = SelectCodec(false, 60).Name!;
                        decoder = MediaCodec.CreateByCodecName(decoderName)!;
                        using var format = MediaFormat.CreateVideoFormat("video/avc", VideoSettings.Width, VideoSettings.Height);
                        format.SetByteBuffer("csd-0", ByteBuffer.Wrap(H264.AnnexB(new[] { sps }))!);
                        format.SetByteBuffer("csd-1", ByteBuffer.Wrap(H264.AnnexB(new[] { pps }))!);
                        format.SetInteger("low-latency", 1);
                        decoder.Configure(format, outputSurface, null, 0);
                        decoder.SetOnFrameRenderedListener(new RenderCallback(this), handler);
                        decoder.Start();
                    }
                    if (remoteRotation != picture.Rotation) { remoteRotation = picture.Rotation; UpdateTransforms(); }
                    var index = decoder.DequeueInputBuffer(10_000);
                    if (index >= 0)
                    {
                        var buffer = decoder.GetInputBuffer(index)!; buffer.Clear();
                        if (picture.Data.Length > buffer.Remaining()) throw new InvalidDataException("Decoder input buffer too small.");
                        buffer.Put(picture.Data); decoder.QueueInputBuffer(index, 0, picture.Data.Length, picture.TimestampUs, 0);
                    }
                    else { Interlocked.Increment(ref Counters.QueueDrops); Volatile.Write(ref waitKey, 1); }
                }
                if (decoder is null) continue;
                int output;
                while ((output = decoder.DequeueOutputBuffer(info, 0)) >= 0) decoder.ReleaseOutputBuffer(output, true);
            }
        }
        catch (System.OperationCanceledException) { }
        catch (Exception ex) { Failed?.Invoke(ex.Message); }
    }
    private int Rotation => (sensorRotation + (rear ? -1 : 1) * (int)(activity.Display?.Rotation ?? SurfaceOrientation.Rotation0) * 90 + 360) % 360;
    private void LayoutChanged(object? sender, View.LayoutChangeEventArgs e) => UpdateTransforms();
    private void UpdateTransforms() => activity.RunOnUiThread(() => { Fit(preview, Rotation); Fit(remote, remoteRotation); });
    private static void Fit(TextureView view, int rotation)
    {
        var w = view.Width; var h = view.Height; if (w == 0 || h == 0) return;
        var swap = rotation is 90 or 270;
        var scale = Math.Min(w / (float)(swap ? VideoSettings.Height : VideoSettings.Width), h / (float)(swap ? VideoSettings.Width : VideoSettings.Height));
        using var matrix = new Matrix();
        matrix.SetScale(VideoSettings.Width / (float)w, VideoSettings.Height / (float)h);
        matrix.PostTranslate(-VideoSettings.Width / 2f, -VideoSettings.Height / 2f);
        matrix.PostRotate(rotation); matrix.PostScale(scale, scale); matrix.PostTranslate(w / 2f, h / 2f);
        view.SetTransform(matrix);
    }
    public DeviceState DeviceState()
    {
        var power = (PowerManager)activity.GetSystemService(Context.PowerService)!;
        using var battery = activity.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
        var level = battery?.GetIntExtra(BatteryManager.ExtraLevel, -1) ?? -1;
        var scale = battery?.GetIntExtra(BatteryManager.ExtraScale, 100) ?? 100;
        var temperature = battery?.GetIntExtra(BatteryManager.ExtraTemperature, -1) ?? -1;
        return new(power.CurrentThermalStatus.ToString(), level < 0 ? null : 100d * level / scale,
            temperature < 0 ? null : temperature / 10d, encoderName, decoderName, true, decoder is null ? null : true);
    }
    void IDisposable.Dispose()
    {
        if (stopped) return; stopped = true; lifetime.Cancel();
        capture?.Close(); camera?.Close();
        try { decodeTask.GetAwaiter().GetResult(); } catch (System.OperationCanceledException) { }
        try { encoder?.Stop(); decoder?.Stop(); } catch (Java.Lang.IllegalStateException) { }
        encoder?.Release(); decoder?.Release();
        input?.Dispose(); outputSurface?.Dispose(); previewSurface?.Dispose();
        activity.RunOnUiThread(() => { preview.LayoutChange -= LayoutChanged; remote.LayoutChange -= LayoutChanged; });
        thread.QuitSafely(); thread.Join(1000); handler?.Dispose(); thread.Dispose(); lifetime.Dispose(); incoming.Dispose();
    }
    private sealed class CameraCallback(TaskCompletionSource<CameraDevice> ready, AndroidVideo owner) : CameraDevice.StateCallback
    {
        public override void OnOpened(CameraDevice camera) { if (!ready.TrySetResult(camera) || owner.stopped) camera.Close(); }
        public override void OnDisconnected(CameraDevice camera) { camera.Close(); ready.TrySetException(new IOException("Camera disconnected.")); owner.Failed?.Invoke("Camera disconnected."); }
        public override void OnError(CameraDevice camera, CameraError error) { camera.Close(); ready.TrySetException(new IOException($"Camera: {error}")); owner.Failed?.Invoke($"Camera: {error}"); }
    }
    private sealed class SessionCallback(TaskCompletionSource<CameraCaptureSession> ready) : CameraCaptureSession.StateCallback
    {
        public override void OnConfigured(CameraCaptureSession session) => ready.TrySetResult(session);
        public override void OnConfigureFailed(CameraCaptureSession session) => ready.TrySetException(new IOException("Camera session configuration failed."));
    }
    private sealed class CaptureCallback(AndroidVideo owner) : CameraCaptureSession.CaptureCallback
    {
        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result) => owner.Counters.Capture();
    }
    private sealed class RenderCallback(AndroidVideo owner) : Java.Lang.Object, MediaCodec.IOnFrameRenderedListener
    {
        public void OnFrameRendered(MediaCodec codec, long presentationTimeUs, long nanoTime) => owner.Counters.Presented();
    }
    private sealed class EncoderCallback(AndroidVideo owner) : MediaCodec.Callback
    {
        public override void OnInputBufferAvailable(MediaCodec codec, int index) { }
        public override void OnError(MediaCodec codec, MediaCodec.CodecException e) => owner.Failed?.Invoke(e.Message ?? "Encoder failed");
        public override void OnOutputFormatChanged(MediaCodec codec, MediaFormat format)
        {
            using var output = new MemoryStream();
            foreach (var key in new[] { "csd-0", "csd-1" })
            {
                using var buffer = format.GetByteBuffer(key); if (buffer is null) continue;
                var bytes = new byte[buffer.Remaining()]; buffer.Get(bytes); output.Write(bytes);
            }
            owner.parameterSets = output.ToArray();
        }
        public override void OnOutputBufferAvailable(MediaCodec codec, int index, MediaCodec.BufferInfo info)
        {
            try
            {
                if (owner.stopped || info.Size <= 0 || (info.Flags & MediaCodecBufferFlags.CodecConfig) != 0) return;
                using var buffer = codec.GetOutputBuffer(index)!;
                buffer.Position(info.Offset); var data = new byte[info.Size]; buffer.Get(data);
                var key = (info.Flags & MediaCodecBufferFlags.KeyFrame) != 0;
                if (key) data = [.. owner.parameterSets, .. data];
                owner.Encoded?.Invoke(new(data, info.PresentationTimeUs, key, owner.Rotation));
            }
            catch (Exception ex) { owner.Failed?.Invoke(ex.Message); }
            finally
            {
                try { codec.ReleaseOutputBuffer(index, false); }
                catch (Java.Lang.IllegalStateException) when (owner.stopped) { }
            }
        }
    }
}
