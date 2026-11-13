using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace HeMiDark;

/// <summary>Drawing helpers over GDI+ (anti-aliasing, rounded shapes, icons from Segoe Fluent Icons).</summary>
static class Gfx
{
    public static readonly FontFamily IconFamily = PickIconFamily();

    static FontFamily PickIconFamily()
    {
        foreach (var name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            try { return new FontFamily(name); } catch (ArgumentException) { }
        }
        throw new InvalidOperationException("The font Segoe Fluent Icons / Segoe MDL2 Assets is missing.");
    }

    public static Color C(string hex) => ColorTranslator.FromHtml(hex);

    /// <summary>The colour <paramref name="top"/> with the opacity <paramref name="t"/> laid over <paramref name="under"/>.</summary>
    public static Color Mix(Color under, Color top, double t) => Color.FromArgb(
        255,
        (int)Math.Round(under.R + (top.R - under.R) * t),
        (int)Math.Round(under.G + (top.G - under.G) * t),
        (int)Math.Round(under.B + (top.B - under.B) * t));

    public static Bitmap New(int w, int h) =>
        new(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);

    public static Graphics G(Bitmap b)
    {
        var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        return g;
    }

    public static GraphicsPath RR(RectangleF r, float tl, float tr, float br, float bl)
    {
        var p = new GraphicsPath();
        if (tl > 0) p.AddArc(r.X, r.Y, 2 * tl, 2 * tl, 180, 90); else p.AddLine(r.X, r.Y, r.X, r.Y);
        if (tr > 0) p.AddArc(r.Right - 2 * tr, r.Y, 2 * tr, 2 * tr, 270, 90); else p.AddLine(r.Right, r.Y, r.Right, r.Y);
        if (br > 0) p.AddArc(r.Right - 2 * br, r.Bottom - 2 * br, 2 * br, 2 * br, 0, 90); else p.AddLine(r.Right, r.Bottom, r.Right, r.Bottom);
        if (bl > 0) p.AddArc(r.X, r.Bottom - 2 * bl, 2 * bl, 2 * bl, 90, 90); else p.AddLine(r.X, r.Bottom, r.X, r.Bottom);
        p.CloseFigure();
        return p;
    }

    public static GraphicsPath RR(RectangleF r, float rad) => RR(r, rad, rad, rad, rad);

    public static void FillRR(Graphics g, Color c, RectangleF r, float rad)
    {
        using var p = RR(r, rad);
        using var b = new SolidBrush(c);
        g.FillPath(b, p);
    }

    public static void FillRect(Graphics g, Color c, RectangleF r)
    {
        using var b = new SolidBrush(c);
        g.FillRectangle(b, r);
    }

