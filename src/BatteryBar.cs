using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace BatteryBar {
public class Preferences {
    public IconStyle Style = IconStyle.Solid;
    public int Low = 20, Full = 100, Seconds = 30;
    public int ColorVersion;
    public int Charging = Color.DodgerBlue.ToArgb(), Charged = Color.MediumSeaGreen.ToArgb(), Discharging = Color.FromArgb(137, 80, 216).ToArgb(), LowColor = Color.OrangeRed.ToArgb();
    public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryBar", "settings.xml"); } }
    public static Preferences Load() {
        try { using (var f = File.OpenRead(FilePath)) { var p = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f); p.UpgradeColors(); p.Validate(); return p; } }
        catch { return new Preferences(); }
    }
    public void Validate() { if (!Enum.IsDefined(typeof(IconStyle), Style)) throw new InvalidDataException("无效的图标样式。"); if (Low < 1 || Full > 100 || Full <= Low || Seconds < 10 || Seconds > 300) throw new InvalidDataException("阈值应满足：1 ≤ 低电量 < 满电 ≤ 100；刷新间隔为 10–300 秒。"); }
    public void UpgradeColors() {
        if (ColorVersion < 1 && Discharging == Color.WhiteSmoke.ToArgb()) Discharging = Color.FromArgb(137, 80, 216).ToArgb();
        ColorVersion = 1;
    }
    public void Save() {
        Validate(); ColorVersion = 1; Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string temp = FilePath + ".tmp";
        using (var f = File.Create(temp)) new XmlSerializer(typeof(Preferences)).Serialize(f, this);
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
    }
}
public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct Battery { public byte AC, Present, Charging, Discharging, Spare0, Spare1, Spare2, Tag; public uint Max, Remaining; public int Rate; public uint Time, Alert1, Alert2; }
    [StructLayout(LayoutKind.Sequential)] public struct Power { public byte AC, Flags, Percent, Saver; public uint Life, FullLife; }
    [DllImport("powrprof.dll")] public static extern uint CallNtPowerInformation(int level, IntPtr input, uint size, out Battery battery, uint outputSize);
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out Power power);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
public class Reading {
    public bool Present, AC, Charging, Discharging, Available;
    public int Percent = -1, Rate = -1;
    public uint Remaining, Max, Time = uint.MaxValue;
    public static Reading Read() {
        var r = new Reading(); Native.Power p;
        if (Native.GetSystemPowerStatus(out p)) { r.Available = true; r.Present = p.Flags != 255 && (p.Flags & 128) == 0; r.AC = p.AC == 1; r.Charging = p.Flags != 255 && (p.Flags & 8) != 0; r.Discharging = r.Present && p.AC == 0; r.Percent = p.Percent <= 100 ? p.Percent : -1; r.Time = p.Life; }
        Native.Battery b;
        if (Native.CallNtPowerInformation(5, IntPtr.Zero, 0, out b, (uint)Marshal.SizeOf(typeof(Native.Battery))) == 0) {
            r.Available = true; r.Present = b.Present != 0; r.AC = b.AC != 0; r.Charging = b.Charging != 0; r.Discharging = b.Discharging != 0; r.Rate = b.Rate; r.Remaining = b.Remaining; r.Max = b.Max;
            if (b.Time != uint.MaxValue) r.Time = b.Time;
        }
        return r;
    }
    public string State(Preferences p) {
        if (!Available) return "读取失败"; if (!Present) return "无电池";
        if (Charging) return "充电中";
        if (AC && !Discharging && Percent >= p.Full) return "已充满";
        if (Percent >= 0 && Percent <= p.Low) return "低电量";
        return AC && !Discharging ? "已接电 · 未充电" : "放电中";
    }
    public Color Tint(Preferences p) { string s = State(p); return Color.FromArgb(s == "充电中" ? p.Charging : s == "已充满" ? p.Charged : s == "低电量" ? p.LowColor : p.Discharging); }
    static string Duration(double seconds) { if (double.IsNaN(seconds) || seconds <= 0 || seconds > 604800) return "不可用"; int m = (int)Math.Ceiling(seconds / 60); return (m / 60) + "小时" + (m % 60) + "分"; }
    public string Detail(Preferences p) {
        string head = "BatteryBar · " + State(p) + (Present && Percent >= 0 ? " " + Percent + "%" : "");
        if (!Present || !Available) return head;
        bool validRate = PowerHistory.Direction(this) != 0;
        string rate = validRate ? (Math.Abs((double)Rate) / 1000).ToString("0.00") + " W" : "不可用";
        string time = "";
        if (Charging) time = "预计充满：" + (validRate && Max != uint.MaxValue && Remaining != uint.MaxValue && Max > Remaining ? Duration((Max - Remaining) * 3600.0 / Rate) + "（估算）" : "不可用");
        else if (Discharging) time = "预计可用：" + (Time != uint.MaxValue && Time > 0 ? Duration(Time) : validRate && Remaining != uint.MaxValue && Remaining > 0 ? Duration(Remaining * 3600.0 / -((double)Rate)) + "（估算）" : "不可用");
        else time = "当前由外接电源供电";
        return head + "\n" + (Charging ? "充电速率：" : Discharging ? "放电速率：" : "充放电速率：") + rate + "\n" + time;
    }
}
public class TrayApp : ApplicationContext {
    readonly TrayTooltip tooltip;
    readonly PowerHistory history = new PowerHistory();
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    readonly PowerPopup popup = new PowerPopup();
    readonly System.Windows.Forms.Timer hoverTimer = new System.Windows.Forms.Timer { Interval = 200 };
    Point hoverPoint; double hoverStarted, lastSample; bool suspended;
    readonly NotifyIcon tray = new NotifyIcon(); readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(); readonly Control dispatcher = new Control(); Preferences prefs = Preferences.Load(); Icon icon; string iconKey; SettingsForm settings;
    public TrayApp() {
        dispatcher.CreateControl(); var handle = dispatcher.Handle;
        var menu = new ContextMenuStrip(); menu.Items.Add("设置…", null, delegate { OpenSettings(); }); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出", null, delegate { ExitThread(); }); tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { OpenSettings(); }; timer.Tick += delegate { Refresh(); }; timer.Interval = Math.Min(prefs.Seconds, 30) * 1000;
        tray.MouseMove += delegate { if (suspended) return; hoverPoint = Cursor.Position; if (!hoverTimer.Enabled) { hoverStarted = clock.Elapsed.TotalSeconds; hoverTimer.Start(); } };
        tray.MouseDown += delegate { HidePopup(); };
        menu.Opening += delegate { HidePopup(); };
        hoverTimer.Tick += delegate {
            var pos = Cursor.Position;
            if (tray.ContextMenuStrip.Visible || Math.Abs(pos.X - hoverPoint.X) > 4 || Math.Abs(pos.Y - hoverPoint.Y) > 4) { HidePopup(); return; }
            double now = clock.Elapsed.TotalSeconds;
            if (now - hoverStarted < .5) return;
            if (!popup.Visible) { Refresh(); popup.ShowAt(pos); }
            else if (now - lastSample >= 2) Refresh();
        };
        SystemEvents.PowerModeChanged += PowerChanged; Refresh(); tray.Visible = true; tooltip = new TrayTooltip(tray); timer.Start();
    }
    void HidePopup() { hoverTimer.Stop(); popup.Hide(); }
    void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (!dispatcher.IsDisposed) { try { dispatcher.BeginInvoke((Action)delegate { if (e.Mode == PowerModes.Suspend) { suspended = true; timer.Stop(); HidePopup(); history.Break(); } else { if (e.Mode == PowerModes.Resume) history.Break(); suspended = false; Refresh(); timer.Start(); } }); } catch (InvalidOperationException) { } } }
    void OpenSettings() { HidePopup(); if (settings != null && !settings.IsDisposed) { settings.Activate(); return; } settings = new SettingsForm(prefs, delegate(Preferences p) { prefs = p; timer.Interval = Math.Min(p.Seconds, 30) * 1000; Refresh(); }); settings.Show(); }
    void Refresh() {
        var r = Reading.Read(); lastSample = clock.Elapsed.TotalSeconds;
        history.Add(lastSample, r, Math.Min(prefs.Seconds, 30) * 2 + 5);
        popup.UpdateData(r, prefs, history, lastSample);
        tray.Text = "";
        string digits = r.Present && r.Percent >= 0 ? r.Percent.ToString() : "?"; Color tint = r.Tint(prefs);
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        string key = digits + ":" + tint.ToArgb() + ":" + prefs.Style + ":" + size; if (key == iconKey) return;
        Icon next = IconRenderer.Draw(digits, tint, prefs.Style, size); tray.Icon = next; if (icon != null) icon.Dispose(); icon = next; iconKey = key;
    }
    protected override void ExitThreadCore() { hoverTimer.Stop(); hoverTimer.Dispose(); popup.Dispose(); timer.Stop(); timer.Dispose(); SystemEvents.PowerModeChanged -= PowerChanged; tray.Visible = false; tray.ContextMenuStrip.Dispose(); tooltip.Dispose(); tray.Dispose(); if (icon != null) icon.Dispose(); if (settings != null) settings.Dispose(); dispatcher.Dispose(); base.ExitThreadCore(); }
}
static class Program {
    [STAThread] static void Main() { bool first; using (var mutex = new Mutex(true, @"Local\BatteryBar.Tray.App", out first)) { if (!first) return; Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new TrayApp()); GC.KeepAlive(mutex); } }
}
}
