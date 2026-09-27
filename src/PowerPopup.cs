using System;
using System.Drawing;
using System.Windows.Forms;

namespace BatteryBar {
// Native tooltip text is too short for a four-window table. This window never takes focus.
public class PowerPopup : Form {
    readonly Label summary = new Label();
    readonly Label note = new Label();
    readonly Label[,] cells = new Label[5, 3];
    bool arranging;
    bool acrylic;
    public bool AcrylicActive { get { return acrylic; } }
    public PowerPopup() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; Font = new Font("Microsoft YaHei UI", 9);
        BackColor = SystemColors.Info; ForeColor = SystemColors.InfoText;
        summary.Name = "Summary"; summary.Visible = false; Controls.Add(summary);
        for (int row = 0; row < 5; row++) for (int col = 0; col < 3; col++) {
            var cell = new Label { Name = "Cell" + row + col, TextAlign = ContentAlignment.MiddleLeft, Visible = false };
            cells[row,col] = cell; Controls.Add(cell);
        }
        cells[0,0].Text = "时间窗口"; cells[0,1].Text = "平均充电 / 有效时长"; cells[0,2].Text = "平均放电 / 有效时长";
        note.Name = "Note"; note.Visible = false;
        note.Text = "按有效采样时长加权；— 表示无数据。\n瞬时值为驱动最近一次报告，悬停时每 2 秒读取。";
        Controls.Add(note); ArrangeContent();
    }
    void ApplyBackdrop() {
        acrylic=PopupBackdrop.Apply(Handle);
        BackColor=acrylic?Color.Black:SystemColors.Info;
        ForeColor=acrylic?Color.WhiteSmoke:SystemColors.InfoText;
        // Keep labels for measurement; paint all text together in the form's buffer.
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using(var brush=new SolidBrush(ForeColor)) using(var format=new StringFormat { FormatFlags=StringFormatFlags.NoWrap }) {
            foreach(Control label in Controls) e.Graphics.DrawString(label.Text,label.Font,brush,new RectangleF(label.Left,label.Top,label.Width,label.Height),format);
        }
    }
    static Size Measure(Label label) {
        return TextRenderer.MeasureText(string.IsNullOrEmpty(label.Text) ? " " : label.Text, label.Font,
            new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix);
    }
    // Size rows from actual font metrics, including independent Windows text scaling.
    void ArrangeContent() {
        if (arranging || cells[4,2] == null) return;
        arranging = true; SuspendLayout();
        try {
            int pad = Math.Max(12, Font.Height), gap = Math.Max(8, Font.Height / 2);
            int[] widths = new int[3], heights = new int[5];
            for (int row = 0; row < 5; row++) for (int col = 0; col < 3; col++) {
                Size size = Measure(cells[row,col]);
                widths[col] = Math.Max(widths[col], size.Width + gap);
                heights[row] = Math.Max(heights[row], size.Height + gap / 2);
            }
            Size top = Measure(summary), bottom = Measure(note);
            int width = Math.Max(widths[0] + widths[1] + widths[2], Math.Max(top.Width, bottom.Width)) + 2 * pad;
            int y = pad;
            summary.SetBounds(pad, y, width - 2 * pad, top.Height + gap / 2); y += summary.Height + gap;
            for (int row = 0; row < 5; row++) {
                int x = pad;
                for (int col = 0; col < 3; col++) { cells[row,col].SetBounds(x, y, widths[col], heights[row]); x += widths[col]; }
                y += heights[row];
            }
            y += gap; note.SetBounds(pad, y, width - 2 * pad, bottom.Height + gap / 2);
            ClientSize = new Size(width, y + note.Height + pad);
        } finally { ResumeLayout(false); arranging = false; }
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ArrangeContent(); }
    protected override void OnShown(EventArgs e) { base.OnShown(e); ArrangeContent(); PlaceAt(Cursor.Position); }
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if(IsHandleCreated && Visible && (m.Msg==0x31E || m.Msg==0x31A || m.Msg==0x1A)) ApplyBackdrop();
    }
    protected override void OnVisibleChanged(EventArgs e) {
        base.OnVisibleChanged(e);
        if(!Visible && IsHandleCreated) { PopupBackdrop.Disable(Handle); acrylic=false; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; } }
    public void UpdateData(Reading reading, Preferences prefs, PowerHistory history, double now) {
        summary.Text = reading.Detail(prefs).Replace("充放电速率：", "瞬时充放电：").Replace("充电速率：", "瞬时充电：").Replace("放电速率：", "瞬时放电：");
        int[] windows = { 1, 5, 10, 30 };
        for (int i = 0; i < windows.Length; i++) { var avg = history.Average(now, windows[i]); cells[i+1,0].Text = windows[i] + " 分钟"; cells[i+1,1].Text = avg.Charge; cells[i+1,2].Text = avg.Discharge; }
        ArrangeContent(); if (Visible) { PlaceAt(Cursor.Position); Invalidate(); }
    }
    void PlaceAt(Point cursor) {
        var area = Screen.FromPoint(cursor).WorkingArea;
        Location = new Point(Math.Max(area.Left, Math.Min(cursor.X - Width / 2, area.Right - Width)), Math.Max(area.Top, Math.Min(cursor.Y - Height - 12, area.Bottom - Height)));
    }
    public void ShowAt(Point cursor) { ArrangeContent(); ApplyBackdrop(); PlaceAt(cursor); Show(); }
}
}
