using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BatteryBar {
public enum IconStyle { Solid, Stacked }

// Simple rounded geometry and true alpha transparency, without highlights or animation.
public static class IconRenderer {
    static GraphicsPath Rounded(RectangleF r, float radius) {
        var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        path.AddArc(r.Left,r.Top,d,d,180,90); path.AddArc(r.Right-d,r.Top,d,d,270,90);
        path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); path.AddArc(r.Left,r.Bottom-d,d,d,90,90); path.CloseFigure(); return path;
    }
    static void BatterySurface(Graphics g, int size, Color tint, bool solid, int percent) {
        var saved = g.Save(); g.ScaleTransform(size/16f,size/16f); g.SmoothingMode=SmoothingMode.AntiAlias;
        var body = solid ? new RectangleF(.4f,1,13.6f,14) : new RectangleF(.4f,.5f,13.6f,4.5f);
        using(var terminal=Rounded(solid ? new RectangleF(13.7f,6.3f,1.5f,3.4f) : new RectangleF(13.7f,1.75f,1.5f,2),.45f))
        using(var brush=new SolidBrush(Color.FromArgb(225,tint))) g.FillPath(brush,terminal);
        using(var outline=Rounded(body,solid?2f:1.25f)) {
            if(solid) {
                // 84% opacity retains contrast on both light and dark taskbars.
                using(var surface=new SolidBrush(Color.FromArgb(215,tint))) g.FillPath(surface,outline);
            } else {
                using(var backdrop=new SolidBrush(Color.FromArgb(40,tint))) g.FillPath(backdrop,outline);
                if(percent>0) {
                    const float levelHeight = 2.2f;
                    var level=new RectangleF(1.7f,body.Top+(body.Height-levelHeight)/2,Math.Max(.8f,11*percent/100f),levelHeight);
                    using(var fill=Rounded(level,.65f)) using(var surface=new SolidBrush(Color.FromArgb(215,tint))) g.FillPath(surface,fill);
                }
            }
            using(var edge=new Pen(Color.FromArgb(235,tint),solid?.55f:.9f)) g.DrawPath(edge,outline);
        }
        g.Restore(saved);
    }

    public static Bitmap Render(string text, Color tint, IconStyle style, int size) {
        if (size < 16 || size > 256) throw new ArgumentOutOfRangeException("size");
        // A single master drawing keeps font/body proportions identical in the tray
        // and enlarged settings preview. Only the final raster size changes.
        using (var master = RenderMaster(text,tint,style,128)) {
            var result = new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(result)) using(var attributes=new ImageAttributes()) {
                graphics.CompositingMode=CompositingMode.SourceCopy;
                graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(master,new Rectangle(0,0,size,size),0,0,master.Width,master.Height,GraphicsUnit.Pixel,attributes);
                // Suppress the resampling filter's faint fringe in the reserved margin.
                if(style==IconStyle.Stacked) using(var clear=new SolidBrush(Color.Transparent)) graphics.FillRectangle(clear,0,size-1,size,1);
            }
            return result;
        }
    }
    static Bitmap RenderMaster(string text, Color tint, IconStyle style, int size) {
        int percent;
        if (!int.TryParse(text, out percent) || percent < 0 || percent > 100) { text = "?"; percent = -1; }
        else text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (size < 16 || size > 256) throw new ArgumentOutOfRangeException("size");
        if (!Enum.IsDefined(typeof(IconStyle), style)) throw new ArgumentOutOfRangeException("style");
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap)) {
            g.Clear(Color.Transparent);
            BatterySurface(g,size,tint,style==IconStyle.Solid,percent);
            float y, height;
            if (style == IconStyle.Solid) {
                y = 3; height = 10;
            } else {
                y = 7; height = 8;
            }
            // Filled icons use opaque contrasting digits so they also work on light taskbars.
            Color ink = style == IconStyle.Stacked ? tint : (.2126 * tint.R + .7152 * tint.G + .0722 * tint.B > 145 ? Color.Black : Color.White);
            using (var digitBrush = new SolidBrush(Color.FromArgb(255, ink)))
            using (var font = new FontFamily("Segoe UI"))
            using (var path = new GraphicsPath())
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone()) {
                path.AddString(text, font, (int)FontStyle.Bold, 32, PointF.Empty, format);
                RectangleF bounds = path.GetBounds();
                float targetWidth = (style == IconStyle.Solid ? 10.5f : 15.0f) * size / 16;
                float targetHeight = height * size / 16.0f;
                if(style==IconStyle.Stacked) targetHeight=Math.Min(targetHeight,size-1.5f-y*size/16.0f);
                float sy = targetHeight / bounds.Height;
                float sx = Math.Min(sy, targetWidth / bounds.Width);
                float center = (style == IconStyle.Solid ? 7.0f : 8.0f) * size / 16;
                using (var transform = new Matrix(sx, 0, 0, sy, center - bounds.Width * sx / 2 - bounds.X * sx, y * size / 16.0f - bounds.Y * sy)) path.Transform(transform);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(digitBrush, path);
            }
        }
        return bitmap;
    }

    public static Icon Draw(string text, Color tint, IconStyle style, int size) {
        using (var bitmap = Render(text, tint, style, size)) {
            IntPtr raw = bitmap.GetHicon();
            try { using (var borrowed = Icon.FromHandle(raw)) return (Icon)borrowed.Clone(); }
            finally { Native.DestroyIcon(raw); }
        }
    }
}
}
