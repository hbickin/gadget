// MyGadget - Araç konsolu tarzı Windows masaüstü sistem göstergesi
// Tek dosya WinForms uygulaması. .NET 8 (net8.0-windows).

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MyGadget;

#region P/Invoke

[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
    // ---- Pencere stilleri (katmanlı/şeffaf pencere, tıklamayı geçirme, görev çubuğunda gizleme) ----
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_TOPMOST = 0x00000008;

    internal const uint ULW_ALPHA = 0x00000002;
    internal const byte AC_SRC_OVER = 0x00;
    internal const byte AC_SRC_ALPHA = 0x01;

    internal const int HTCAPTION = 0x2;
    internal const int WM_NCHITTEST = 0x84;
    internal const int WM_NCLBUTTONDOWN = 0xA1;
    internal const int WM_LBUTTONDOWN = 0x0201;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", ExactSpelling = true, SetLastError = true)]
    internal static extern int GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", ExactSpelling = true, SetLastError = true)]
    internal static extern int SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    internal static IntPtr GetWindowExStyle(IntPtr hWnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE)
            : new IntPtr(GetWindowLongPtr32(hWnd, GWL_EXSTYLE));
    }

    internal static void SetWindowExStyle(IntPtr hWnd, IntPtr style)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hWnd, GWL_EXSTYLE, style);
        else
            SetWindowLongPtr32(hWnd, GWL_EXSTYLE, style.ToInt32());
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // ---- Tek örnek: ikinci açılışta mevcut örneğin ayarlarını açtırma ----
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    internal static readonly IntPtr HWND_BROADCAST = new(0xffff);

    // ---- Tam ekran uygulama tespiti ----
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    internal const int WM_EXITSIZEMOVE = 0x0232;

    // ---- Masaüstü seviyesi (WorkerW) ----
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    internal static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    // ---- Bellek bilgisi (RAM) ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public static MEMORYSTATUSEX Create()
        {
            var m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            return m;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

#endregion

#region Ayarlar

public enum GaugeKind
{
    Cpu, Ram, CpuCoreTemp, CpuPackageTemp, ThreadUsage, DiskRead, Network, Gpu
}

public enum GaugeSize { Large, Medium, Small }

public enum AppTheme { ChromeClassic, DarkSport, Light, Retro }

public enum DockPosition { TopCenter, TopLeft, TopRight, Free }

public enum TemperatureSource { Auto, LibreHardwareMonitorWmi, LibreHardwareMonitorHttp, Acpi }

public sealed class GaugeSettings
{
    public string Id { get; set; } = "";
    public GaugeKind Kind { get; set; }
    public bool Visible { get; set; } = true;
    public int Order { get; set; }
    public double YellowThreshold { get; set; } = 70;
    public double RedThreshold { get; set; } = 85;
    public double ManualMaxMbps { get; set; } = 0; // 0 = otomatik (NetworkInterface.Speed)
}

public sealed class AppSettings
{
    // Veri
    public int UpdateIntervalMs { get; set; } = 1000;
    public int SmoothingSamples { get; set; } = 4;
    public double NeedleDamping { get; set; } = 0.22; // 0..1, yüksek = daha hızlı
    public bool PeakNeedleEnabled { get; set; } = true;
    public int PeakHoldSeconds { get; set; } = 4;
    public TemperatureSource TempSource { get; set; } = TemperatureSource.Auto;
    public int LhmHttpPort { get; set; } = 8085;
    public bool UseFahrenheit { get; set; } = false;
    public bool HideVirtualAdapters { get; set; } = true;
    public bool ShowThreadUsageMiniGauges { get; set; } = false;

    // Görünüm
    public int ScalePercent { get; set; } = 100;
    public int GaugeSpacing { get; set; } = 10;
    public AppTheme Theme { get; set; } = AppTheme.ChromeClassic;
    public int OpacityPercent { get; set; } = 92;
    public DockPosition Position { get; set; } = DockPosition.TopCenter;
    public string? MonitorDeviceName { get; set; } = null;
    public int FreeX { get; set; } = 100;
    public int FreeY { get; set; } = 0;
    public bool ShowDigitalValues { get; set; } = true;
    public bool ShowLabels { get; set; } = true;
    public bool LedsEnabled { get; set; } = true;
    public int LedYellowBlinkMs { get; set; } = 600;
    public int LedRedBlinkMs { get; set; } = 250;
    public double LedHysteresis { get; set; } = 3.0;
    public int LedDelaySeconds { get; set; } = 2;

    // Davranış
    public bool AlwaysOnTop { get; set; } = true;
    public bool DesktopLevel { get; set; } = false;
    public bool PositionLocked { get; set; } = false;
    public bool ClickThrough { get; set; } = false;
    public bool HideOnFullscreenApp { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    // Göstergeler
    public List<GaugeSettings> Gauges { get; set; } = new();

    public static AppSettings CreateDefault()
    {
        var s = new AppSettings();
        int order = 0;
        s.Gauges.Add(new GaugeSettings { Id = "cpu", Kind = GaugeKind.Cpu, Order = order++, YellowThreshold = 70, RedThreshold = 90 });
        s.Gauges.Add(new GaugeSettings { Id = "ram", Kind = GaugeKind.Ram, Order = order++, YellowThreshold = 75, RedThreshold = 90 });
        return s;
    }
}

internal static class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyGadget");

    internal static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    internal static string LogPath => Path.Combine(AppDataDir, "log.txt");

    internal static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    if (loaded.Gauges.Count == 0)
                        loaded.Gauges = AppSettings.CreateDefault().Gauges;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Ayarlar okunamadı, varsayılanlar kullanılıyor: " + ex.Message);
        }
        return AppSettings.CreateDefault();
    }

    internal static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Copy(tmp, SettingsPath, overwrite: true);
            File.Delete(tmp);
        }
        catch (Exception ex)
        {
            Logger.Log("Ayarlar kaydedilemedi: " + ex.Message);
        }
    }
}

internal static class Logger
{
    private static readonly object Lock = new();

    internal static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(SettingsManager.AppDataDir);
                File.AppendAllText(SettingsManager.LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Loglama başarısız olursa yutulur; kullanıcıya mesaj kutusu gösterilmez.
        }
    }
}

[SupportedOSPlatform("windows")]
internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MyGadget";

    internal static void Apply(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(ValueName, "\"" + exePath + "\"");
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Windows başlangıç kaydı güncellenemedi: " + ex.Message);
        }
    }
}

#endregion

#region Sensörler

public struct SensorReading
{
    public double Value;       // 0-100 gösterge yüzdesi
    public bool Available;
    public string DigitalText; // örn. "42%" veya "3.2 GB / 16 GB"

    public static SensorReading Unavailable(string text = "N/A") =>
        new() { Value = 0, Available = false, DigitalText = text };
}

[SupportedOSPlatform("windows")]
internal sealed class CpuSensor : IDisposable
{
    private PerformanceCounter? _counter;
    private bool _warned;

