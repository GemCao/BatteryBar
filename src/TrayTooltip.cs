using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BatteryBar {
// Version 4 suppresses the legacy Shell tooltip unless NIF_SHOWTIP is requested.
// Translate its packed mouse callbacks for the Framework NotifyIcon handler.
sealed class TrayTooltip : NativeWindow, IDisposable {
    readonly uint id;
    readonly int taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NotifyData {
        public uint Size;
        public IntPtr Window;
        public uint Id, Flags, Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int RegisterWindowMessage(string message);
    public TrayTooltip(NotifyIcon icon) {
        // This application targets the Windows .NET Framework implementation.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var windowField = typeof(NotifyIcon).GetField("window", flags);
        var idField = typeof(NotifyIcon).GetField("id", flags);
        if (windowField == null || idField == null) return;
        var window = windowField.GetValue(icon) as NativeWindow;
        if (window == null || window.Handle == IntPtr.Zero) return;
        id = Convert.ToUInt32(idField.GetValue(icon));
        AssignHandle(window.Handle);
        SetVersion();
    }
    void SetVersion() {
        var data = new NotifyData { Size = (uint)Marshal.SizeOf(typeof(NotifyData)), Window = Handle, Id = id, Version = 4 };
        Shell_NotifyIcon(4, ref data);
    }
    protected override void WndProc(ref Message message) {
        if (message.Msg == 0x400 + 1024) {
            int notification = (int)(message.LParam.ToInt64() & 0xffff);
            if (notification == 0x7b) notification = 0x205; // Context menu -> right button up.
            message.LParam = new IntPtr(notification);
        }
        base.WndProc(ref message);
        if (message.Msg == taskbarCreated) SetVersion();
    }
    public void Dispose() { ReleaseHandle(); }
}
}
