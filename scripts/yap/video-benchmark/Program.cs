using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Bolt.Media.Browser;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Bench.App>("#app");
var host = builder.Build(); MediaBench.Js = host.Services.GetRequiredService<IJSRuntime>(); await host.RunAsync();
public static class MediaBench {
 public static IJSRuntime Js = null!;
 static BoltSFrameInterop tx = null!, rx = null!;
 static Bolt.Media.IMediaEncryption enc = null!, dec = null!;
 static Guid stream = Guid.NewGuid(); static uint seq;
 [JSInvokable] public static async Task Init(bool asyncOnly = false) {
  var call = Guid.NewGuid(); IJSRuntime runtime = asyncOnly ? new AsyncRuntime(Js) : Js; tx = new(runtime); rx = new(runtime);
  await tx.ConfigureAsync(call, "alice"); await rx.ConfigureAsync(call, "bob");
  var a = new SFrameSenderKey("alice", "1", Enumerable.Repeat((byte)1,32).ToArray());
  var b = new SFrameSenderKey("bob", "2", Enumerable.Repeat((byte)2,32).ToArray());
  await tx.InstallEpochAsync("1", new string('a',64), a, [b]); await rx.InstallEpochAsync("1", new string('a',64), b, [a]);
  await tx.ActivateEpochAsync("1", new string('a',64)); await rx.ActivateEpochAsync("1", new string('a',64));
  enc=tx.ForStream(call,"alice"); dec=rx.ForStream(call,"alice");
 }
 [JSInvokable] public static async Task End() { await tx.DisposeAsync(); await rx.DisposeAsync(); }
 [JSInvokable] public static async Task<byte[]> RoundTrip(byte[] data, uint id, uint timestamp, bool key) {
  var packets=VideoFrameFragments.Split(data,id,timestamp,key); var start=seq; var ts=(uint)((ulong)timestamp*90/1000);
  using var output=new MemoryStream(); using(var writer=new BinaryWriter(output,System.Text.Encoding.UTF8,true))
   foreach(var p in packets) { var encrypted=await enc.EncryptAsync(p,seq++,ts,stream); writer.Write(encrypted.Length); writer.Write(encrypted); }
  var echoed=await Js.InvokeAsync<byte[]>("echoPackets",output.ToArray());
  using var reader=new BinaryReader(new MemoryStream(echoed)); var assembler=new VideoFrameAssembler();
  while(reader.BaseStream.Position<reader.BaseStream.Length) { var decrypted=await dec.DecryptAsync(reader.ReadBytes(reader.ReadInt32()),start++,ts,stream); if(assembler.Add(decrypted) is {} pic) return pic.Data; }
  throw new Exception("Incomplete picture");
 }
}

sealed class AsyncRuntime(IJSRuntime inner) : IJSRuntime {
 public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name,CancellationToken.None,args);
 public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
  var value=await inner.InvokeAsync<T>(name,ct,args);return value is IJSObjectReference reference ? (T)(object)new AsyncReference(reference) : value;
 }
}
sealed class AsyncReference(IJSObjectReference inner) : IJSObjectReference {
 public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name,CancellationToken.None,args);
 public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
  var value=await inner.InvokeAsync<T>(name,ct,args);return value is IJSObjectReference reference ? (T)(object)new AsyncReference(reference) : value;
 }
 public ValueTask DisposeAsync()=>inner.DisposeAsync();
}