    internal CpuSensor()
    {
        try
        {
            if (PerformanceCounterCategory.Exists("Processor Information") &&
                CounterExists("Processor Information", "% Processor Utility"))
            {
                _counter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true);
            }
            else
            {
                _counter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            }
            _counter.NextValue(); // ilk çağrı 0 döner, ısındırma.
        }
        catch (Exception ex)
        {
            Logger.Log("CPU sayacı başlatılamadı: " + ex.Message);
            _counter = null;
        }
    }

    private static bool CounterExists(string category, string counter)
    {
        try
        {
            var cat = new PerformanceCounterCategory(category);
            return cat.CounterExists(counter);
        }
        catch { return false; }
    }

    internal SensorReading Read()
    {
        if (_counter == null) return SensorReading.Unavailable();
        try
        {
            var v = Math.Clamp(_counter.NextValue(), 0, 100);
            return new SensorReading { Value = v, Available = true, DigitalText = $"{v:0}%" };
        }
        catch (Exception ex)
        {
            if (!_warned) { Logger.Log("CPU okuma hatası: " + ex.Message); _warned = true; }
            return SensorReading.Unavailable();
        }
    }

    public void Dispose() => _counter?.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class RamSensor
{
    internal SensorReading Read()
    {
        try
        {
            var mem = NativeMethods.MEMORYSTATUSEX.Create();
            if (!NativeMethods.GlobalMemoryStatusEx(ref mem))
                return SensorReading.Unavailable();

            double usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / 1024.0 / 1024.0 / 1024.0;
            double totalGb = mem.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
            return new SensorReading
            {
                Value = mem.dwMemoryLoad,
                Available = true,
                DigitalText = $"{usedGb:0.1} / {totalGb:0.1} GB"
            };
        }
        catch (Exception ex)
        {
            Logger.Log("RAM okuma hatası: " + ex.Message);
            return SensorReading.Unavailable();
        }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class DiskSensor : IDisposable
{
    private readonly string _instance;
    private PerformanceCounter? _readTimeCounter;
    private PerformanceCounter? _readBytesCounter;
    private bool _warned;

    internal DiskSensor(string instance = "C:")
    {
        _instance = instance;
        try
        {
            if (PerformanceCounterCategory.Exists("LogicalDisk"))
            {
                _readTimeCounter = new PerformanceCounter("LogicalDisk", "% Disk Read Time", instance, true);
                _readBytesCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", instance, true);
                _readTimeCounter.NextValue();
                _readBytesCounter.NextValue();
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Disk sayacı başlatılamadı ({instance}): " + ex.Message);
            _readTimeCounter = null;
            _readBytesCounter = null;
        }
    }

    internal SensorReading Read()
    {
        if (_readTimeCounter == null || _readBytesCounter == null) return SensorReading.Unavailable();
        try
        {
            double pct = Math.Clamp(_readTimeCounter.NextValue(), 0, 100);
            double mbPerSec = _readBytesCounter.NextValue() / 1024.0 / 1024.0;
            return new SensorReading { Value = pct, Available = true, DigitalText = $"{mbPerSec:0.0} MB/s" };
        }
        catch (Exception ex)
        {
            if (!_warned) { Logger.Log("Disk okuma hatası: " + ex.Message); _warned = true; }
            return SensorReading.Unavailable();
        }
    }

    public void Dispose()
    {
        _readTimeCounter?.Dispose();
        _readBytesCounter?.Dispose();
    }
}

internal sealed class NetworkAdapterInfo
{
    public required string Id;          // Görünen ad (ayar anahtarı olarak da kullanılır)
    public required NetworkInterface Nic;
    public long LastBytesReceived;
    public long LastBytesSent;
    public DateTime LastSampleUtc;
    public bool HasSample;
}

internal sealed class NetworkSensorManager : IDisposable
{
    private readonly Dictionary<string, NetworkAdapterInfo> _adapters = new();
    private readonly object _lock = new();
    private volatile bool _dirty = true;

    internal event Action? DiscoveryChanged;

    internal NetworkSensorManager()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => _dirty = true;

    internal List<string> CurrentIds()
    {
        lock (_lock) return _adapters.Keys.ToList();
    }

    internal Dictionary<string, SensorReading> PollAll(bool hideVirtual, Dictionary<string, double> manualMaxMbps)
    {
        RefreshIfNeeded(hideVirtual);
        var result = new Dictionary<string, SensorReading>();
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            foreach (var kv in _adapters)
            {
                var info = kv.Value;
                try
                {
                    var stats = info.Nic.GetIPv4Statistics();
                    double elapsed = (now - info.LastSampleUtc).TotalSeconds;
                    if (elapsed <= 0) elapsed = 1;

                    long rx = stats.BytesReceived, tx = stats.BytesSent;
                    double downBytesPerSec = info.HasSample ? Math.Max(0, rx - info.LastBytesReceived) / elapsed : 0;
                    double upBytesPerSec = info.HasSample ? Math.Max(0, tx - info.LastBytesSent) / elapsed : 0;
                    info.LastBytesReceived = rx;
                    info.LastBytesSent = tx;
                    info.LastSampleUtc = now;
                    info.HasSample = true;

                    double speedMbps = manualMaxMbps.TryGetValue(info.Id, out var manual) && manual > 0
                        ? manual
                        : info.Nic.Speed > 0 ? info.Nic.Speed / 1_000_000.0 : 100.0;

                    double downMbps = downBytesPerSec * 8 / 1_000_000.0;
                    double upMbps = upBytesPerSec * 8 / 1_000_000.0;
                    double pct = Math.Clamp(Math.Max(downMbps, upMbps) / speedMbps * 100.0, 0, 100);

                    result[info.Id] = new SensorReading
                    {
                        Value = pct,
                        Available = true,
                        DigitalText = $"↓{FormatRate(downBytesPerSec)} ↑{FormatRate(upBytesPerSec)}"
                    };
                }
                catch (Exception ex)
                {
                    Logger.Log($"Ağ adaptörü okunamadı ({info.Id}): " + ex.Message);
                    result[info.Id] = SensorReading.Unavailable();
                }
            }
        }
        return result;
    }

    private static string FormatRate(double bytesPerSec)
    {
        double mbPerSec = bytesPerSec / 1024.0 / 1024.0;
        return mbPerSec >= 1 ? $"{mbPerSec:0.0}MB/s" : $"{bytesPerSec / 1024.0:0}KB/s";
    }

    private void RefreshIfNeeded(bool hideVirtual)
    {
        if (!_dirty) return;
        _dirty = false;
        try
        {
            var current = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ethernet3Megabit
                    or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                    or NetworkInterfaceType.Wireless80211)
                .Where(n => !hideVirtual || !LooksVirtual(n))
                .ToList();

            lock (_lock)
            {
                var currentIds = current.Select(n => n.Name).ToHashSet();
                foreach (var staleId in _adapters.Keys.Where(id => !currentIds.Contains(id)).ToList())
                    _adapters.Remove(staleId);

                foreach (var nic in current)
                {
                    if (!_adapters.ContainsKey(nic.Name))
                    {
                        _adapters[nic.Name] = new NetworkAdapterInfo { Id = nic.Name, Nic = nic, LastSampleUtc = DateTime.UtcNow };
                    }
                    else
                    {
                        _adapters[nic.Name].Nic = nic; // handle nesnesi güncellensin
                    }
                }
            }
            DiscoveryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log("Ağ adaptörleri listelenemedi: " + ex.Message);
        }
    }

    private static bool LooksVirtual(NetworkInterface nic)
    {
        string d = (nic.Description + " " + nic.Name).ToLowerInvariant();
        return d.Contains("virtual") || d.Contains("vpn") || d.Contains("hyper-v") || d.Contains("vmware")
            || d.Contains("virtualbox") || d.Contains("tap-") || d.Contains("loopback") || d.Contains("tunnel");
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
    }
}

internal sealed class GpuEngineGroup
{
    public required string Id;
    public readonly Dictionary<string, double> EngineTypeTotals = new();
}

internal sealed class GpuSensorManager : IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _counters = new();
    private readonly object _lock = new();
    private List<string> _knownGpuIds = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly Regex InstanceRegex = new(
        @"luid_0x([0-9A-Fa-f]+)_0x([0-9A-Fa-f]+).*?engtype_(\w+)$", RegexOptions.Compiled);

    internal event Action? DiscoveryChanged;
    internal bool CategoryAvailable { get; private set; } = true;
    private readonly Dictionary<string, string> _gpuNames = new();

    internal List<string> CurrentIds()
    {
        lock (_lock) return _knownGpuIds.ToList();
    }

    internal string NameFor(string id) => _gpuNames.TryGetValue(id, out var n) ? n : id.ToUpperInvariant();

    internal Dictionary<string, SensorReading> PollAll()
    {
        RefreshInstancesIfNeeded();
        if (!CategoryAvailable) return new();

        var groups = new Dictionary<string, GpuEngineGroup>();
        lock (_lock)
        {
            foreach (var kv in _counters)
            {
                var match = InstanceRegex.Match(kv.Key);
                if (!match.Success) continue;
                string gpuId = "gpu_" + match.Groups[1].Value + "_" + match.Groups[2].Value;
                string engineType = match.Groups[3].Value;

                double value;
                try { value = kv.Value.NextValue(); }
                catch { continue; }

                if (!groups.TryGetValue(gpuId, out var grp))
                {
                    grp = new GpuEngineGroup { Id = gpuId };
                    groups[gpuId] = grp;
                }
                grp.EngineTypeTotals.TryGetValue(engineType, out var existing);
                grp.EngineTypeTotals[engineType] = existing + value;
            }
        }

        var result = new Dictionary<string, SensorReading>();
        foreach (var grp in groups.Values)
        {
            double maxEngine = grp.EngineTypeTotals.Count > 0 ? grp.EngineTypeTotals.Values.Max() : 0;
            double pct = Math.Clamp(maxEngine, 0, 100);
            result[grp.Id] = new SensorReading { Value = pct, Available = true, DigitalText = $"{pct:0}%" };
        }
        return result;
    }

    private void RefreshInstancesIfNeeded()
    {
        if ((DateTime.UtcNow - _lastRefresh).TotalSeconds < 5) return;
        _lastRefresh = DateTime.UtcNow;

        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
            {
                CategoryAvailable = false;
                return;
            }
            CategoryAvailable = true;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instanceNames = category.GetInstanceNames()
                .Where(n => n.Contains("engtype_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            lock (_lock)
            {
                var toRemove = _counters.Keys.Except(instanceNames).ToList();
                foreach (var name in toRemove)
                {
                    _counters[name].Dispose();
                    _counters.Remove(name);
                }

                foreach (var name in instanceNames)
                {
                    if (_counters.ContainsKey(name)) continue;
                    try
                    {
                        var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                        counter.NextValue();
                        _counters[name] = counter;
                    }
                    catch
                    {
                        // Bazı örnekler anlık olarak kaybolabilir; görmezden gel.
                    }
                }

                var newGpuIds = _counters.Keys
                    .Select(n => InstanceRegex.Match(n))
                    .Where(m => m.Success)
                    .Select(m => "gpu_" + m.Groups[1].Value + "_" + m.Groups[2].Value)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                if (!newGpuIds.SequenceEqual(_knownGpuIds))
                {
                    _knownGpuIds = newGpuIds;
                    AssignGpuNames();
                    DiscoveryChanged?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("GPU Engine sayaçları listelenemedi: " + ex.Message);
            CategoryAvailable = false;
        }
    }

    private void AssignGpuNames()
    {
        _gpuNames.Clear();
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["Name"] is string s) names.Add(s);
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Win32_VideoController okunamadı: " + ex.Message);
        }

        for (int i = 0; i < _knownGpuIds.Count; i++)
        {
            _gpuNames[_knownGpuIds[i]] = i < names.Count ? names[i] : $"GPU {i}";
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var c in _counters.Values) c.Dispose();
            _counters.Clear();
        }
    }
}

internal static class CpuTopology
{
    internal static int GetPhysicalCoreCount()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
            int total = 0;
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["NumberOfCores"] != null)
                    total += Convert.ToInt32(mo["NumberOfCores"]);
            }
            if (total > 0) return total;
        }
        catch (Exception ex)
        {
            Logger.Log("Çekirdek sayısı WMI üzerinden okunamadı: " + ex.Message);
        }
        return Math.Max(1, Environment.ProcessorCount / 2);
    }
}

internal sealed class ThreadUsageSensorManager : IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _counters = new();
    private bool _initialized;

    internal List<string> EnsureInitialized()
    {
        if (_initialized) return _counters.Keys.ToList();
        _initialized = true;
        try
        {
            if (!PerformanceCounterCategory.Exists("Processor Information")) return new();
            var category = new PerformanceCounterCategory("Processor Information");
            string counterName = category.CounterExists("% Processor Utility") ? "% Processor Utility" : "% Processor Time";
            string fallbackCategory = counterName == "% Processor Utility" ? "Processor Information" : "Processor";

            foreach (var instance in category.GetInstanceNames().Where(n => n != "_Total").OrderBy(n => n))
            {
                try
                {
                    var counter = new PerformanceCounter(fallbackCategory, counterName, instance, true);
                    counter.NextValue();
                    _counters["thread_" + instance] = counter;
                }
                catch { /* bu çekirdek atlanır */ }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("İş parçacığı kullanım sayaçları başlatılamadı: " + ex.Message);
        }
        return _counters.Keys.ToList();
    }

    internal Dictionary<string, SensorReading> PollAll()
    {
        var result = new Dictionary<string, SensorReading>();
        foreach (var kv in _counters)
        {
            try
            {
                double v = Math.Clamp(kv.Value.NextValue(), 0, 100);
                result[kv.Key] = new SensorReading { Value = v, Available = true, DigitalText = $"{v:0}%" };
            }
            catch
            {
                result[kv.Key] = SensorReading.Unavailable();
            }
        }
        return result;
    }

    public void Dispose()
    {
        foreach (var c in _counters.Values) c.Dispose();
    }
}

