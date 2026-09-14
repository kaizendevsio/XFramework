namespace Bolt.Media.Browser;

public sealed record AudioOutputDevice(string Id, string Label);
public sealed record AudioOutputs(bool Supported, string DeviceId, List<AudioOutputDevice> Devices);
