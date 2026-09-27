using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace BatteryBar {
public class SettingsForm : Form {
    readonly TableLayoutPanel layout = new TableLayoutPanel();
    public SettingsForm(Preferences current, Action<Preferences> save) {
        Text = "BatteryBar 设置"; AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96,96);
        Font = new Font("Microsoft YaHei UI",10); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; AutoScroll = true;
        MaximumSize = Screen.FromPoint(Cursor.Position).WorkingArea.Size;
        layout.AutoSize = true; layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        layout.Padding = new Padding(20); layout.ColumnCount = 2;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(layout);
        var p = new Preferences { Style=current.Style, Low=current.Low, Full=current.Full, Seconds=current.Seconds, Charging=current.Charging, Charged=current.Charged, Discharging=current.Discharging, LowColor=current.LowColor, ColorVersion=current.ColorVersion };
        var startup = new CheckBox { Text="登录 Windows 时自动启动（无延迟任务）", AutoSize=true, Checked=Startup.Enabled }; AddWide(startup);
        var low=Number("低电量阈值（%）",1,99,p.Low); var full=Number("满电显示阈值（%）",2,100,p.Full);
        var seconds=Number("后台采样间隔（秒）",10,30,Math.Min(p.Seconds,30));
        var style = new ComboBox { Name="IconStyleSelector", DropDownStyle=ComboBoxStyle.DropDownList, Width=280, Dock=DockStyle.Fill };
        style.Items.AddRange(new object[]{"数字在电池内", "数字在电池外"}); style.SelectedIndex=(int)p.Style;
        AddPair("托盘图标样式",style);
        AddWide(new Label { Text="放大预览 · 充电 / 满电 / 放电 / 低电量", AutoSize=true });
        int[] colors={p.Charging,p.Charged,p.Discharging,p.LowColor};
        var preview = new Panel { Name="IconPreview", Height=96, Dock=DockStyle.Fill, BackColor=Color.FromArgb(32,32,32) };
        preview.Paint += delegate(object sender,PaintEventArgs e) {
            int size=Math.Min(preview.Height-24,preview.Width/4-24); size=Math.Max(16,Math.Min(128,size));
            int[] values={68,100,85,15};
            for(int i=0;i<4;i++) using(var bitmap=IconRenderer.Render(values[i].ToString(),Color.FromArgb(colors[i]),(IconStyle)style.SelectedIndex,size))
                e.Graphics.DrawImageUnscaled(bitmap,preview.Width*(2*i+1)/8-size/2,(preview.Height-size)/2);
        };
        AddWide(preview); style.SelectedIndexChanged += delegate { preview.Invalidate(); };
        string[] names={"充电中","已充满","放电中 / 待机","低电量"};
        for(int i=0;i<4;i++) {
            int n=i; var button=new Button { Text="更改颜色…", AutoSize=true, Dock=DockStyle.Fill, BackColor=Color.FromArgb(colors[i]), UseVisualStyleBackColor=false, Padding=new Padding(8,4,8,4) };
            button.ForeColor=button.BackColor.GetBrightness()>.55?Color.Black:Color.White;
            button.Click += delegate { using(var dialog=new ColorDialog { Color=Color.FromArgb(colors[n]), FullOpen=true }) if(dialog.ShowDialog(this)==DialogResult.OK) { colors[n]=dialog.Color.ToArgb(); button.BackColor=dialog.Color; button.ForeColor=dialog.Color.GetBrightness()>.55?Color.Black:Color.White; preview.Invalidate(); } };
            AddPair(names[i],button);
        }
        AddWide(new Label { AutoSize=true, Text="满电阈值仅控制显示，不限制硬件充电。\n悬停时约每 2 秒更新；预计时间可能随负载变化。" });
        var actions=new FlowLayoutPanel { AutoSize=true, FlowDirection=FlowDirection.RightToLeft, Dock=DockStyle.Fill };
        var cancel=new Button { Name="CancelSettings", Text="取消", AutoSize=true, CausesValidation=false, DialogResult=DialogResult.Cancel, Padding=new Padding(14,4,14,4) };
        // This window is opened with Show(), so DialogResult alone does not close it.
        cancel.Click += delegate { Close(); };
        var ok=new Button { Text="保存", AutoSize=true, Padding=new Padding(14,4,14,4) }; actions.Controls.Add(cancel); actions.Controls.Add(ok); AddWide(actions);
        AcceptButton=ok; CancelButton=cancel;
        ok.Click += delegate {
            p.Style=(IconStyle)style.SelectedIndex; p.Low=(int)low.Value; p.Full=(int)full.Value; p.Seconds=(int)seconds.Value;
            p.Charging=colors[0]; p.Charged=colors[1]; p.Discharging=colors[2]; p.LowColor=colors[3];
            try { p.Validate(); bool old=Startup.Enabled; if(old!=startup.Checked) Startup.Set(startup.Checked); try { p.Save(); } catch { if(old!=startup.Checked) Startup.Set(old); throw; } save(p); Close(); }
            catch(Exception ex) { MessageBox.Show(this,"保存失败："+ex.Message); }
        };
    }
    void AddWide(Control control) { int row=layout.RowCount++; layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); control.Margin=new Padding(4,6,4,6); layout.Controls.Add(control,0,row); layout.SetColumnSpan(control,2); }
    void AddPair(string text,Control control) {
        int row=layout.RowCount++; layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label=new Label { Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(4,8,20,8) };
        control.Margin=new Padding(4,6,4,6); layout.Controls.Add(label,0,row); layout.Controls.Add(control,1,row);
    }
    NumericUpDown Number(string label,int min,int max,int value) { var input=new NumericUpDown { Minimum=min,Maximum=max,Value=value,Width=120,Anchor=AnchorStyles.Left }; AddPair(label,input); return input; }
}
}
