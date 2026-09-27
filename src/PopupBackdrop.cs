using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryBar {
// Documented DWM Desktop Acrylic (Windows 11 build 22621+); no injection or hooks.
public static class PopupBackdrop {
    [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
    [DllImport("dwmapi.dll")] static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    public static bool Apply(IntPtr window) {
        try {
            bool composition; Native.Power power;
            if (SystemInformation.HighContrast || DwmIsCompositionEnabled(out composition) != 0 || !composition ||
                (Native.GetSystemPowerStatus(out power) && power.Saver == 1)) { Disable(window); return false; }
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                if(key!=null && Convert.ToInt32(key.GetValue("EnableTransparency",1))==0) { Disable(window); return false; }
            }
            int dark=1, rounded=2, acrylic=3;
            DwmSetWindowAttribute(window,20,ref dark,4);
            DwmSetWindowAttribute(window,33,ref rounded,4);
            if(DwmSetWindowAttribute(window,38,ref acrylic,4)!=0) { Disable(window); return false; }
            var margins=new Margins { Left=-1,Right=-1,Top=-1,Bottom=-1 };
            if(DwmExtendFrameIntoClientArea(window,ref margins)!=0) { Disable(window); return false; }
            return true;
        } catch(DllNotFoundException) { return false; }
        catch(EntryPointNotFoundException) { return false; }
        catch(System.Security.SecurityException) { Disable(window); return false; }
        catch(InvalidCastException) { Disable(window); return false; }
        catch(FormatException) { Disable(window); return false; }
    }
    public static void Disable(IntPtr window) {
        try { int none=1; DwmSetWindowAttribute(window,38,ref none,4); var margins=new Margins(); DwmExtendFrameIntoClientArea(window,ref margins); }
        catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
    }
}
}