internal sealed class TemperatureSensorManager : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private AppSettings _settings;
    private string _activeSourceDescription = "Henüz denenmedi";

    internal TemperatureSensorManager(AppSettings settings) => _settings = settings;

    internal void UpdateSettings(AppSettings settings) => _settings = settings;

    internal string ActiveSourceDescription => _activeSourceDescription;

    internal Dictionary<string, SensorReading> Poll(int coreCount)
    {
        var readings = TryLibreHardwareMonitorWmi(coreCount);
        if (readings == null && (_settings.TempSource == TemperatureSource.Auto || _settings.TempSource == TemperatureSource.LibreHardwareMonitorHttp))
            readings = TryLibreHardwareMonitorHttp(coreCount);
        if (readings == null && (_settings.TempSource == TemperatureSource.Auto || _settings.TempSource == TemperatureSource.Acpi))
            readings = TryAcpi(coreCount);

        if (readings != null) return ApplyUnit(readings);

        _activeSourceDescription = "Yok (LibreHardwareMonitor'ü çalıştırın)";
        var fallback = new Dictionary<string, SensorReading>();
        for (int i = 0; i < coreCount; i++) fallback[$"cpu_core_{i}"] = SensorReading.Unavailable();
        fallback["cpu_package"] = SensorReading.Unavailable();
        return fallback;
    }

    private Dictionary<string, SensorReading>? TryLibreHardwareMonitorWmi(int coreCount)
    {
        if (_settings.TempSource != TemperatureSource.Auto && _settings.TempSource != TemperatureSource.LibreHardwareMonitorWmi)
            return null;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\LibreHardwareMonitor",
                "SELECT Name, Identifier, Value FROM Sensor WHERE SensorType='Temperature'");
            var items = searcher.Get().Cast<ManagementObject>()
                .Where(mo => ((string?)mo["Identifier"])?.Contains("cpu", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            if (items.Count == 0) return null;

            var result = new Dictionary<string, SensorReading>();
            foreach (var mo in items)
            {
                string name = (string?)mo["Name"] ?? "";
                float value = Convert.ToSingle(mo["Value"]);
                ClassifyCpuSensor(name, value, result);
            }
            if (result.Count == 0) return null;
            _activeSourceDescription = "LibreHardwareMonitor (WMI)";
            return FillMissingCores(result, coreCount);
        }
        catch (Exception ex)
        {
            Logger.Log("LibreHardwareMonitor WMI okunamadı: " + ex.Message);
            return null;
        }
    }

    private Dictionary<string, SensorReading>? TryLibreHardwareMonitorHttp(int coreCount)
    {
        try
        {
            var url = $"http://localhost:{_settings.LhmHttpPort}/data.json";
            var json = _http.GetStringAsync(url).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);

            var result = new Dictionary<string, SensorReading>();
            CollectTemperatureNodes(doc.RootElement, result);
            if (result.Count == 0) return null;
            _activeSourceDescription = "LibreHardwareMonitor (HTTP)";
            return FillMissingCores(result, coreCount);
        }
        catch (Exception ex)
        {
            Logger.Log("LibreHardwareMonitor HTTP okunamadı: " + ex.Message);
            return null;
        }
    }

    private void CollectTemperatureNodes(JsonElement node, Dictionary<string, SensorReading> result)
    {
        if (node.TryGetProperty("Text", out var textEl) && node.TryGetProperty("Value", out var valueEl))
        {
            string text = textEl.GetString() ?? "";
            string valueStr = valueEl.GetString() ?? "";
            var m = Regex.Match(valueStr, @"-?\d+[.,]?\d*");
            if (m.Success && valueStr.Contains('°'))
            {
                if (float.TryParse(m.Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var val))
                    ClassifyCpuSensor(text, val, result);
            }
        }
        if (node.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
                CollectTemperatureNodes(child, result);
        }
    }

    private Dictionary<string, SensorReading>? TryAcpi(int coreCount)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject mo in searcher.Get())
            {
                double raw = Convert.ToDouble(mo["CurrentTemperature"]);
                double celsius = raw / 10.0 - 273.15;
                _activeSourceDescription = "ACPI (MSAcpi_ThermalZoneTemperature)";
                var result = new Dictionary<string, SensorReading>
                {
                    ["cpu_package"] = new SensorReading { Value = celsius, Available = true, DigitalText = $"{celsius:0.0}°" }
                };
                for (int i = 0; i < coreCount; i++) result[$"cpu_core_{i}"] = SensorReading.Unavailable();
                return result;
            }
        }
        catch (Exception ex)
        {
            Logger.Log("ACPI sıcaklık bölgesi okunamadı (yönetici yetkisi gerekebilir): " + ex.Message);
        }
        return null;
    }

    private static void ClassifyCpuSensor(string name, float celsius, Dictionary<string, SensorReading> result)
    {
        var reading = new SensorReading { Value = celsius, Available = true, DigitalText = $"{celsius:0.0}°" };
        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase) || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Die", StringComparison.OrdinalIgnoreCase))
        {
            result["cpu_package"] = reading;
            return;
        }
        var m = Regex.Match(name, @"Core\s*#?(\d+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            int idx = int.Parse(m.Groups[1].Value) - 1;
            if (idx < 0) idx = 0;
            result[$"cpu_core_{idx}"] = reading;
        }
    }

    private static Dictionary<string, SensorReading> FillMissingCores(Dictionary<string, SensorReading> result, int coreCount)
    {
        for (int i = 0; i < coreCount; i++)
            result.TryAdd($"cpu_core_{i}", SensorReading.Unavailable());
        result.TryAdd("cpu_package", SensorReading.Unavailable());
        return result;
    }

    private Dictionary<string, SensorReading> ApplyUnit(Dictionary<string, SensorReading> readings)
    {
        if (!_settings.UseFahrenheit) return readings;
        var converted = new Dictionary<string, SensorReading>();
        foreach (var kv in readings)
        {
            if (!kv.Value.Available) { converted[kv.Key] = kv.Value; continue; }
            double f = kv.Value.Value * 9.0 / 5.0 + 32.0;
            converted[kv.Key] = new SensorReading { Value = f, Available = true, DigitalText = $"{f:0.0}°F" };
        }
        return converted;
    }

    public void Dispose() => _http.Dispose();
}

#endregion

#region Gösterge Modeli ve Çizimi

public sealed class LedState
{
    private double _overThresholdSince = -1;

    public bool Active;
    public bool IsRed;

    public void Update(double value, double yellow, double red, double hysteresis, double delaySeconds, double nowSeconds)
    {
        bool overYellow = value >= yellow - (Active ? hysteresis : 0);
        bool overRed = value >= red - (Active && IsRed ? hysteresis : 0);

        if (!overYellow)
        {
            Active = false;
            IsRed = false;
            _overThresholdSince = -1;
            return;
        }

        if (_overThresholdSince < 0) _overThresholdSince = nowSeconds;

        if (nowSeconds - _overThresholdSince >= delaySeconds)
        {
            Active = true;
            IsRed = overRed;
        }
    }
}

public sealed class GaugeModel
{
    public required string Id;
    public required GaugeKind Kind;
    public required string Label;
    public GaugeSize Size = GaugeSize.Medium;
    public double TargetValue;      // sensörden gelen ham değer (0-100)
    public double DisplayValue;     // ibrenin gösterdiği sönümlenmiş değer
    public double PeakValue;
    public DateTime PeakSetAt;
    public bool Available = true;
    public string DigitalText = "N/A";
    public double YellowThreshold = 70;
    public double RedThreshold = 85;
    public readonly LedState Led = new();
    public readonly Queue<double> RecentSamples = new();

    public void PushSample(SensorReading reading, int smoothingSamples)
    {
        Available = reading.Available;
        DigitalText = reading.DigitalText;
        if (!reading.Available) return;

        RecentSamples.Enqueue(reading.Value);
        while (RecentSamples.Count > Math.Max(1, smoothingSamples)) RecentSamples.Dequeue();
        TargetValue = RecentSamples.Average();

        if (TargetValue >= PeakValue)
        {
            PeakValue = TargetValue;
            PeakSetAt = DateTime.UtcNow;
        }
    }

    public void DecayPeak(int holdSeconds)
    {
        if ((DateTime.UtcNow - PeakSetAt).TotalSeconds > holdSeconds)
        {
            PeakValue = Math.Max(TargetValue, PeakValue - 1.2);
        }
    }

    public bool AnimateTowardTarget(double damping)
    {
        double diff = TargetValue - DisplayValue;
        if (Math.Abs(diff) < 0.05)
        {
            DisplayValue = TargetValue;
            return false;
        }
        DisplayValue += diff * Math.Clamp(damping, 0.01, 1.0);
        return true;
    }
}

internal static class GaugeTheme
{
    internal static Color BezelOuter(AppTheme t) => t switch
    {
        AppTheme.DarkSport => Color.FromArgb(40, 40, 42),
        AppTheme.Light => Color.FromArgb(220, 220, 224),
        AppTheme.Retro => Color.FromArgb(70, 55, 30),
        _ => Color.FromArgb(205, 208, 212), // Krom
    };

    internal static Color BezelInner(AppTheme t) => t switch
    {
        AppTheme.DarkSport => Color.FromArgb(10, 10, 10),
        AppTheme.Light => Color.FromArgb(250, 250, 250),
        AppTheme.Retro => Color.FromArgb(30, 22, 10),
        _ => Color.FromArgb(120, 123, 128),
    };

    internal static Color Face(AppTheme t) => t switch
    {
        AppTheme.DarkSport => Color.FromArgb(18, 18, 20),
        AppTheme.Light => Color.FromArgb(245, 245, 248),
        AppTheme.Retro => Color.FromArgb(222, 197, 138),
        _ => Color.FromArgb(28, 30, 34),
    };

    internal static Color TickColor(AppTheme t) => t switch
    {
        AppTheme.Light => Color.FromArgb(40, 40, 40),
        AppTheme.Retro => Color.FromArgb(60, 42, 20),
        _ => Color.FromArgb(225, 225, 230),
    };

    internal static Color NeedleColor(AppTheme t) => t switch
    {
        AppTheme.Retro => Color.FromArgb(150, 20, 20),
        _ => Color.FromArgb(235, 30, 30),
    };
}

internal static class GaugePainter
{
    private const float StartAngle = 135f; // derece, 0 saat 3 pozisyonundan itibaren
    private const float SweepAngle = 270f;

    internal static void DrawGauge(Graphics g, RectangleF bounds, GaugeModel model, AppSettings settings)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        float dialSize = Math.Min(bounds.Width, bounds.Height * 0.78f);
        var dialRect = new RectangleF(bounds.X + (bounds.Width - dialSize) / 2f, bounds.Y, dialSize, dialSize);

