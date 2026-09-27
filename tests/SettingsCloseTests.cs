using System;
using System.Windows.Forms;
using BatteryBar;
class TestSettings : SettingsForm {
    public TestSettings(Preferences p, Action<Preferences> save) : base(p,save) { }
    public void Escape() { ProcessDialogKey(Keys.Escape); }
    public void SystemClose() { var m=Message.Create(Handle,0x112,new IntPtr(0xF060),IntPtr.Zero); WndProc(ref m); }
}
class SettingsCloseTests {
    [STAThread] static void Main() {
        Native.SetProcessDPIAware(); Application.EnableVisualStyles();
        foreach(string action in new string[]{"cancel","escape","titlebar"}) {
            var p=new Preferences(); bool saved=false;
            using(var form=new TestSettings(p,delegate(Preferences updated){saved=true;})) {
                form.Show(); Application.DoEvents();
                ((ComboBox)form.Controls.Find("IconStyleSelector",true)[0]).SelectedIndex=1;
                if(action=="cancel") ((Button)form.Controls.Find("CancelSettings",true)[0]).PerformClick();
                else if(action=="escape") form.Escape(); else form.SystemClose();
                Application.DoEvents();
                if(!form.IsDisposed || form.Visible) throw new Exception(action+" did not close");
                if(saved || p.Style!=IconStyle.Solid) throw new Exception(action+" saved cancelled changes");
            }
        }
        Console.WriteLine("PASS: cancel, Esc, and titlebar close modeless settings without saving changes.");
    }
}
