using System;
using System.Drawing;
using System.Windows.Forms;
using BatteryBar;
class PopupLayoutTests {
    [STAThread] static void Main() {
        Native.SetProcessDPIAware(); Application.EnableVisualStyles();
        var prefs = new Preferences();
        var old = new Preferences { Discharging = Color.WhiteSmoke.ToArgb() }; old.UpgradeColors();
        if (old.Discharging == Color.WhiteSmoke.ToArgb()) throw new Exception("Legacy default migration failed");
        var custom = new Preferences { Discharging = Color.Gold.ToArgb() }; custom.UpgradeColors();
        if (custom.Discharging != Color.Gold.ToArgb()) throw new Exception("Custom color changed");
        var r = new Reading { Available = true, Present = true, Percent = 92, Discharging = true, Rate = -9230, Time = 14400 };
        var history = new PowerHistory(); for(int t=0;t<=1800;t+=30) history.Add(t,r,65);
        foreach (float points in new float[]{9,11.25f,13.5f,18}) using(var form = new PowerPopup()) {
            form.Font = new Font("Microsoft YaHei UI",points); form.UpdateData(r,prefs,history,1800); form.Show(); Application.DoEvents();
            foreach(Control control in form.Controls) {
                var label = control as Label; if(label == null) continue;
                var size = TextRenderer.MeasureText(label.Text,label.Font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPrefix);
                if(label.Width < size.Width || label.Height < size.Height) throw new Exception("Clipped " + label.Name + " at " + points);
                if(!form.ClientRectangle.Contains(label.Bounds)) throw new Exception("Outside window");
                foreach(Control other in form.Controls) if(other != label && label.Bounds.IntersectsWith(other.Bounds)) throw new Exception("Overlapping " + label.Name);
            }
            using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,form.ClientRectangle); bitmap.Save("dist/popup-"+points.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"); }
            form.Hide();
        }
        Console.WriteLine("PASS: no clipping/overlap at 9, 11.25, 13.5, 18pt; default color migration and custom-color preservation.");
    }
}