        DrawBezel(g, dialRect, settings.Theme);

        var faceRect = Inflate(dialRect, -dialSize * 0.09f);
        DrawFace(g, faceRect, settings.Theme);

        DrawThresholdArcs(g, faceRect, model, settings.Theme);
        DrawTicks(g, faceRect, settings.Theme);

        if (model.Available)
        {
            DrawNeedle(g, faceRect, ValueToAngle(model.DisplayValue), GaugeTheme.NeedleColor(settings.Theme), 1.0f);
            if (settings.PeakNeedleEnabled && model.PeakValue > model.DisplayValue + 1)
                DrawNeedle(g, faceRect, ValueToAngle(model.PeakValue), Color.FromArgb(200, 255, 60, 60), 0.55f);
        }
        else
        {
            using var f = new Font("Segoe UI", faceRect.Width * 0.11f, FontStyle.Bold);
            var sz = g.MeasureString("N/A", f);
            g.DrawString("N/A", f, Brushes.Gray, faceRect.X + (faceRect.Width - sz.Width) / 2,
                faceRect.Y + (faceRect.Height - sz.Height) / 2);
        }

        DrawHub(g, faceRect, settings.Theme);

        if (settings.ShowLabels)
            DrawLabel(g, bounds, dialRect, model.Label, settings.Theme);

        if (settings.ShowDigitalValues)
            DrawDigital(g, bounds, dialRect, model.DigitalText, settings.Theme);
    }

    private static RectangleF Inflate(RectangleF r, float amount)
    {
        var copy = r;
        copy.Inflate(amount, amount);
        return copy;
    }

    private static void DrawBezel(Graphics g, RectangleF rect, AppTheme theme)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(rect);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = GaugeTheme.BezelInner(theme),
            SurroundColors = new[] { GaugeTheme.BezelOuter(theme) },
            CenterPoint = new PointF(rect.X + rect.Width * 0.35f, rect.Y + rect.Height * 0.35f)
        };
        g.FillEllipse(brush, rect);
        using var pen = new Pen(Color.FromArgb(70, 0, 0, 0), Math.Max(1f, rect.Width * 0.01f));
        g.DrawEllipse(pen, rect);
    }

    private static void DrawFace(Graphics g, RectangleF rect, AppTheme theme)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(rect);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Lighten(GaugeTheme.Face(theme), 12),
            SurroundColors = new[] { GaugeTheme.Face(theme) },
            CenterPoint = new PointF(rect.X + rect.Width * 0.42f, rect.Y + rect.Height * 0.38f)
        };
        g.FillEllipse(brush, rect);
    }

    private static Color Lighten(Color c, int amount) => Color.FromArgb(c.A,
        Math.Min(255, c.R + amount), Math.Min(255, c.G + amount), Math.Min(255, c.B + amount));

    private static void DrawThresholdArcs(Graphics g, RectangleF faceRect, GaugeModel model, AppTheme theme)
    {
        var arcRect = Inflate(faceRect, -faceRect.Width * 0.08f);
        float penWidth = Math.Max(2f, faceRect.Width * 0.045f);
        using var pen = new Pen(Color.FromArgb(160, 60, 200, 90), penWidth);

        float yellowAngle = ValueToSweepFraction(model.YellowThreshold) * SweepAngle;
        float redAngle = ValueToSweepFraction(model.RedThreshold) * SweepAngle;

        pen.Color = Color.FromArgb(160, 60, 200, 90);
        g.DrawArc(pen, arcRect, StartAngle, yellowAngle);

        pen.Color = Color.FromArgb(170, 230, 200, 40);
        g.DrawArc(pen, arcRect, StartAngle + yellowAngle, redAngle - yellowAngle);

        pen.Color = Color.FromArgb(180, 220, 50, 50);
        g.DrawArc(pen, arcRect, StartAngle + redAngle, SweepAngle - redAngle);
    }

    private static void DrawTicks(Graphics g, RectangleF faceRect, AppTheme theme)
    {
        var center = new PointF(faceRect.X + faceRect.Width / 2f, faceRect.Y + faceRect.Height / 2f);
        float outerR = faceRect.Width / 2f * 0.80f;
        float innerRMajor = outerR * 0.82f;
        float innerRMinor = outerR * 0.90f;
        using var majorPen = new Pen(GaugeTheme.TickColor(theme), Math.Max(1.2f, faceRect.Width * 0.012f));
        using var minorPen = new Pen(GaugeTheme.TickColor(theme), Math.Max(1f, faceRect.Width * 0.006f));
        using var font = new Font("Segoe UI", faceRect.Width * 0.065f, FontStyle.Bold);
        using var textBrush = new SolidBrush(GaugeTheme.TickColor(theme));

        for (int v = 0; v <= 100; v += 10)
        {
            double angleDeg = StartAngle + ValueToSweepFraction(v) * SweepAngle;
            double rad = angleDeg * Math.PI / 180.0;
            bool major = v % 20 == 0;
            float ir = major ? innerRMajor : innerRMinor;
            var p1 = new PointF(center.X + (float)(outerR * Math.Cos(rad)), center.Y + (float)(outerR * Math.Sin(rad)));
            var p2 = new PointF(center.X + (float)(ir * Math.Cos(rad)), center.Y + (float)(ir * Math.Sin(rad)));
            g.DrawLine(major ? majorPen : minorPen, p1, p2);

            if (major)
            {
                float labelR = outerR * 1.0f + faceRect.Width * 0.04f;
                var lp = new PointF(center.X + (float)(labelR * Math.Cos(rad)), center.Y + (float)(labelR * Math.Sin(rad)));
                var size = g.MeasureString(v.ToString(), font);
                g.DrawString(v.ToString(), font, textBrush, lp.X - size.Width / 2, lp.Y - size.Height / 2);
            }
        }
    }

    private static void DrawNeedle(Graphics g, RectangleF faceRect, double angleDeg, Color color, float lengthFactor)
    {
        var center = new PointF(faceRect.X + faceRect.Width / 2f, faceRect.Y + faceRect.Height / 2f);
        float len = faceRect.Width / 2f * 0.72f * lengthFactor;
        double rad = angleDeg * Math.PI / 180.0;
        var tip = new PointF(center.X + (float)(len * Math.Cos(rad)), center.Y + (float)(len * Math.Sin(rad)));
        var tailRad = rad + Math.PI;
        var tail = new PointF(center.X + (float)(faceRect.Width * 0.06 * Math.Cos(tailRad)),
            center.Y + (float)(faceRect.Width * 0.06 * Math.Sin(tailRad)));

        using var path = new GraphicsPath();
        double perp = rad + Math.PI / 2;
        float w = faceRect.Width * 0.018f;
        var b1 = new PointF(tail.X + (float)(w * Math.Cos(perp)), tail.Y + (float)(w * Math.Sin(perp)));
        var b2 = new PointF(tail.X - (float)(w * Math.Cos(perp)), tail.Y - (float)(w * Math.Sin(perp)));
        path.AddPolygon(new[] { b1, tip, b2 });

        using var shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
        var shadowPath = (GraphicsPath)path.Clone();
        var m = new Matrix();
        m.Translate(faceRect.Width * 0.012f, faceRect.Width * 0.012f);
        shadowPath.Transform(m);
        g.FillPath(shadowBrush, shadowPath);
        shadowPath.Dispose();

        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawHub(Graphics g, RectangleF faceRect, AppTheme theme)
    {
        var center = new PointF(faceRect.X + faceRect.Width / 2f, faceRect.Y + faceRect.Height / 2f);
        float r = faceRect.Width * 0.055f;
        var hubRect = new RectangleF(center.X - r, center.Y - r, r * 2, r * 2);
        using var path = new GraphicsPath();
        path.AddEllipse(hubRect);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(230, 230, 235),
            SurroundColors = new[] { Color.FromArgb(90, 90, 95) },
            CenterPoint = new PointF(center.X - r * 0.3f, center.Y - r * 0.3f)
        };
        g.FillEllipse(brush, hubRect);
    }

    private static void DrawLabel(Graphics g, RectangleF bounds, RectangleF dialRect, string label, AppTheme theme)
    {
        using var font = new Font("Segoe UI", bounds.Width * 0.09f, FontStyle.Bold);
        using var brush = new SolidBrush(theme == AppTheme.Light ? Color.FromArgb(40, 40, 40) : Color.White);
        var size = g.MeasureString(label, font);
        float y = dialRect.Bottom + bounds.Height * 0.02f;
        g.DrawString(label, font, brush, bounds.X + (bounds.Width - size.Width) / 2, y);
    }

    private static void DrawDigital(Graphics g, RectangleF bounds, RectangleF dialRect, string text, AppTheme theme)
    {
        using var font = new Font("Consolas", bounds.Width * 0.085f, FontStyle.Regular);
        using var brush = new SolidBrush(theme == AppTheme.Light ? Color.FromArgb(70, 70, 70) : Color.FromArgb(0, 220, 255));
        var size = g.MeasureString(text, font);
        float y = bounds.Bottom - size.Height;
        g.DrawString(text, font, brush, bounds.X + (bounds.Width - size.Width) / 2, y);
    }

    private static float ValueToSweepFraction(double value) => (float)Math.Clamp(value / 100.0, 0, 1);

    private static double ValueToAngle(double value) => StartAngle + ValueToSweepFraction(value) * SweepAngle;

    internal static void DrawLed(Graphics g, RectangleF rect, GaugeModel model, AppSettings settings, double blinkPhase)
    {
        if (!settings.LedsEnabled) return;

        Color onColor;
        bool show;
        if (!model.Led.Active)
        {
            onColor = Color.FromArgb(40, 40, 42);
            show = true;
        }
        else if (model.Led.IsRed)
        {
            onColor = Color.FromArgb(255, 40, 40);
            double period = Math.Max(80, settings.LedRedBlinkMs);
            show = (blinkPhase % period) < period / 2;
        }
        else
        {
            onColor = Color.FromArgb(255, 210, 40);
            double period = Math.Max(80, settings.LedYellowBlinkMs);
            show = (blinkPhase % period) < period / 2;
        }

        Color drawColor = show ? onColor : Color.FromArgb(35, 35, 38);

        using var path = new GraphicsPath();
        path.AddEllipse(rect);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Lighten(drawColor, 60),
            SurroundColors = new[] { drawColor },
            CenterPoint = new PointF(rect.X + rect.Width * 0.3f, rect.Y + rect.Height * 0.3f)
        };
        g.FillEllipse(brush, rect);

        using var rim = new Pen(Color.FromArgb(120, 0, 0, 0), Math.Max(1f, rect.Width * 0.08f));
        g.DrawEllipse(rim, rect);

        if (show && model.Led.Active)
        {
            using var glow = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
            var glintRect = new RectangleF(rect.X + rect.Width * 0.18f, rect.Y + rect.Height * 0.15f,
                rect.Width * 0.3f, rect.Height * 0.25f);
            g.FillEllipse(glow, glintRect);
        }
    }
}