    public static void FillCircle(Graphics g, Color c, float cx, float cy, float rad)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, cx - rad, cy - rad, 2 * rad, 2 * rad);
    }

    /// <summary>
    /// A piece of the window frame: fill + 1 px border on the chosen sides, rounded corners where two bordered sides meet.
    /// Sides without a border are drawn past the edge of the bitmap, so their border is cut off.
    /// </summary>
    public static Bitmap Frame(int w, int h, Color fill, Color border, bool top, bool right, bool bottom, bool left, int radius, bool rounded)
    {
        var b = New(w, h);
        using var g = G(b);
        float ext = radius * 4 + 8;
        float x0 = left ? 0 : -ext, y0 = top ? 0 : -ext, x1 = right ? w : w + ext, y1 = bottom ? h : h + ext;
        float rad = rounded ? radius : 0;
        float tl = top && left ? rad : 0, tr = top && right ? rad : 0, br = bottom && right ? rad : 0, bl = bottom && left ? rad : 0;
        using (var p = RR(new RectangleF(x0, y0, x1 - x0, y1 - y0), tl, tr, br, bl))
        using (var bb = new SolidBrush(border))
            g.FillPath(bb, p);
        static float In(float v) => Math.Max(0, v - 1);
        using (var p = RR(new RectangleF(x0 + 1, y0 + 1, x1 - x0 - 2, y1 - y0 - 2), In(tl), In(tr), In(br), In(bl)))
        using (var fb = new SolidBrush(fill))
            g.FillPath(fb, p);
        return b;
    }

    static GraphicsPath GlyphPath(char ch, float sizePx)
    {
        var p = new GraphicsPath();
        p.AddString(ch.ToString(), IconFamily, 0, sizePx, PointF.Empty, StringFormat.GenericTypographic);
        return p;
    }

    /// <summary>
    /// Draws an icon centred on (cx, cy), moved by <paramref name="ox"/> (optical correction). With
    /// <paramref name="refGlyph"/> it is centred by that glyph instead, so pairs of icons (e.g. volume / muted) do not jump.
    /// </summary>
    public static void Icon(Graphics g, char ch, float cx, float cy, float sizePx, Color c, char? refGlyph = null, float ox = 0)
    {
        using var p = GlyphPath(ch, sizePx);
        var (ix, iy) = InkCenter(refGlyph ?? ch, sizePx);
        using var m = new Matrix();
        m.Translate(cx + ox - ix, cy - iy);
        p.Transform(m);
        using var b = new SolidBrush(c);
        g.FillPath(b, p);
    }

    /// <summary>
    /// The centre of the really drawn "ink" of a glyph (with its anti-aliased edges), in the coordinates of its path.
    /// GetBounds does not match the pixels exactly, so the glyph is drawn and the coverage of its outer columns / rows measured.
    /// </summary>
    static (float x, float y) InkCenter(char ch, float sizePx)
    {
        using var p = GlyphPath(ch, sizePx);
        var bnd = p.GetBounds();
        const int pad = 4;
        float tx = pad - (float)Math.Floor(bnd.X), ty = pad - (float)Math.Floor(bnd.Y);
        int w = (int)Math.Ceiling(bnd.Width) + 2 * pad + 2, h = (int)Math.Ceiling(bnd.Height) + 2 * pad + 2;
        using var b = New(w, h);
        using (var g = G(b))
        using (var m = new Matrix())
        {
            m.Translate(tx, ty);
            p.Transform(m);
            g.FillPath(Brushes.Black, p);
        }
        var col = new int[w];
        var row = new int[h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int a = b.GetPixel(x, y).A;
                if (a > col[x]) col[x] = a;
                if (a > row[y]) row[y] = a;
            }
        static (float lo, float hi) Extent(int[] v)
        {
            int i0 = Array.FindIndex(v, a => a > 0), i1 = Array.FindLastIndex(v, a => a > 0);
            if (i0 < 0) return (0, 0);
            return (i0 + 1 - v[i0] / 255f, i1 + v[i1] / 255f);
        }
        var (l, r) = Extent(col);
        var (t, bt) = Extent(row);
        return ((l + r) / 2 - tx, (t + bt) / 2 - ty);
    }

    public static Font UiFont(float px, bool semibold = false)
    {
        string fam = semibold ? "Segoe UI Semibold" : "Segoe UI";
        try { return new Font(fam, px, FontStyle.Regular, GraphicsUnit.Pixel); }
        catch (ArgumentException) { return new Font("Segoe UI", px, semibold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
    }

    public static SizeF Measure(string text, Font f)
    {
        using var b = New(4, 4);
        using var g = G(b);
        return g.MeasureString(text, f, PointF.Empty, StringFormat.GenericTypographic);
    }

    /// <summary>Text aligned in a rectangle (vertically centred).</summary>
    public static void Text(Graphics g, string text, Font f, Color c, RectangleF r, StringAlignment h)
    {
        using var sf = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = h,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var b = new SolidBrush(c);
        g.DrawString(text, f, b, r, sf);
    }

    public static Bitmap Crop(Bitmap src, Rectangle r)
    {
        var b = New(r.Width, r.Height);
        using var g = Graphics.FromImage(b);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(src, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
        return b;
    }
}
