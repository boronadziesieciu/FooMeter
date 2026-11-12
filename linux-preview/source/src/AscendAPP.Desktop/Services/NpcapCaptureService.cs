using AscendAPP.Core.Protocol;
using SharpPcap;

namespace AscendAPP.Desktop.Services;

public sealed class DeviceChoice(ICaptureDevice device)
{
    public ICaptureDevice Device { get; } = device;
    public string Label => string.IsNullOrWhiteSpace(Device.Description) ? Device.Name : $"{Device.Description} [{Device.Name}]";
    public override string ToString() => Label;
}

/// <summary>Captures only after Start. All-adapter mode includes Npcap loopback.</summary>
public sealed class NpcapCaptureService(NetworkPipeline pipeline) : IDisposable
{
    private readonly List<ICaptureDevice> _devices = new();
    private readonly object _callbackGate = new();
    private bool _accept;
    public bool IsRunning => _devices.Count > 0;
    public int ActiveDevices => _devices.Count;
    public int FailedDevices { get; private set; }
    public int LowLatencyFallbacks {get;private set;}
    public string LatencyStatus=>IsRunning?(LowLatencyFallbacks==0?"Immediate delivery requested · 50 ms read timeout":$"Low-latency options unavailable on {LowLatencyFallbacks} adapter(s) · 50 ms fallback"):"Capture stopped";
    internal static DeviceConfiguration LowLatencyConfiguration(Action<string> onOptionalFailure)
    {
        var config=new DeviceConfiguration{Mode=DeviceModes.Promiscuous,ReadTimeout=50,Immediate=true,MinToCopy=0};
        config.ConfigurationFailed+=(_,e)=>{if(e is ConfigurationFailedEventArgs f){if(f.Property is nameof(DeviceConfiguration.Immediate) or nameof(DeviceConfiguration.MinToCopy))onOptionalFailure(f.Message);else throw new InvalidOperationException(f.Message);}};
        return config;
    }
    public static IReadOnlyList<DeviceChoice> Devices() => CaptureDeviceList.Instance.Select(x => new DeviceChoice(x)).ToArray();
    public void Start(DeviceChoice choice, int? tcpPort, bool allAdapters = false)
    {
        Stop(); pipeline.StartSession(); FailedDevices = 0;LowLatencyFallbacks=0;
        Exception? first = null;
        var choices = allAdapters ? Devices() : [choice];
        lock (_callbackGate) _accept = true;
        foreach (var entry in choices.DistinctBy(x => x.Device.Name))
        {
            var device = entry.Device;
            try
            {
                bool fallback=false;device.Open(LowLatencyConfiguration(_=>fallback=true));if(fallback)LowLatencyFallbacks++;
                device.Filter = tcpPort.HasValue ? $"tcp and port {tcpPort.Value}" : "tcp";
                device.OnPacketArrival += OnPacket;
                device.StartCapture();
                _devices.Add(device);
            }
            catch (Exception ex)
            {
                first ??= ex; FailedDevices++;
                try { device.OnPacketArrival -= OnPacket; device.Close(); } catch { }
            }
        }
        if (_devices.Count == 0)
        {
            lock (_callbackGate) _accept = false;
            throw new InvalidOperationException("No capture adapter could be opened. Check Npcap and its access settings. " + first?.Message, first);
        }
    }
    private void OnPacket(object sender, PacketCapture capture)
    {
        lock (_callbackGate)
        {
            if (!_accept) return;
            try
            {
                var raw = capture.GetPacket();
                pipeline.Ingest((int)raw.LinkLayerType, raw.Data, (sender as ICaptureDevice)?.Name ?? "capture");
            }
            catch { pipeline.RecordCaptureError(); }
        }
    }
    public void Stop()
    {
        lock (_callbackGate) _accept = false; // drains in-flight ingestion before a new session
        foreach (var device in _devices)
        {
            try { device.StopCapture(); } catch { }
            try { device.OnPacketArrival -= OnPacket; device.Close(); } catch { }
        }
        _devices.Clear();
    }
    public void Dispose() => Stop();
}