#endregion

#region Sensör Merkezi

[SupportedOSPlatform("windows")]
internal sealed class SensorHub : IDisposable
{
    private readonly CpuSensor _cpu = new();
    private readonly RamSensor _ram = new();
    private readonly DiskSensor _disk = new();
    private readonly NetworkSensorManager _network = new();
    private readonly GpuSensorManager _gpu = new();
    private readonly ThreadUsageSensorManager _threads = new();
    private readonly TemperatureSensorManager _temp;
    private readonly int _coreCount;
    private System.Threading.Timer? _timer;
    private AppSettings _settings;
    private readonly object _lock = new();
    private volatile bool _busy;

    internal event Action? SamplesReady;
    internal event Action? StructureChanged; // ağ/GPU listesi değişti -> gösterge listesi yeniden kurulmalı
    internal Dictionary<string, SensorReading> Latest { get; } = new();
    internal int CoreCount => _coreCount;
    internal string TemperatureSourceDescription => _temp.ActiveSourceDescription;

    internal SensorHub(AppSettings settings)
    {
        _settings = settings;
        _temp = new TemperatureSensorManager(settings);
        _coreCount = CpuTopology.GetPhysicalCoreCount();
        _network.DiscoveryChanged += () => StructureChanged?.Invoke();
        _gpu.DiscoveryChanged += () => StructureChanged?.Invoke();
    }

    internal List<string> NetworkAdapterIds() => _network.CurrentIds();
    internal List<string> GpuIds() => _gpu.CurrentIds();
    internal List<string> ThreadIds() => _threads.EnsureInitialized();
    internal string GpuDisplayName(string id) => _gpu.NameFor(id);

    internal void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _temp.UpdateSettings(settings);
        Reschedule();
    }

    internal void Start()
    {
        _threads.EnsureInitialized();
        Reschedule();
    }

    private void Reschedule()
    {
        _timer?.Dispose();
        int interval = Math.Clamp(_settings.UpdateIntervalMs, 250, 5000);
        _timer = new System.Threading.Timer(_ => Tick(), null, 0, interval);
    }

    private void Tick()
    {
        if (_busy) return; // önceki tur bitmediyse atla, UI'yı tıkamayalım.
        _busy = true;
        try
        {
            var snapshot = new Dictionary<string, SensorReading>
            {
                ["cpu"] = _cpu.Read(),
                ["ram"] = _ram.Read(),
                ["disk_c"] = _disk.Read(),
            };

            foreach (var kv in _network.PollAll(_settings.HideVirtualAdapters, BuildManualSpeedMap()))
                snapshot[kv.Key] = kv.Value;

            foreach (var kv in _gpu.PollAll())
                snapshot[kv.Key] = kv.Value;

            if (_settings.ShowThreadUsageMiniGauges)
            {
                foreach (var kv in _threads.PollAll())
                    snapshot[kv.Key] = kv.Value;
            }

            foreach (var kv in _temp.Poll(_coreCount))
                snapshot[kv.Key] = kv.Value;

            lock (_lock)
            {
                foreach (var kv in snapshot) Latest[kv.Key] = kv.Value;
            }
            SamplesReady?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log("Sensör döngüsü hatası: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private Dictionary<string, double> BuildManualSpeedMap() =>
        _settings.Gauges.Where(g => g.Kind == GaugeKind.Network && g.ManualMaxMbps > 0)
            .ToDictionary(g => g.Id, g => g.ManualMaxMbps);

    internal SensorReading? TryGet(string id)
    {
        lock (_lock)
        {
            return Latest.TryGetValue(id, out var r) ? r : null;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cpu.Dispose();
        _disk.Dispose();
        _network.Dispose();
        _gpu.Dispose();
        _threads.Dispose();
        _temp.Dispose();
    }
}

#endregion

#region Ana Form (Gösterge Şeridi)

[SupportedOSPlatform("windows")]
internal sealed class GadgetForm : Form
{
    private AppSettings _settings;
    private readonly SensorHub _hub;
    private readonly List<GaugeModel> _gauges = new();
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Bitmap? _backBuffer;
    private bool _layeredOk = true;
    private readonly NotifyIcon _trayIcon;
    private SettingsForm? _settingsForm;
    private bool _manualHidden;
    private bool _attachedToDesktop;
    private double _fullscreenCheckAccumulatorMs;
    private readonly uint _showSettingsMessageId;

    private const int GaugeWidthLarge = 150;
    private const int GaugeWidthMedium = 108;
    private const int GaugeWidthSmall = 78;
    private const int StripHeight = 185;

    public GadgetForm(AppSettings settings, SensorHub hub, uint showSettingsMessageId)
    {
        _settings = settings;
        _hub = hub;
        _showSettingsMessageId = showSettingsMessageId;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        // NOT: TransparencyKeyburada KASITLI OLARAK ayarlanmıyor. WinForms, TransparencyKey
        // ayarlandığında WS_EX_LAYERED'ı kendisi yönetip SetLayeredWindowAttributes (renk anahtarı)
        // çağırır; bu, bizim UpdateLayeredWindow (piksel bazlı alfa) çağrılarımızla aynı HWND
        // üzerinde çakışır ve pencereyi görünmez kılar. TransparencyKey yalnızca UpdateLayeredWindow
        // gerçekten başarısız olursa, Present() içinde devreye alınır (bkz. EnableTransparencyKeyFallback).

        SyncStaticGauges();
        BuildGaugeModels();
        ApplyTopMost();
        PositionWindow();

        _renderTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _renderTimer.Tick += (_, _) => RenderTick();
        _renderTimer.Start();

        _hub.SamplesReady += OnSamplesReady;
        _hub.StructureChanged += OnStructureChanged;

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Visible = true,
            Text = "MyGadget"
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Göster/Gizle", null, (_, _) => ToggleVisibility());
        menu.Items.Add("Ayarlar...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => Application.Exit());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        AutoStartManager.Apply(_settings.StartWithWindows);
        ApplyDesktopLevel();
    }

    private void ToggleVisibility()
    {
        Visible = !Visible;
        _manualHidden = !Visible; // kullanıcı bilerek gizlediyse tam ekran mantığı onu geri açmasın.
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            // NOT: Control'ün temel kurucusu, GadgetForm'un kendi kurucu gövdesi (ve _settings alan
            // ataması) çalışmadan ÖNCE bu getter'ı çağırır (sanal üye çağrısı taban kurucudan gelir).
            // Bu yüzden _settings burada null olabilir; null-koşullu erişimle çökmeyi önlüyoruz.
            if (_settings?.ClickThrough == true) cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
            if (_settings?.AlwaysOnTop ?? true) cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    private void ApplyTopMost()
    {
        TopMost = _settings.AlwaysOnTop;
    }

    internal void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        BuildGaugeModels();
        ApplyTopMost();
        PositionWindow();
        RecreateHandleForStyleChange();
        AutoStartManager.Apply(settings.StartWithWindows);
        ApplyDesktopLevel();
        _hub.UpdateSettings(settings);
        RenderTick(force: true);
    }

    private void ApplyDesktopLevel()
    {
        if (!IsHandleCreated) return;
        if (!_settings.DesktopLevel || _settings.AlwaysOnTop)
        {
            RestoreNormalParent();
            return;
        }

        try
        {
            IntPtr progman = NativeMethods.FindWindow("Progman", null);
            NativeMethods.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            IntPtr workerW = IntPtr.Zero;
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                IntPtr shellView = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                    workerW = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                return true;
            }, IntPtr.Zero);

            if (workerW != IntPtr.Zero)
            {
                NativeMethods.SetParent(Handle, workerW);
                _attachedToDesktop = true;
            }
            else
            {
                Logger.Log("Masaüstü seviyesi: WorkerW penceresi bulunamadı, normal pencere modu kullanılacak.");
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Masaüstü seviyesi uygulanamadı: " + ex.Message);
        }
    }

    private void RestoreNormalParent()
    {
        if (!_attachedToDesktop) return;
        try
        {
            NativeMethods.SetParent(Handle, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Logger.Log("Masaüstü seviyesinden çıkılamadı: " + ex.Message);
        }
        finally
        {
            _attachedToDesktop = false;
        }
    }

    private bool IsFullscreenAppActive()
    {
        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == Handle) return false;

            var sb = new System.Text.StringBuilder(256);
            NativeMethods.GetClassName(fg, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "DV2ControlHost") return false;

            if (!NativeMethods.GetWindowRect(fg, out var rect)) return false;
            var screen = Screen.FromHandle(fg);
            return rect.Left <= screen.Bounds.Left && rect.Top <= screen.Bounds.Top &&
                   rect.Right >= screen.Bounds.Right && rect.Bottom >= screen.Bounds.Bottom;
        }
        catch
        {
            return false;
        }
    }

    private void RecreateHandleForStyleChange()
    {
        if (IsHandleCreated)
        {
            // ClickThrough / AlwaysOnTop gibi genişletilmiş stil değişiklikleri handle yeniden
            // oluşturulmadan uygulanmaz; RecreateHandle CreateParams'ı yeniden okutur.
            RecreateHandle();
        }
    }

    private void SyncStaticGauges()
    {
        bool changed = false;
        changed |= EnsureGauge("disk_c", GaugeKind.DiskRead, 70, 90);
        changed |= EnsureGauge("cpu_package", GaugeKind.CpuPackageTemp, 70, 85);
        for (int i = 0; i < _hub.CoreCount; i++)
            changed |= EnsureGauge($"cpu_core_{i}", GaugeKind.CpuCoreTemp, 70, 85);
        foreach (var tid in _hub.ThreadIds())
            changed |= EnsureGauge(tid, GaugeKind.ThreadUsage, 80, 95, visible: false);

        if (changed) SettingsManager.Save(_settings);
    }

    private bool EnsureGauge(string id, GaugeKind kind, double yellow, double red, bool visible = true)
    {
        if (_settings.Gauges.Any(g => g.Id == id)) return false;
        int order = _settings.Gauges.Count == 0 ? 0 : _settings.Gauges.Max(g => g.Order) + 1;
        _settings.Gauges.Add(new GaugeSettings
        {
            Id = id, Kind = kind, Order = order, YellowThreshold = yellow, RedThreshold = red, Visible = visible
        });
        return true;
    }

    private void OnStructureChanged()
    {
        if (IsDisposed) return;
        try { BeginInvoke(new Action(SyncDiscoveredGauges)); }
        catch (InvalidOperationException) { /* form kapanıyor olabilir */ }
    }

    private void SyncDiscoveredGauges()
    {
        bool changed = false;
        changed |= SyncDynamicKind(GaugeKind.Network, _hub.NetworkAdapterIds(), 80, 95);
        changed |= SyncDynamicKind(GaugeKind.Gpu, _hub.GpuIds(), 80, 95);

        if (!changed) return;
        SettingsManager.Save(_settings);
        BuildGaugeModels();
        PositionWindow();
        RenderTick(force: true);
    }

    private bool SyncDynamicKind(GaugeKind kind, List<string> discoveredIds, double yellow, double red)
    {
        bool changed = false;
        var existing = _settings.Gauges.Where(g => g.Kind == kind).ToList();
        var existingIds = existing.Select(g => g.Id).ToHashSet();

        foreach (var id in discoveredIds.Where(id => !existingIds.Contains(id)))
            changed |= EnsureGauge(id, kind, yellow, red);

        var discoveredSet = discoveredIds.ToHashSet();
        foreach (var stale in existing.Where(g => !discoveredSet.Contains(g.Id)).ToList())
        {
            _settings.Gauges.Remove(stale);
            changed = true;
        }
        return changed;
    }

    private void BuildGaugeModels()
    {
        _gauges.Clear();
        foreach (var gs in _settings.Gauges.Where(g => g.Visible).OrderBy(g => g.Order))
        {
            var model = new GaugeModel
            {
                Id = gs.Id,
                Kind = gs.Kind,
                Label = LabelFor(gs),
                Size = SizeFor(gs.Kind),
                YellowThreshold = gs.YellowThreshold,
                RedThreshold = gs.RedThreshold,
            };
            _gauges.Add(model);
        }
    }

    private string LabelFor(GaugeSettings gs) => gs.Kind switch
    {
        GaugeKind.Cpu => "CPU",
        GaugeKind.Ram => "RAM",
        GaugeKind.DiskRead => "C:",
        GaugeKind.CpuPackageTemp => "Package",
        GaugeKind.CpuCoreTemp => "Core " + ExtractTrailingNumber(gs.Id),
        GaugeKind.Network => gs.Id,
        GaugeKind.Gpu => _hub.GpuDisplayName(gs.Id),
        GaugeKind.ThreadUsage => "T" + ExtractTrailingNumber(gs.Id),
        _ => gs.Id
    };

    private static string ExtractTrailingNumber(string id)
    {
        var m = Regex.Match(id, @"(\d+)$");
        return m.Success ? m.Value : id;
    }

    private static GaugeSize SizeFor(GaugeKind kind) => kind switch
    {
        GaugeKind.Cpu => GaugeSize.Large,
        GaugeKind.Ram => GaugeSize.Large,
        GaugeKind.CpuCoreTemp => GaugeSize.Small,
        GaugeKind.CpuPackageTemp => GaugeSize.Small,
        GaugeKind.ThreadUsage => GaugeSize.Small,
        _ => GaugeSize.Medium
    };

    private void PositionWindow()
    {
        float scale = (DeviceDpi / 96f) * (_settings.ScalePercent / 100f);
        int totalWidth = 20;
        foreach (var gm in _gauges)
        {
            int w = gm.Size switch { GaugeSize.Large => GaugeWidthLarge, GaugeSize.Small => GaugeWidthSmall, _ => GaugeWidthMedium };
            totalWidth += (int)(w * scale) + (int)(_settings.GaugeSpacing * scale);
        }
        int height = (int)(StripHeight * scale);

        var screen = ResolveScreen();
        int x, y = screen.WorkingArea.Top + (int)(4 * scale);
        switch (_settings.Position)
        {
            case DockPosition.TopLeft:
                x = screen.WorkingArea.Left + (int)(10 * scale);
                break;
            case DockPosition.TopRight:
                x = screen.WorkingArea.Right - totalWidth - (int)(10 * scale);
                break;
            case DockPosition.Free:
                x = _settings.FreeX;
                y = _settings.FreeY;
                break;
            default: // TopCenter
                x = screen.WorkingArea.Left + (screen.WorkingArea.Width - totalWidth) / 2;
                break;
        }

        Bounds = new Rectangle(x, y, totalWidth, height);
    }

    private Screen ResolveScreen()
    {
        if (!string.IsNullOrEmpty(_settings.MonitorDeviceName))
        {
            var match = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _settings.MonitorDeviceName);
            if (match != null) return match;
        }
        return Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    private void OnSamplesReady()
    {
        if (IsDisposed) return;
        try { BeginInvoke(new Action(ApplySamples)); }
        catch (InvalidOperationException) { /* form kapanıyor olabilir */ }
    }

    private void ApplySamples()
    {
        foreach (var gm in _gauges)
        {
            var reading = _hub.TryGet(gm.Id);
            if (reading.HasValue)
                gm.PushSample(reading.Value, Math.Max(1, _settings.SmoothingSamples));
        }
    }

    private void RenderTick(bool force = false)
    {
        CheckFullscreenHide();

        bool anyAnimating = force;
        double nowSeconds = _clock.Elapsed.TotalSeconds;

        foreach (var gm in _gauges)
        {
            if (gm.AnimateTowardTarget(_settings.NeedleDamping)) anyAnimating = true;
            gm.DecayPeak(_settings.PeakHoldSeconds);
            bool wasActive = gm.Led.Active;
            gm.Led.Update(gm.DisplayValue, gm.YellowThreshold, gm.RedThreshold, _settings.LedHysteresis,
                _settings.LedDelaySeconds, nowSeconds);
            if (gm.Led.Active) anyAnimating = true; // yanıp sönme sürdüğü için çizim devam etmeli.
            if (wasActive != gm.Led.Active) anyAnimating = true;
        }

        if (!anyAnimating && !force) return;

        Compose(nowSeconds * 1000.0);
    }

    private void CheckFullscreenHide()
    {
        if (!_settings.HideOnFullscreenApp || _manualHidden) return;

        _fullscreenCheckAccumulatorMs += _renderTimer.Interval;
        if (_fullscreenCheckAccumulatorMs < 500) return;
        _fullscreenCheckAccumulatorMs = 0;

        bool shouldHide = IsFullscreenAppActive();
        if (Visible == shouldHide) Visible = !shouldHide;
    }

    private void Compose(double blinkPhaseMs)
    {
        int w = Math.Max(1, Width);
        int h = Math.Max(1, Height);
        if (_backBuffer == null || _backBuffer.Width != w || _backBuffer.Height != h)
        {
            _backBuffer?.Dispose();
            _backBuffer = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        }

        using (var g = Graphics.FromImage(_backBuffer))
        {
            g.Clear(Color.Transparent);
            float scale = (DeviceDpi / 96f) * (_settings.ScalePercent / 100f);
            float x = 10 * scale;
            foreach (var gm in _gauges)
            {
                int baseWidth = gm.Size switch { GaugeSize.Large => GaugeWidthLarge, GaugeSize.Small => GaugeWidthSmall, _ => GaugeWidthMedium };
                float gw = baseWidth * scale;
                var bounds = new RectangleF(x, 2 * scale, gw, h - 4 * scale);
                GaugePainter.DrawGauge(g, bounds, gm, _settings);

                if (_settings.LedsEnabled)
                {
                    float ledSize = gw * 0.14f;
                    var ledRect = new RectangleF(bounds.X + (bounds.Width - ledSize) / 2, bounds.Bottom - ledSize * 1.6f, ledSize, ledSize);
                    GaugePainter.DrawLed(g, ledRect, gm, _settings, blinkPhaseMs);
                }

                x += gw + _settings.GaugeSpacing * scale;
            }
        }

        Present();
    }

    private void Present()
    {
        if (_backBuffer == null) return;
        if (_layeredOk)
        {
            try
            {
                SetBitmapLayered(_backBuffer, (byte)Math.Clamp(_settings.OpacityPercent * 255 / 100, 0, 255));
                return;
            }
            catch (Exception ex)
            {
                Logger.Log("Katmanlı pencere çizimi başarısız, TransparencyKey yöntemine düşülüyor: " + ex.Message);
                _layeredOk = false;
                EnableTransparencyKeyFallback();
            }
        }
        Invalidate();
    }

    private void EnableTransparencyKeyFallback()
    {
        // UpdateLayeredWindow çalışmadığında devreye alınan yedek yöntem: burada GERÇEKTEN
        // SetLayeredWindowAttributes (renk anahtarı) kullanılacağı için artık içerik normal
        // WM_PAINT üzerinden (bkz. OnPaint) çiziliyor olmalı.
        TransparencyKey = Color.Black;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { /* yanıp sönmeyi azalt */ }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_layeredOk && _backBuffer != null)
            e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
    }

    private void SetBitmapLayered(Bitmap bitmap, byte opacity)
    {
        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

            var size = new NativeMethods.SIZE(bitmap.Width, bitmap.Height);
            var srcPoint = new NativeMethods.POINT(0, 0);
            var topLeft = new NativeMethods.POINT(Left, Top);
            var blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = opacity,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            if (!NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref topLeft, ref size, memDc, ref srcPoint, 0, ref blend, NativeMethods.ULW_ALPHA))
                throw new InvalidOperationException("UpdateLayeredWindow başarısız (hata kodu " + Marshal.GetLastWin32Error() + ")");
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            if (hBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memDc, oldBitmap);
                NativeMethods.DeleteObject(hBitmap);
            }
            NativeMethods.DeleteDC(memDc);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (_showSettingsMessageId != 0 && (uint)m.Msg == _showSettingsMessageId)
        {
            // İkinci bir MyGadget örneği başlatılmaya çalışıldı; onun yerine ayarları açıyoruz.
            OpenSettings();
            return;
        }
        if (m.Msg == NativeMethods.WM_LBUTTONDOWN && !_settings.PositionLocked && !_settings.ClickThrough)
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, (IntPtr)NativeMethods.HTCAPTION, IntPtr.Zero);
            return;
        }
        if (m.Msg == NativeMethods.WM_EXITSIZEMOVE && !_settings.PositionLocked)
        {
            // Kullanıcı sürükleyerek taşıdı; konumu "Serbest" moda alıp kalıcı hâle getir.
            _settings.Position = DockPosition.Free;
            _settings.FreeX = Left;
            _settings.FreeY = Top;
            SettingsManager.Save(_settings);
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Right) OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, ApplySettings, LabelFor);
        }
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _hub.SamplesReady -= OnSamplesReady;
        _hub.StructureChanged -= OnStructureChanged;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _backBuffer?.Dispose();
        base.OnFormClosed(e);
    }
}

#endregion

#region Ayarlar Formu

[SupportedOSPlatform("windows")]
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _working;
    private readonly Action<AppSettings> _apply;
    private readonly Func<GaugeSettings, string> _nameResolver;

    private DataGridView _gaugeGrid = null!;
    private List<GaugeRow> _gaugeRows = new();

    // Veri
    private NumericUpDown _updateIntervalInput = null!;
    private NumericUpDown _smoothingInput = null!;
    private NumericUpDown _dampingInput = null!;
    private CheckBox _peakEnabledInput = null!;
    private NumericUpDown _peakHoldInput = null!;
    private ComboBox _tempSourceInput = null!;
    private NumericUpDown _lhmPortInput = null!;
    private ComboBox _tempUnitInput = null!;
    private CheckBox _hideVirtualInput = null!;
    private CheckBox _threadMiniInput = null!;

    // Görünüm
    private NumericUpDown _scaleInput = null!;
    private NumericUpDown _spacingInput = null!;
    private ComboBox _themeInput = null!;
    private NumericUpDown _opacityInput = null!;
    private ComboBox _positionInput = null!;
    private ComboBox _monitorInput = null!;
    private CheckBox _showDigitalInput = null!;
    private CheckBox _showLabelsInput = null!;
    private CheckBox _ledsEnabledInput = null!;
    private NumericUpDown _ledYellowBlinkInput = null!;
    private NumericUpDown _ledRedBlinkInput = null!;
    private NumericUpDown _ledHysteresisInput = null!;
    private NumericUpDown _ledDelayInput = null!;

    // Davranış
    private CheckBox _alwaysOnTopInput = null!;
    private CheckBox _desktopLevelInput = null!;
    private CheckBox _positionLockedInput = null!;
    private CheckBox _clickThroughInput = null!;
    private CheckBox _hideFullscreenInput = null!;
    private CheckBox _startWithWindowsInput = null!;

    public SettingsForm(AppSettings current, Action<AppSettings> apply, Func<GaugeSettings, string> nameResolver)
    {
        _working = Clone(current);
        _apply = apply;
        _nameResolver = nameResolver;

        Text = "MyGadget - Ayarlar";
        Width = 580;
        Height = 560;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGaugesTab());
        tabs.TabPages.Add(BuildDataTab());
        tabs.TabPages.Add(BuildAppearanceTab());
        tabs.TabPages.Add(BuildBehaviorTab());

        var applyBtn = new Button { Text = "Uygula" };
        var okBtn = new Button { Text = "Tamam" };
        var cancelBtn = new Button { Text = "İptal", DialogResult = DialogResult.Cancel };
        var resetBtn = new Button { Text = "Varsayılanlara dön" };
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(8)
        };
        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Controls.Add(okBtn);
        buttonPanel.Controls.Add(applyBtn);
        buttonPanel.Controls.Add(resetBtn);

        Controls.Add(tabs);
        Controls.Add(buttonPanel);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        applyBtn.Click += (_, _) => ApplyWorking();
        okBtn.Click += (_, _) => { ApplyWorking(); Close(); };
        resetBtn.Click += (_, _) => ResetToDefaults();
    }

    private sealed class GaugeRow
    {
        public GaugeSettings Settings { get; }
        public string Name { get; }

        public GaugeRow(GaugeSettings settings, string name)
        {
            Settings = settings;
            Name = name;
        }

        public bool Visible { get => Settings.Visible; set => Settings.Visible = value; }
        public double Yellow { get => Settings.YellowThreshold; set => Settings.YellowThreshold = value; }
        public double Red { get => Settings.RedThreshold; set => Settings.RedThreshold = value; }
        public double ManualMbps { get => Settings.ManualMaxMbps; set => Settings.ManualMaxMbps = value; }
    }

    private TabPage BuildGaugesTab()
    {
        var page = new TabPage("Göstergeler");

        _gaugeGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
        };
        _gaugeGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Visible", HeaderText = "Göster", Width = 55 });
        _gaugeGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Gösterge", ReadOnly = true, Width = 150 });
        _gaugeGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Yellow", HeaderText = "Sarı Eşik", Width = 80 });
        _gaugeGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Red", HeaderText = "Kırmızı Eşik", Width = 90 });
        _gaugeGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ManualMbps", HeaderText = "Maks Mbps (ağ)", Width = 100 });

        RefreshGaugeGrid();

        var upBtn = new Button { Text = "▲ Yukarı", Width = 90 };
        var downBtn = new Button { Text = "▼ Aşağı", Width = 90 };
        upBtn.Click += (_, _) => MoveSelectedGauge(-1);
        downBtn.Click += (_, _) => MoveSelectedGauge(1);
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(4) };
        btnPanel.Controls.Add(upBtn);
        btnPanel.Controls.Add(downBtn);

        page.Controls.Add(_gaugeGrid);
        page.Controls.Add(btnPanel);
        return page;
    }

    private void RefreshGaugeGrid()
    {
        _gaugeRows = _working.Gauges
            .Where(g => g.Kind != GaugeKind.ThreadUsage) // bu grup "Veri" sekmesindeki tek anahtarla kontrol edilir.
            .OrderBy(g => g.Order)
            .Select(g => new GaugeRow(g, _nameResolver(g)))
            .ToList();
        _gaugeGrid.DataSource = null;
        _gaugeGrid.DataSource = _gaugeRows;
    }

    private void MoveSelectedGauge(int direction)
    {
        _gaugeGrid.EndEdit();
        if (_gaugeGrid.CurrentRow == null) return;
        int idx = _gaugeGrid.CurrentRow.Index;
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= _gaugeRows.Count) return;

        (_gaugeRows[idx].Settings.Order, _gaugeRows[newIdx].Settings.Order) =
            (_gaugeRows[newIdx].Settings.Order, _gaugeRows[idx].Settings.Order);

        RefreshGaugeGrid();
        if (newIdx < _gaugeGrid.Rows.Count)
            _gaugeGrid.CurrentCell = _gaugeGrid.Rows[newIdx].Cells[0];
    }

    private TabPage BuildDataTab()
    {
        var page = new TabPage("Veri");
        var layout = NewFormLayout();

        _updateIntervalInput = new NumericUpDown { Minimum = 250, Maximum = 5000, Increment = 50, Value = _working.UpdateIntervalMs };
        AddRow(layout, "Güncelleme aralığı (ms):", _updateIntervalInput);

        _smoothingInput = new NumericUpDown { Minimum = 1, Maximum = 20, Value = _working.SmoothingSamples };
        AddRow(layout, "Yumuşatma örnek sayısı:", _smoothingInput);

        _dampingInput = new NumericUpDown { Minimum = 0.01m, Maximum = 1.00m, DecimalPlaces = 2, Increment = 0.01m, Value = (decimal)_working.NeedleDamping };
        AddRow(layout, "İbre sönüm/hız katsayısı (0-1):", _dampingInput);

        _peakEnabledInput = new CheckBox { Checked = _working.PeakNeedleEnabled, Text = "Etkin", AutoSize = true };
        AddRow(layout, "Tepe değer ibresi:", _peakEnabledInput);

        _peakHoldInput = new NumericUpDown { Minimum = 1, Maximum = 30, Value = _working.PeakHoldSeconds };
        AddRow(layout, "Tepe değer tutma süresi (sn):", _peakHoldInput);

        _tempSourceInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _tempSourceInput.Items.AddRange(new object[] { "Otomatik", "LibreHardwareMonitor WMI", "LibreHardwareMonitor HTTP", "ACPI" });
        _tempSourceInput.SelectedIndex = (int)_working.TempSource;
        AddRow(layout, "Sıcaklık kaynağı:", _tempSourceInput);

        _lhmPortInput = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = _working.LhmHttpPort };
        AddRow(layout, "LHM HTTP portu:", _lhmPortInput);

        _tempUnitInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _tempUnitInput.Items.AddRange(new object[] { "°C", "°F" });
        _tempUnitInput.SelectedIndex = _working.UseFahrenheit ? 1 : 0;
        AddRow(layout, "Sıcaklık birimi:", _tempUnitInput);

        _hideVirtualInput = new CheckBox { Checked = _working.HideVirtualAdapters, Text = "Gizle", AutoSize = true };
        AddRow(layout, "Sanal/VPN adaptörleri:", _hideVirtualInput);

        _threadMiniInput = new CheckBox { Checked = _working.ShowThreadUsageMiniGauges, Text = "Göster", AutoSize = true };
        AddRow(layout, "İş parçacığı kullanım mini göstergeleri:", _threadMiniInput);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildAppearanceTab()
    {
        var page = new TabPage("Görünüm");
        var layout = NewFormLayout();

        _scaleInput = new NumericUpDown { Minimum = 50, Maximum = 200, Value = _working.ScalePercent };
        AddRow(layout, "Ölçek (%):", _scaleInput);

        _spacingInput = new NumericUpDown { Minimum = 0, Maximum = 50, Value = _working.GaugeSpacing };
        AddRow(layout, "Göstergeler arası boşluk:", _spacingInput);

        _themeInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _themeInput.Items.AddRange(new object[] { "Krom (klasik)", "Koyu spor", "Açık", "Retro" });
        _themeInput.SelectedIndex = (int)_working.Theme;
        AddRow(layout, "Tema:", _themeInput);

        _opacityInput = new NumericUpDown { Minimum = 30, Maximum = 100, Value = _working.OpacityPercent };
        AddRow(layout, "Saydamlık (%):", _opacityInput);

        _positionInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _positionInput.Items.AddRange(new object[] { "Üst-orta", "Üst-sol", "Üst-sağ", "Serbest" });
        _positionInput.SelectedIndex = (int)_working.Position;
        AddRow(layout, "Konum:", _positionInput);

        _monitorInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _monitorInput.Items.Add("(Birincil ekran)");
        foreach (var s in Screen.AllScreens) _monitorInput.Items.Add(s.DeviceName);
        int monitorIdx = 0;
        if (!string.IsNullOrEmpty(_working.MonitorDeviceName))
        {
            int found = Array.FindIndex(Screen.AllScreens, s => s.DeviceName == _working.MonitorDeviceName);
            if (found >= 0) monitorIdx = found + 1;
        }
        _monitorInput.SelectedIndex = monitorIdx;
        AddRow(layout, "Monitör:", _monitorInput);

        _showDigitalInput = new CheckBox { Checked = _working.ShowDigitalValues, Text = "Göster", AutoSize = true };
        AddRow(layout, "Dijital değerler:", _showDigitalInput);

        _showLabelsInput = new CheckBox { Checked = _working.ShowLabels, Text = "Göster", AutoSize = true };
        AddRow(layout, "Etiketler:", _showLabelsInput);

        _ledsEnabledInput = new CheckBox { Checked = _working.LedsEnabled, Text = "Etkin", AutoSize = true };
        AddRow(layout, "LED uyarı lambaları:", _ledsEnabledInput);

        _ledYellowBlinkInput = new NumericUpDown { Minimum = 80, Maximum = 2000, Increment = 20, Value = _working.LedYellowBlinkMs };
        AddRow(layout, "Sarı LED yanıp sönme (ms):", _ledYellowBlinkInput);

        _ledRedBlinkInput = new NumericUpDown { Minimum = 80, Maximum = 2000, Increment = 20, Value = _working.LedRedBlinkMs };
        AddRow(layout, "Kırmızı LED yanıp sönme (ms):", _ledRedBlinkInput);

        _ledHysteresisInput = new NumericUpDown { Minimum = 0, Maximum = 20, DecimalPlaces = 1, Increment = 0.5m, Value = (decimal)_working.LedHysteresis };
        AddRow(layout, "LED histerezis:", _ledHysteresisInput);

        _ledDelayInput = new NumericUpDown { Minimum = 0, Maximum = 30, Value = _working.LedDelaySeconds };
        AddRow(layout, "LED gecikme süresi (sn):", _ledDelayInput);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildBehaviorTab()
    {
        var page = new TabPage("Davranış");
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14)
        };

        _alwaysOnTopInput = new CheckBox { Text = "Her zaman üstte", Checked = _working.AlwaysOnTop, AutoSize = true };
        _desktopLevelInput = new CheckBox { Text = "Masaüstü seviyesinde (diğer pencerelerin altında)", Checked = _working.DesktopLevel, AutoSize = true };
        _positionLockedInput = new CheckBox { Text = "Konumu kilitle", Checked = _working.PositionLocked, AutoSize = true };
        _clickThroughInput = new CheckBox { Text = "Fare tıklamalarını geçir (click-through)", Checked = _working.ClickThrough, AutoSize = true };
        var note = new Label
        {
            Text = "Not: click-through açıkken ayarlara sistem tepsisi simgesinden ulaşabilirsiniz.",
            AutoSize = true, Margin = new Padding(20, 0, 0, 10), ForeColor = Color.DimGray
        };
        _hideFullscreenInput = new CheckBox { Text = "Tam ekran uygulama/oyun açıkken gizle", Checked = _working.HideOnFullscreenApp, AutoSize = true };
        _startWithWindowsInput = new CheckBox { Text = "Windows ile başlat", Checked = _working.StartWithWindows, AutoSize = true };

        var exitBtn = new Button { Text = "MyGadget'tan Çık", AutoSize = true, Margin = new Padding(0, 24, 0, 0) };
        exitBtn.Click += (_, _) => Application.Exit();

        layout.Controls.Add(_alwaysOnTopInput);
        layout.Controls.Add(_desktopLevelInput);
        layout.Controls.Add(_positionLockedInput);
        layout.Controls.Add(_clickThroughInput);
        layout.Controls.Add(note);
        layout.Controls.Add(_hideFullscreenInput);
        layout.Controls.Add(_startWithWindowsInput);
        layout.Controls.Add(exitBtn);

        page.Controls.Add(layout);
        return page;
    }

    private static TableLayoutPanel NewFormLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        int row = layout.Controls.Count / layout.ColumnCount;
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 6, 7) }, 0, row);
        control.Anchor = AnchorStyles.Left;
        control.Margin = new Padding(0, 4, 0, 4);
        if (control.Width < 90) control.Width = 130;
        layout.Controls.Add(control, 1, row);
    }

    private void ApplyWorking()
    {
        _gaugeGrid.EndEdit();

        _working.UpdateIntervalMs = (int)_updateIntervalInput.Value;
        _working.SmoothingSamples = (int)_smoothingInput.Value;
        _working.NeedleDamping = (double)_dampingInput.Value;
        _working.PeakNeedleEnabled = _peakEnabledInput.Checked;
        _working.PeakHoldSeconds = (int)_peakHoldInput.Value;
        _working.TempSource = (TemperatureSource)_tempSourceInput.SelectedIndex;
        _working.LhmHttpPort = (int)_lhmPortInput.Value;
        _working.UseFahrenheit = _tempUnitInput.SelectedIndex == 1;
        _working.HideVirtualAdapters = _hideVirtualInput.Checked;
        _working.ShowThreadUsageMiniGauges = _threadMiniInput.Checked;

        _working.ScalePercent = (int)_scaleInput.Value;
        _working.GaugeSpacing = (int)_spacingInput.Value;
        _working.Theme = (AppTheme)_themeInput.SelectedIndex;
        _working.OpacityPercent = (int)_opacityInput.Value;
        _working.Position = (DockPosition)_positionInput.SelectedIndex;
        _working.MonitorDeviceName = _monitorInput.SelectedIndex <= 0 ? null : Screen.AllScreens[_monitorInput.SelectedIndex - 1].DeviceName;
        _working.ShowDigitalValues = _showDigitalInput.Checked;
        _working.ShowLabels = _showLabelsInput.Checked;
        _working.LedsEnabled = _ledsEnabledInput.Checked;
        _working.LedYellowBlinkMs = (int)_ledYellowBlinkInput.Value;
        _working.LedRedBlinkMs = (int)_ledRedBlinkInput.Value;
        _working.LedHysteresis = (double)_ledHysteresisInput.Value;
        _working.LedDelaySeconds = (int)_ledDelayInput.Value;

        _working.AlwaysOnTop = _alwaysOnTopInput.Checked;
        _working.DesktopLevel = _desktopLevelInput.Checked;
        _working.PositionLocked = _positionLockedInput.Checked;
        _working.ClickThrough = _clickThroughInput.Checked;
        _working.HideOnFullscreenApp = _hideFullscreenInput.Checked;
        _working.StartWithWindows = _startWithWindowsInput.Checked;

        foreach (var g in _working.Gauges.Where(g => g.Kind == GaugeKind.ThreadUsage))
            g.Visible = _working.ShowThreadUsageMiniGauges;

        // Gösterge satırlarındaki Göster/Eşik/Mbps değerleri DataGridView bağlamasıyla
        // zaten doğrudan GaugeSettings nesnelerine yazılmış durumda.

        SettingsManager.Save(_working);
        _apply(Clone(_working));
    }

    private void ResetToDefaults()
    {
        var d = AppSettings.CreateDefault();

        _updateIntervalInput.Value = d.UpdateIntervalMs;
        _smoothingInput.Value = d.SmoothingSamples;
        _dampingInput.Value = (decimal)d.NeedleDamping;
        _peakEnabledInput.Checked = d.PeakNeedleEnabled;
        _peakHoldInput.Value = d.PeakHoldSeconds;
        _tempSourceInput.SelectedIndex = (int)d.TempSource;
        _lhmPortInput.Value = d.LhmHttpPort;
        _tempUnitInput.SelectedIndex = d.UseFahrenheit ? 1 : 0;
        _hideVirtualInput.Checked = d.HideVirtualAdapters;
        _threadMiniInput.Checked = d.ShowThreadUsageMiniGauges;

        _scaleInput.Value = d.ScalePercent;
        _spacingInput.Value = d.GaugeSpacing;
        _themeInput.SelectedIndex = (int)d.Theme;
        _opacityInput.Value = d.OpacityPercent;
        _positionInput.SelectedIndex = (int)d.Position;
        _monitorInput.SelectedIndex = 0;
        _showDigitalInput.Checked = d.ShowDigitalValues;
        _showLabelsInput.Checked = d.ShowLabels;
        _ledsEnabledInput.Checked = d.LedsEnabled;
        _ledYellowBlinkInput.Value = d.LedYellowBlinkMs;
        _ledRedBlinkInput.Value = d.LedRedBlinkMs;
        _ledHysteresisInput.Value = (decimal)d.LedHysteresis;
        _ledDelayInput.Value = d.LedDelaySeconds;

        _alwaysOnTopInput.Checked = d.AlwaysOnTop;
        _desktopLevelInput.Checked = d.DesktopLevel;
        _positionLockedInput.Checked = d.PositionLocked;
        _clickThroughInput.Checked = d.ClickThrough;
        _hideFullscreenInput.Checked = d.HideOnFullscreenApp;
        _startWithWindowsInput.Checked = d.StartWithWindows;

        foreach (var row in _gaugeRows)
        {
            row.Settings.YellowThreshold = row.Settings.Kind == GaugeKind.Gpu ? 80 : 70;
            row.Settings.RedThreshold = row.Settings.Kind == GaugeKind.Gpu ? 95 : 90;
            row.Settings.Visible = true;
            row.Settings.ManualMaxMbps = 0;
        }
        RefreshGaugeGrid();
    }

    private static AppSettings Clone(AppSettings s)
    {
        var json = JsonSerializer.Serialize(s);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.CreateDefault();
    }
}

#endregion

#region Program

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        uint showSettingsMessageId = NativeMethods.RegisterWindowMessage("MyGadget_ShowSettings_8F2C2B1E");

        using var mutex = new Mutex(true, "MyGadget_SingleInstance_8F2C2B1E", out bool createdNew);
        if (!createdNew)
        {
            // Zaten çalışan bir örnek var; onun ayarlarını açmasını isteyip çıkalım.
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, showSettingsMessageId, IntPtr.Zero, IntPtr.Zero);
            Logger.Log("Başka bir MyGadget örneği zaten çalışıyor; ayarlar penceresi açılması istendi.");
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Log("Yakalanmayan hata: " + e.ExceptionObject);
        Application.ThreadException += (_, e) =>
            Logger.Log("UI iş parçacığı hatası: " + e.Exception);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settings = SettingsManager.Load();
        using var hub = new SensorHub(settings);
        hub.Start();

        using var form = new GadgetForm(settings, hub, showSettingsMessageId);
        form.Show();

        Application.Run();
    }
}

#endregion
