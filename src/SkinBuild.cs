using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Security;
using System.Text;
using static HeMiDark.Gfx;

namespace HeMiDark;

record Imgs(string U, string O, string D);

/// <summary>Builds one skin (theme.xml + pictures + fonts) for one colour and scaling and packs it into a .vlt.</summary>
sealed class SkinBuild
{
    // --- Palette (Windows 11, dark mode) ---
    static readonly Color Bg = C("#202020");          // window / Mica
    static readonly Color FscBg = C("#2C2C2C");       // floating full-screen controller (flyout)
    static readonly Color Border = C("#3D3D3D");
    static readonly Color FscBorder = C("#464646");
    static readonly Color VideoBg = C("#141414");
    static readonly Color TextPri = C("#FFFFFF");
    static readonly Color TextSec = C("#C5C5C5");
    static readonly Color TextTer = C("#8E8E8E");
    static readonly Color Rail = C("#7A7A7A");
    static readonly Color ThumbOuter = C("#454545");
    static readonly Color ThumbRing = C("#555555");
    static readonly Color CloseRed = C("#C42B1C");
    static readonly Color Divider = C("#2D2D2D");

    readonly string fontDir, dir, imgDir, colorName;
    readonly double S;
    readonly int pct;
    readonly Color Acc;
    readonly StringBuilder head = new(), body = new();
    readonly HashSet<string> bitmaps = new();
    readonly FontFamily selawik;
    readonly PrivateFontCollection pfc = new();
    int ind;

    /// <param name="fontDir">src\Assets\Fonts</param>
    /// <param name="workDir">Folder for the unpacked skin (emptied first).</param>
    public SkinBuild(string fontDir, string workDir, int pct, Color accent, string colorName)
    {
        this.fontDir = fontDir;
        this.pct = pct;
        this.colorName = colorName;
        S = pct / 100.0;
        Acc = accent;
        dir = workDir;
        imgDir = Path.Combine(dir, "img");
        pfc.AddFontFile(Path.Combine(fontDir, "selawk.ttf"));
        selawik = pfc.Families[0];
    }

    int Px(double v) => (int)Math.Round(v * S);
    int R => Px(8);

    // ------------------------------------------------------------------ fonts

    int FontPx(double v) => Math.Max(9, Px(v));

    /// <summary>Height of a line of text as FreeType draws it in skins2 (ascender + descender).</summary>
    int LineH(int sizePx)
    {
        var st = FontStyle.Regular;
        double em = selawik.GetEmHeight(st);
        return (int)Math.Round((selawik.GetCellAscent(st) + selawik.GetCellDescent(st)) * sizePx / em);
    }

    readonly Dictionary<string, int> fonts = new();

    string Font(string id, double basePx, bool semibold = false)
    {
        int px = FontPx(basePx);
        if (fonts.TryAdd(id, px))
            head.AppendLine($"  <Font id=\"{id}\" file=\"fonts/{(semibold ? "selawksb.ttf" : "selawk.ttf")}\" size=\"{px}\"/>");
        return id;
    }

    int TextY(string fontId, int centerY) => centerY - LineH(fonts[fontId]) / 2;

    /// <summary>Width of a fixed title text (Selawik) with a small reserve – as little room as possible blocks dragging.</summary>
    int TitleTextW(string text)
    {
        using var f = new Font(selawik, fonts["f_title"], FontStyle.Regular, GraphicsUnit.Pixel);
        return (int)Math.Ceiling(Measure(text, f).Width) + Px(6);
    }

    // ------------------------------------------------------------------ XML

    static string Attrs((string k, object? v)[] a) => string.Concat(a.Where(x => x.v != null).Select(x =>
        $" {x.k}=\"{SecurityElement.Escape(Convert.ToString(x.v, CultureInfo.InvariantCulture))}\""));

    void Line(string s) => body.Append(' ', ind * 2).AppendLine(s);
    void El(string tag, params (string, object?)[] a) => Line($"<{tag}{Attrs(a)}/>");
    void Open(string tag, params (string, object?)[] a) { Line($"<{tag}{Attrs(a)}>"); ind++; }
    void Close(string tag) { ind--; Line($"</{tag}>"); }

    // ------------------------------------------------------------------ pictures and the atlas

    // All pictures are put together into an atlas at the end (SubBitmap). VLC loads every <Bitmap> through its whole
    // decoder chain (~15 ms each), so 190 separate PNGs made the start several seconds slower.
    readonly Dictionary<string, Bitmap> pending = new();

    string Bmp(string id, Bitmap b)
    {
        if (bitmaps.Add(id)) pending[id] = b;
        else b.Dispose();
        return id;
    }

    Size BmpSize(string id) => pending[id].Size;

    void WriteAtlases(StringBuilder xml)
    {
        int maxW = Math.Max(512, Px(1024));
        var all = pending.OrderByDescending(kv => kv.Value.Height).ThenByDescending(kv => kv.Value.Width).ToList();
        var normal = all.Where(kv => kv.Value.Width + 1 <= maxW).ToList();
        var wide = all.Where(kv => kv.Value.Width + 1 > maxW).ToList();   // the long slider thumbs
        WriteAtlas(xml, "atlas", normal, maxW);
        if (wide.Count > 0) WriteAtlas(xml, "atlas_wide", wide, wide.Max(kv => kv.Value.Width) + 1);
        foreach (var b in pending.Values) b.Dispose();
        pending.Clear();
    }

    /// <summary>Shelf packing (highest first), 1 px gap between the pictures.</summary>
    void WriteAtlas(StringBuilder xml, string name, List<KeyValuePair<string, Bitmap>> items, int W)
    {
        var placed = new List<(string id, Bitmap b, int x, int y)>();
        int x = 0, y = 0, shelf = 0;
        foreach (var (id, b) in items)
        {
            if (x + b.Width + 1 > W) { y += shelf; x = 0; shelf = 0; }
            placed.Add((id, b, x, y));
            x += b.Width + 1;
            shelf = Math.Max(shelf, b.Height + 1);
        }
        using var atlas = New(W, y + shelf);
        using (var g = Graphics.FromImage(atlas))
        {
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            foreach (var p in placed)
                g.DrawImage(p.b, new Rectangle(p.x, p.y, p.b.Width, p.b.Height), new Rectangle(0, 0, p.b.Width, p.b.Height), GraphicsUnit.Pixel);
        }
        atlas.Save(Path.Combine(imgDir, name + ".png"), ImageFormat.Png);
        xml.AppendLine($"  <Bitmap id=\"{name}\" file=\"img/{name}.png\" alphacolor=\"#FF00FF\">");
        foreach (var p in placed.OrderBy(p => p.id, StringComparer.Ordinal))
            xml.AppendLine($"    <SubBitmap id=\"{p.id}\" x=\"{p.x}\" y=\"{p.y}\" width=\"{p.b.Width}\" height=\"{p.b.Height}\"/>");
        xml.AppendLine("  </Bitmap>");
    }

    string Solid(string id, Color c, int w = 2, int h = 2)
    {
        var b = New(w, h);
        using (var g = Graphics.FromImage(b)) g.Clear(c);
        return Bmp(id, b);
    }

    // ------------------------------------------------------------------ controls

    void Img(string image, int x, int y, int w, int h, string lt = "lefttop", string rb = "lefttop",
        string? action = null, string? action2 = null, string? visible = null, bool kx = false, bool ky = false,
        bool art = false, string? help = null)
        => El("Image", ("x", x), ("y", y), ("width", w), ("height", h), ("image", image), ("resize", "scale"),
            ("lefttop", lt), ("rightbottom", rb), ("action", action), ("action2", action2), ("visible", visible),
            ("xkeepratio", kx ? "true" : null), ("ykeepratio", ky ? "true" : null), ("art", art ? "true" : null), ("help", help));

    void Btn(Imgs im, int x, int y, string action, string tip, string lt = "lefttop", string rb = "lefttop",
        string? visible = null, bool kx = false, bool ky = false)
        => El("Button", ("x", x), ("y", y), ("up", im.U), ("over", im.O), ("down", im.D), ("action", action),
            ("tooltiptext", tip), ("lefttop", lt), ("rightbottom", rb), ("visible", visible),
            ("xkeepratio", kx ? "true" : null), ("ykeepratio", ky ? "true" : null));

    void Chk(string state, Imgs off, Imgs on, string action1, string action2, string tip1, string tip2,
        int x, int y, string lt = "lefttop", string rb = "lefttop", string? visible = null)
        => El("Checkbox", ("x", x), ("y", y), ("state", state),
            ("up1", off.U), ("over1", off.O), ("down1", off.D),
            ("up2", on.U), ("over2", on.O), ("down2", on.D),
            ("action1", action1), ("action2", action2), ("tooltiptext1", tip1), ("tooltiptext2", tip2),
            ("lefttop", lt), ("rightbottom", rb), ("visible", visible));

    void Txt(string text, string font, Color color, int x, int y, int w, string align = "left",
        string lt = "lefttop", string rb = "lefttop", string? visible = null, bool kx = false, bool ky = false)
        => El("Text", ("x", x), ("y", y), ("width", w), ("text", text), ("font", font), ("color", Hex(color)),
            ("alignment", align), ("scrolling", "none"), ("lefttop", lt), ("rightbottom", rb), ("visible", visible),
            ("xkeepratio", kx ? "true" : null), ("ykeepratio", ky ? "true" : null));

    void SliderH(string value, Imgs th, int x, int y, int len, string tip, string lt = "lefttop",
        string rb = "lefttop", string? visible = null)
        => El("Slider", ("x", x), ("y", y), ("points", $"(0,0),({len},0)"), ("value", value),
            ("up", th.U), ("over", th.O), ("down", th.D), ("thickness", Px(10)), ("tooltiptext", tip),
            ("lefttop", lt), ("rightbottom", rb), ("visible", visible));

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ------------------------------------------------------------------ pictures of the controls

    float IconPx => Px(16);

    // VLC 3 has no menu of its own for subtitles – the track is chosen in the main menu (Subtitle › Sub Track).
    const string SubTip = "Subtitles: menu › Subtitle › Sub Track (V cycles them)";

    /// <summary>"Subtle" button (Windows 11 command bar): an icon, a soft fill under the mouse.</summary>
    Imgs IconBtn(string id, char glyph, int w, int h, Color bg, Color? iconColor = null, bool active = false,
        char? refGlyph = null, float? iconPx = null)
    {
        var ids = new string[3];
        for (int s = 0; s < 3; s++)
        {
            var b = New(w, h);
            using (var g = G(b))
            {
                g.Clear(bg);
                Color fill = s switch
                {
                    1 => Mix(bg, Color.White, 0.07),
                    2 => Mix(bg, Color.White, 0.04),
                    _ => active ? Mix(bg, Color.White, 0.05) : bg,
                };
                if (fill != bg) FillRR(g, fill, new RectangleF(0, 0, w, h), Px(4));
                Color ic = iconColor ?? TextPri;
                if (s == 2) ic = Mix(fill, ic, 0.75);
                Icon(g, glyph, w / 2f, h / 2f, iconPx ?? IconPx, ic, refGlyph);
            }
            ids[s] = Bmp($"{id}_{"uod"[s]}", b);
        }
        return new Imgs(ids[0], ids[1], ids[2]);
    }

    /// <summary>Round accent button (play / pause).</summary>
    Imgs AccentBtn(string id, char glyph, int d, Color bg, float opticalDx = 0)
    {
        var ids = new string[3];
        for (int s = 0; s < 3; s++)
        {
            var b = New(d, d);
            using (var g = G(b))
            {
                g.Clear(bg);
                Color fill = s switch { 1 => Mix(bg, Acc, 0.9), 2 => Mix(bg, Acc, 0.8), _ => Acc };
                FillCircle(g, fill, d / 2f, d / 2f, d / 2f - 0.5f);
                Color ic = s == 2 ? Mix(fill, Color.Black, 0.55) : Color.Black;
                Icon(g, glyph, d / 2f, d / 2f, Px(18), ic, ox: opticalDx);
            }
            ids[s] = Bmp($"{id}_{"uod"[s]}", b);
        }
        return new Imgs(ids[0], ids[1], ids[2]);
    }

    /// <summary>Title bar button (46 × 32 like Windows 11), with the top edge of the window.</summary>
    Imgs CaptionBtn(string id, char glyph, int w, int h, bool close, bool rounded, Color? iconColor = null, char? refGlyph = null)
    {
        var ids = new string[3];
        for (int s = 0; s < 3; s++)
        {
            Color fill = close
                ? s switch { 1 => CloseRed, 2 => Mix(Bg, CloseRed, 0.85), _ => Bg }
                : s switch { 1 => Mix(Bg, Color.White, 0.07), 2 => Mix(Bg, Color.White, 0.04), _ => Bg };
            var b = Frame(w, h, fill, Border, top: true, right: close, bottom: false, left: false, R, rounded);
            using (var g = G(b))
            {
                Color ic = iconColor ?? TextPri;
                if (!close && s == 2) ic = Mix(fill, ic, 0.75);
                Icon(g, glyph, w / 2f, h / 2f, Px(10), ic, refGlyph);
            }
            ids[s] = Bmp($"{id}_{"uod"[s]}", b);
        }
        return new Imgs(ids[0], ids[1], ids[2]);
    }

    /// <summary>Button with text (standard or accent), with an optional icon.</summary>
    Imgs TextBtn(string id, string text, int h, Color bg, bool accent, char? glyph = null)
    {
        using var f = UiFont(Px(14));
        var ts = Measure(text, f);
        int iconW = glyph != null ? Px(16) + Px(8) : 0;
        int w = (int)Math.Ceiling(ts.Width) + iconW + Px(28);
        var ids = new string[3];
        for (int s = 0; s < 3; s++)
        {
            var b = New(w, h);
            using (var g = G(b))
            {
                g.Clear(bg);
                var rect = new RectangleF(0, 0, w, h);
                Color fill, fg;
                if (accent)
                {
                    fill = s switch { 1 => Mix(bg, Acc, 0.9), 2 => Mix(bg, Acc, 0.8), _ => Acc };
                    fg = s == 2 ? Mix(fill, Color.Black, 0.55) : Color.Black;
                    FillRR(g, fill, rect, Px(4));
                }
                else
                {
                    fill = s switch { 1 => Mix(bg, Color.White, 0.084), 2 => Mix(bg, Color.White, 0.033), _ => Mix(bg, Color.White, 0.061) };
                    fg = s == 2 ? Mix(fill, TextPri, 0.786) : TextPri;
                    FillRR(g, Mix(bg, Color.White, 0.093), rect, Px(4));
                    FillRR(g, fill, new RectangleF(1, 1, w - 2, h - 2), Math.Max(1, Px(4) - 1));
                }
                float x = Px(14);
                if (glyph != null)
                {
                    Icon(g, glyph.Value, x + Px(8), h / 2f, Px(16), fg);
                    x += iconW;
                }
                Text(g, text, f, fg, new RectangleF(x, 0, w - x, h), StringAlignment.Near);
            }
            ids[s] = Bmp($"{id}_{"uod"[s]}", b);
        }
        return new Imgs(ids[0], ids[1], ids[2]);
    }

    /// <summary>
    /// Thumb of a slider. The picture is centred on the point of the slider's curve, so it is a "long" picture:
    /// the thumb in the middle and an accent bar from the thumb to the left (or down) – that is the filled part of the slider.
    /// The part of the bar that reaches too far is hidden by "covers" drawn after the slider.
    /// </summary>
    Imgs Thumb(string id, int barLen, bool vertical)
    {
        int r = Px(10), bt = 2 * Math.Max(1, Px(2));
        var ids = new string[3];
        float[] inner = { Px(6), Px(7), Px(5) };
        for (int s = 0; s < 3; s++)
        {
            Bitmap b = vertical ? New(2 * r, 2 * barLen) : New(2 * barLen, 2 * r);
            using (var g = G(b))
            {
                float cx = vertical ? r : barLen, cy = vertical ? barLen : r;
                if (vertical) FillRect(g, Acc, new RectangleF(r - bt / 2f, barLen, bt, barLen));
                else FillRect(g, Acc, new RectangleF(0, r - bt / 2f, barLen, bt));
                FillCircle(g, ThumbRing, cx, cy, r - 0.5f);
                FillCircle(g, ThumbOuter, cx, cy, r - 1.5f);
                FillCircle(g, Acc, cx, cy, inner[s]);
            }
            ids[s] = Bmp($"{id}_{"uod"[s]}", b);
        }
        return new Imgs(ids[0], ids[1], ids[2]);
    }

    /// <summary>Windows 11 toggle switch with the label On / Off.</summary>
    (Imgs off, Imgs on) Toggle(string id, Color bg)
    {
        int tw = Px(40), th = Px(20), w = Px(120), h = Px(32);
        using var f = UiFont(Px(14));
        Imgs Make(bool on)
        {
            var ids = new string[3];
            for (int s = 0; s < 3; s++)
            {
                var b = New(w, h);
                using (var g = G(b))
                {
                    g.Clear(bg);
                    float y0 = (h - th) / 2f;
                    var pill = new RectangleF(0, y0, tw, th);
                    float knobR = s == 1 ? Px(7) : Px(6);
                    if (on)
                    {
                        FillRR(g, s switch { 1 => Mix(bg, Acc, 0.9), 2 => Mix(bg, Acc, 0.8), _ => Acc }, pill, th / 2f);
                        FillCircle(g, Color.Black, tw - th / 2f, y0 + th / 2f, knobR);
                    }
                    else
                    {
                        FillRR(g, Mix(bg, Color.White, 0.6), pill, th / 2f);
                        var inner = new RectangleF(1, y0 + 1, tw - 2, th - 2);
                        FillRR(g, Mix(bg, Color.White, s == 1 ? 0.08 : 0.04), inner, th / 2f - 1);
                        FillCircle(g, Mix(bg, Color.White, 0.8), th / 2f, y0 + th / 2f, knobR);
                    }
                    Text(g, on ? "On" : "Off", f, TextPri, new RectangleF(tw + Px(12), 0, w, h), StringAlignment.Near);
                }
                ids[s] = Bmp($"{id}{(on ? 1 : 0)}_{"uod"[s]}", b);
            }
            return new Imgs(ids[0], ids[1], ids[2]);
        }
        return (Make(false), Make(true));
    }

    // ------------------------------------------------------------------ build

    public string Build(string vltPath)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(imgDir);
        Directory.CreateDirectory(Path.Combine(dir, "fonts"));
        foreach (var f in new[] { "selawk.ttf", "selawksb.ttf", "Selawik-LICENSE.txt" })
            File.Copy(Path.Combine(fontDir, f), Path.Combine(dir, "fonts", f));

        Font("f_title", 12);
        Font("f_time", 12);
        Font("f_list", 13);
        Font("f_hint", 13);

        ind = 1;
        BuildMain();
        BuildPlaylist();
        BuildEqualizer();
        BuildFsc();

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.AppendLine("<!DOCTYPE Theme PUBLIC \"-//VideoLAN//DTD VLC Skins V2.0//EN\" \"skin.dtd\">");
        xml.AppendLine("<Theme version=\"2.0\" tooltipfont=\"defaultfont\" magnet=\"12\" alpha=\"255\" movealpha=\"255\">");
        xml.AppendLine($"  <ThemeInfo name=\"HeMi Dark {SecurityElement.Escape(colorName)} ({pct}%)\" author=\"HeMi\" email=\"\" webpage=\"\"/>");
        xml.Append(head);
        WriteAtlases(xml);
        xml.Append(body);
        xml.AppendLine("</Theme>");
        File.WriteAllText(Path.Combine(dir, "theme.xml"), xml.ToString(), new UTF8Encoding(false));

        Directory.CreateDirectory(Path.GetDirectoryName(vltPath)!);
        if (File.Exists(vltPath)) File.Delete(vltPath);
        var psi = new ProcessStartInfo("tar", $"-czf \"{vltPath}\" -C \"{dir}\" theme.xml img fonts")
        { UseShellExecute = false, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException("tar failed: " + err);
        return vltPath;
    }

    // ------------------------------------------------------------------ main window

    void BuildMain()
    {
        int W = Px(854), TB = Px(32), VH = Px(480), CB = Px(92), H = TB + VH + CB;
        int capW = Px(46), bw = Px(40), gap = Px(4);
        int r = Px(10), bt = 2 * Math.Max(1, Px(2));
        const string Max = "main.isMaximized()", NotMax = "not main.isMaximized()";
        const string RT = "righttop", RB = "rightbottom", LB = "leftbottom";

        Open("Window", ("id", "main"), ("position", "Center"), ("dragdrop", "true"), ("playondrop", "true"));
        Open("Layout", ("id", "main_layout"), ("width", W), ("height", H), ("minwidth", Px(700)), ("maxwidth", 99999),
            ("minheight", TB + CB + Px(140)), ("maxheight", 99999));

        // ---- title bar
        Open("Panel", ("x", 0), ("y", 0), ("width", W), ("height", TB), ("rightbottom", RT));
        string tbL = Bmp("tb_l", Frame(R, TB, Bg, Border, true, false, false, true, R, true));
        string tbLq = Bmp("tb_lq", Frame(R, TB, Bg, Border, true, false, false, true, R, false));
        string tbC = Bmp("tb_c", Frame(2, TB, Bg, Border, true, false, false, false, R, false));
        string tbR = Bmp("tb_r", Frame(R, TB, Bg, Border, true, true, false, false, R, true));
        string tbRq = Bmp("tb_rq", Frame(R, TB, Bg, Border, true, true, false, false, R, false));
        Img(tbL, 0, 0, R, TB, action: "move", action2: "main.maximize()", visible: NotMax);
        Img(tbLq, 0, 0, R, TB, action: "move", action2: "main.unmaximize()", visible: Max);
        Img(tbC, R, 0, W - 2 * R, TB, rb: RT, action: "move", action2: "main.maximize()", visible: NotMax);
        Img(tbC, R, 0, W - 2 * R, TB, rb: RT, action: "move", action2: "main.unmaximize()", visible: Max);
        Img(tbR, W - R, 0, R, TB, lt: RT, rb: RT, action: "move", action2: "main.maximize()", visible: NotMax);
        Img(tbRq, W - R, 0, R, TB, lt: RT, rb: RT, action: "move", action2: "main.unmaximize()", visible: Max);

        Btn(IconBtn("t_menu", '', Px(40), Px(24), Bg, iconPx: Px(14)), Px(6), Px(4), "dialogs.popup()", "VLC menu");
        // A Text control of skins2 takes the mouse in its whole rectangle (the window cannot be grabbed there), so the
        // title has a fixed width and does not grow with the window – the rest of the bar stays free for dragging.
        int titleX = Px(54), titleW = Math.Min(Px(320), W - titleX - 4 * capW - Px(8));
        Txt("$N", "f_title", TextPri, titleX, TextY("f_title", TB / 2), titleW, visible: "not vlc.isStopped");
        Txt("VLC media player", "f_title", TextSec, titleX, TextY("f_title", TB / 2), TitleTextW("VLC media player"), visible: "vlc.isStopped");

        Chk("vlc.isOnTop",
            CaptionBtn("cap_pin0", '', capW, TB, false, false),
            CaptionBtn("cap_pin1", '', capW, TB, false, false, Acc, ''),
            "vlc.onTop()", "vlc.onTop()", "Always on top", "Not always on top", W - 4 * capW, 0, RT, RT);
        Btn(CaptionBtn("cap_min", '', capW, TB, false, false), W - 3 * capW, 0, "vlc.minimize()", "Minimize", RT, RT);
        Btn(CaptionBtn("cap_max", '', capW, TB, false, false), W - 2 * capW, 0, "main.maximize()", "Maximize", RT, RT, NotMax);
        Btn(CaptionBtn("cap_res", '', capW, TB, false, false), W - 2 * capW, 0, "main.unmaximize()", "Restore", RT, RT, Max);
        Btn(CaptionBtn("cap_close", '', capW, TB, true, true), W - capW, 0, "vlc.quit()", "Close", RT, RT, NotMax);
        Btn(CaptionBtn("cap_closeq", '', capW, TB, true, false), W - capW, 0, "vlc.quit()", "Close", RT, RT, Max);
        Close("Panel");

        // ---- video
        Open("Panel", ("x", 0), ("y", TB), ("width", W), ("height", VH), ("rightbottom", RB));
        string border = Solid("border", Border);
        Img(Solid("video_bg", VideoBg), 1, 0, W - 2, VH, rb: RB);
        Img(border, 0, 0, 1, VH, rb: LB);
        Img(border, W - 1, 0, 1, VH, lt: RT, rb: RB);

        int art = Px(200);
        Img(ArtPlaceholder(art), (W - art) / 2, (VH - art) / 2, art, art, kx: true, ky: true, art: true,
            visible: "not vlc.isStopped and not vlc.hasVout");

        var open = TextBtn("v_open", "Open file", Px(36), VideoBg, accent: true, glyph: '');
        int openW = BmpSize(open.U).Width;
        Btn(open, (W - openW) / 2, VH / 2 - Px(30), "dialogs.file()", "Open file (Ctrl+O)", visible: "vlc.isStopped", kx: true, ky: true);
        int hintW = Px(360);
        Txt("or drop a file here", "f_hint", TextTer, (W - hintW) / 2, VH / 2 + Px(18), hintW, "center",
            visible: "vlc.isStopped", kx: true, ky: true);

        El("Video", ("x", 1), ("y", 0), ("width", W - 2), ("height", VH), ("rightbottom", RB), ("autoresize", "false"),
            ("visible", "vlc.hasVout"));
        Close("Panel");

        // ---- control bar
        Open("Panel", ("x", 0), ("y", TB + VH), ("width", W), ("height", CB), ("lefttop", LB), ("rightbottom", RB));
        Img(Bmp("cb_l", Frame(R, CB, Bg, Border, false, false, true, true, R, true)), 0, 0, R, CB, action: "move", visible: NotMax);
        Img(Bmp("cb_lq", Frame(R, CB, Bg, Border, false, false, true, true, R, false)), 0, 0, R, CB, action: "move", visible: Max);
        Img(Bmp("cb_c", Frame(2, CB, Bg, Border, false, false, true, false, R, false)), R, 0, W - 2 * R, CB, rb: RT, action: "move");
        Img(Bmp("cb_re", Frame(R, CB - R, Bg, Border, false, true, false, false, R, false)), W - R, 0, R, CB - R, RT, RT, action: "resizeE");
        Img(Bmp("cb_br", Frame(R, R, Bg, Border, false, true, true, false, R, true)), W - R, CB - R, R, R, RT, RT, action: "resizeSE", visible: NotMax);
        Img(Bmp("cb_brq", Frame(R, R, Bg, Border, false, true, true, false, R, false)), W - R, CB - R, R, R, RT, RT, action: "resizeSE", visible: Max);
        Img(Bmp("cb_s", Frame(2, Px(4), Bg, Border, false, false, true, false, R, false)), R, CB - Px(4), W - 2 * R, Px(4), rb: RT, action: "resizeS");

        string rail = Solid("rail", Rail, 2, bt);

        // row 1: seek bar (the thumb's bar must reach across a maximized 5120 px window)
        int pad = Px(14), timeW = Px(56), c1 = Px(20);
        int rs = pad + timeW + Px(6);
        Img(rail, rs, c1 - bt / 2, W - 2 * rs, bt, rb: RT);
        SliderH("time", Thumb("th_seek", Math.Max(Px(4000), 7680), false), rs + r, c1, W - 2 * rs - 2 * r, "$T / $D", rb: RT,
            visible: "not vlc.isStopped");
        Img(Bmp("cover_seek", Frame(rs, 2 * r, Bg, Border, false, false, false, true, R, false)), 0, c1 - r, rs, 2 * r);
        Txt("$T", "f_time", TextSec, pad, TextY("f_time", c1), timeW, "left");
        Txt("$D", "f_time", TextSec, W - pad - timeW, TextY("f_time", c1), timeW, "right", RT, RT);

        // row 2 left: mute + volume
        int c2 = Px(64), by = c2 - bw / 2, mx = Px(8);
        int vrs = mx + bw + Px(4), vL = Px(88);
        Img(rail, vrs, c2 - bt / 2, vL + 2 * r, bt);
        SliderH("volume", Thumb("th_vol", vL + r, false), vrs + r, c2, vL, "Volume: $V%");
        Img(Bmp("cover_vol", Frame(vrs, 2 * r, Bg, Border, false, false, false, true, R, false)), 0, c2 - r, vrs, 2 * r);
        Chk("vlc.isMute",
            IconBtn("m_vol", '', bw, bw, Bg),
            IconBtn("m_mute", '', bw, bw, Bg, refGlyph: ''),
            "vlc.mute()", "vlc.mute()", "Mute (M)", "Unmute (M)", mx, by);

        // row 2 middle: playback
        TransportGroup("m", Bg, W, c2, kx: true);

        // row 2 right
        int x = W - mx - bw;
        Btn(IconBtn("m_fs", '', bw, bw, Bg), x, by, "vlc.fullscreen()", "Full screen (F)", RT, RT);
        x -= bw + gap;
        Btn(IconBtn("m_video", '', bw, bw, Bg), x, by, "dialogs.videoPopup()", "Video: aspect ratio, crop, deinterlace…", RT, RT);
        x -= bw + gap;
        Chk("eqwin.isVisible", IconBtn("m_eq0", '', bw, bw, Bg), IconBtn("m_eq1", '', bw, bw, Bg, Acc, true),
            "eqwin.show()", "eqwin.hide()", "Equalizer", "Hide equalizer", x, by, RT, RT);
        x -= bw + gap;
        Chk("plwin.isVisible", IconBtn("m_pl0", '', bw, bw, Bg), IconBtn("m_pl1", '', bw, bw, Bg, Acc, true),
            "plwin.show()", "plwin.hide()", "Playlist", "Hide playlist", x, by, RT, RT);
        x -= bw + gap + Px(8);
        Btn(IconBtn("m_sub", '', bw, bw, Bg), x, by, "dialogs.popup()", SubTip, RT, RT);
        x -= bw + gap;
        Btn(IconBtn("m_audio", '', bw, bw, Bg), x, by, "dialogs.audioPopup()", "Audio track", RT, RT);
        Close("Panel");

        Close("Layout");
        Close("Window");
    }

    /// <summary>Stop · previous · play/pause · next · repeat, centred.</summary>
    void TransportGroup(string p, Color bg, int W, int c2, bool kx)
    {
        int bw = Px(40), gap = Px(4), pd = Px(44);
        int cw = 4 * bw + pd + 4 * gap, by = (pd - bw) / 2;
        Open("Panel", ("x", (W - cw) / 2), ("y", c2 - pd / 2), ("width", cw), ("height", pd), ("xkeepratio", kx ? "true" : null));
        int x = 0;
        Btn(IconBtn($"{p}_stop", '', bw, bw, bg, iconPx: Px(13)), x, by, "vlc.stop()", "Stop (S)");
        x += bw + gap;
        Btn(IconBtn($"{p}_prev", '', bw, bw, bg), x, by, "playlist.previous()", "Previous (P)");
        x += bw + gap;
        // the "play" triangle has its centre of mass left of the centre of its outline – it is moved a little to the right
        Chk("vlc.isPlaying", AccentBtn($"{p}_play", '', pd, bg, (float)(S * 1.25)), AccentBtn($"{p}_pause", '', pd, bg),
            "vlc.play()", "vlc.pause()", "Play (Space)", "Pause (Space)", x, 0);
        x += pd + gap;
        Btn(IconBtn($"{p}_next", '', bw, bw, bg), x, by, "playlist.next()", "Next (N)");
        x += bw + gap;
        Btn(IconBtn($"{p}_rep0", '', bw, bw, bg, TextSec), x, by, "playlist.setLoop(true)", "Repeat: off",
            visible: "not playlist.isLoop and not playlist.isRepeat");
        Btn(IconBtn($"{p}_rep1", '', bw, bw, bg, Acc, true), x, by, "playlist.setLoop(false);playlist.setRepeat(true)",
            "Repeat: all", visible: "playlist.isLoop");
        Btn(IconBtn($"{p}_rep2", '', bw, bw, bg, Acc, true, ''), x, by, "playlist.setRepeat(false)",
            "Repeat: one", visible: "playlist.isRepeat");
        Close("Panel");
    }

    string ArtPlaceholder(int size)
    {
        var b = New(size, size);
        using (var g = G(b))
        {
            FillRR(g, C("#262626"), new RectangleF(0, 0, size, size), Px(8));
            Icon(g, '', size / 2f, size / 2f, size * 0.36f, C("#5A5A5A"));
        }
        return Bmp("art_default", b);
    }

    // ------------------------------------------------------------------ playlist

    void BuildPlaylist()
    {
        int W = Px(440), TB = Px(32), BB = Px(52), H = Px(520), BH = H - TB - BB;
        int capW = Px(46), bw = Px(40), gap = Px(4);
        const string RT = "righttop", RB = "rightbottom", LB = "leftbottom";

        Open("Window", ("id", "plwin"), ("position", "East"), ("xmargin", Px(24)), ("visible", "false"), ("dragdrop", "true"), ("playondrop", "false"));
        Open("Layout", ("id", "pl_layout"), ("width", W), ("height", H), ("minwidth", Px(340)), ("maxwidth", 99999),
            ("minheight", Px(240)), ("maxheight", 99999));

        Open("Panel", ("x", 0), ("y", 0), ("width", W), ("height", TB), ("rightbottom", RT));
        Img("tb_l", 0, 0, R, TB, action: "move");
        Img("tb_c", R, 0, W - 2 * R, TB, rb: RT, action: "move");
        Img("tb_r", W - R, 0, R, TB, RT, RT, action: "move");
        Txt("Playlist", "f_title", TextPri, Px(14), TextY("f_title", TB / 2), TitleTextW("Playlist"));
        Btn(new Imgs("cap_close_u", "cap_close_o", "cap_close_d"), W - capW, 0, "plwin.hide()", "Close", RT, RT);
        Close("Panel");

        Open("Panel", ("x", 0), ("y", TB), ("width", W), ("height", BH), ("rightbottom", RB));
        Img(Solid("pl_bg", Bg), 1, 0, W - 2, BH, rb: RB);
        Img("border", 0, 0, 1, BH, rb: LB);
        Img("border", W - 1, 0, 1, BH, lt: RT, rb: RB);
        int sbw = Px(6), ph = Px(40), lp = Px(6);
        string[] pid = new string[3];
        for (int s = 0; s < 3; s++)
        {
            var b = New(sbw, ph);
            using (var g = G(b)) FillRR(g, s == 0 ? C("#5C5C5C") : C("#8A8A8A"), new RectangleF(0, 0, sbw, ph), sbw / 2f);
            pid[s] = Bmp($"pl_sb_{"uod"[s]}", b);
        }
        Open("Playtree", ("id", "playtree"), ("x", lp), ("y", Px(4)), ("width", W - lp - Px(18)), ("height", BH - Px(8)),
            ("rightbottom", RB), ("font", "f_list"), ("flat", "true"),
            ("bgcolor1", Hex(Bg)), ("bgcolor2", Hex(Bg)), ("fgcolor", Hex(C("#E4E4E4"))),
            ("playcolor", Hex(Acc)), ("selcolor", Hex(C("#383838"))));
        int sx = W - Px(10), sy = Px(4) + ph / 2, sh = BH - Px(8) - ph;
        El("Slider", ("x", sx), ("y", sy), ("points", $"(0,{sh}),(0,0)"), ("up", pid[0]), ("over", pid[1]), ("down", pid[2]),
            ("thickness", Px(8)), ("lefttop", RT), ("rightbottom", RB));
        Close("Playtree");
        Close("Panel");

        Open("Panel", ("x", 0), ("y", TB + BH), ("width", W), ("height", BB), ("lefttop", LB), ("rightbottom", RB));
        Img(Bmp("pb_l", Frame(R, BB, Bg, Border, false, false, true, true, R, true)), 0, 0, R, BB, action: "move");
        Img(Bmp("pb_c", Frame(2, BB, Bg, Border, false, false, true, false, R, false)), R, 0, W - 2 * R, BB, rb: RT, action: "move");
        Img(Bmp("pb_re", Frame(R, BB - R, Bg, Border, false, true, false, false, R, false)), W - R, 0, R, BB - R, RT, RT, action: "resizeE");
        Img(Bmp("pb_br", Frame(R, R, Bg, Border, false, true, true, false, R, true)), W - R, BB - R, R, R, RT, RT, action: "resizeSE");
        Img(Bmp("pb_s", Frame(2, Px(4), Bg, Border, false, false, true, false, R, false)), R, BB - Px(4), W - 2 * R, Px(4), rb: RT, action: "resizeS");
        Img(Solid("divider", Divider), 1, 0, W - 2, 1, rb: RT);

        int by = (BB - bw) / 2, x = Px(8);
        Btn(IconBtn("p_add", '', bw, bw, Bg), x, by, "playlist.add()", "Add files");
        x += bw + gap;
        Btn(IconBtn("p_del", '', bw, bw, Bg), x, by, "playlist.del()", "Remove selected");
        x += bw + gap;
        Btn(IconBtn("p_sort", '', bw, bw, Bg), x, by, "playlist.sort()", "Sort by name");
        x += bw + gap + Px(12);
        Chk("playlist.isRandom", IconBtn("p_shuf0", '', bw, bw, Bg, TextSec), IconBtn("p_shuf1", '', bw, bw, Bg, Acc, true),
            "playlist.setRandom(true)", "playlist.setRandom(false)", "Shuffle: off", "Shuffle: on", x, by);
        x += bw + gap;
        Btn(new Imgs("m_rep0_u", "m_rep0_o", "m_rep0_d"), x, by, "playlist.setLoop(true)", "Repeat: off",
            visible: "not playlist.isLoop and not playlist.isRepeat");
        Btn(new Imgs("m_rep1_u", "m_rep1_o", "m_rep1_d"), x, by, "playlist.setLoop(false);playlist.setRepeat(true)",
            "Repeat: all", visible: "playlist.isLoop");
        Btn(new Imgs("m_rep2_u", "m_rep2_o", "m_rep2_d"), x, by, "playlist.setRepeat(false)",
            "Repeat: one", visible: "playlist.isRepeat");

        x = W - Px(8) - bw;
        Btn(IconBtn("p_save", '', bw, bw, Bg), x, by, "playlist.save()", "Save playlist", RT, RT);
        x -= bw + gap;
        Btn(IconBtn("p_load", '', bw, bw, Bg), x, by, "playlist.load()", "Load playlist", RT, RT);
        Close("Panel");

        Close("Layout");
        Close("Window");
    }

    // ------------------------------------------------------------------ equalizer

    void BuildEqualizer()
    {
        string[] bands = { "60", "170", "310", "600", "1k", "3k", "6k", "12k", "14k", "16k" };
        int TB = Px(32), capW = Px(46);
        int r = Px(10), bt = 2 * Math.Max(1, Px(2));
        int pre = Px(96), first = Px(162), step = Px(42);
        int W = first + 9 * step + Px(36);
        int rowC = Px(58);
        int railTop = Px(92), L = Px(140), railBot = railTop + L + 2 * r;
        int labelC = railBot + Px(18), H = labelC + Px(26);
        int[] cols = new int[11];
        cols[0] = pre;
        for (int i = 0; i < 10; i++) cols[i + 1] = first + i * step;

        // one baked background: title, rails, scale and labels
        var bg = Frame(W, H, Bg, Border, true, true, true, true, R, true);
        using (var g = G(bg))
        {
            using var fTitle = UiFont(Px(12));
            using var fLab = UiFont(Px(12));
            Text(g, "Equalizer", fTitle, TextPri, new RectangleF(Px(14), 0, W, TB), StringAlignment.Near);
            for (int i = 0; i < 11; i++)
                FillRect(g, Rail, new RectangleF(cols[i] - bt / 2f, railTop, bt, railBot - railTop));
            // scale
            int lx = Px(14), lw = pre - r - Px(10) - lx;
            Text(g, "+20 dB", fLab, TextTer, new RectangleF(lx, railTop + r - Px(10), lw, Px(20)), StringAlignment.Far);
            Text(g, "0 dB", fLab, TextTer, new RectangleF(lx, railTop + r + L / 2 - Px(10), lw, Px(20)), StringAlignment.Far);
            Text(g, "−20 dB", fLab, TextTer, new RectangleF(lx, railTop + r + L - Px(10), lw, Px(20)), StringAlignment.Far);
            FillRect(g, Divider, new RectangleF(cols[1] - step / 2f - 0.5f, railTop, 1, railBot - railTop));
            // labels of the bands
            Text(g, "Preamp", fLab, TextSec, new RectangleF(cols[0] - Px(30), labelC - Px(10), Px(60), Px(20)), StringAlignment.Center);
            for (int i = 0; i < 10; i++)
                Text(g, bands[i], fLab, TextSec, new RectangleF(cols[i + 1] - Px(21), labelC - Px(10), Px(42), Px(20)), StringAlignment.Center);
            Text(g, "Hz", fLab, TextTer, new RectangleF(cols[10] + Px(14), labelC - Px(10), Px(30), Px(20)), StringAlignment.Near);
        }
        var cover = Crop(bg, new Rectangle(0, railBot, W, H - railBot));
        string bgId = Bmp("eq_bg", bg);
        string coverId = Bmp("eq_cover", cover);

        Open("Window", ("id", "eqwin"), ("position", "West"), ("xmargin", Px(24)), ("visible", "false"), ("dragdrop", "false"));
        Open("Layout", ("id", "eq_layout"), ("width", W), ("height", H), ("minwidth", W), ("maxwidth", W), ("minheight", H), ("maxheight", H));
        Img(bgId, 0, 0, W, H, action: "move");
        Btn(new Imgs("cap_close_u", "cap_close_o", "cap_close_d"), W - capW, 0, "eqwin.hide()", "Close");

        var (off, on) = Toggle("eq_tg", Bg);
        Chk("equalizer.isEnabled", off, on, "equalizer.enable()", "equalizer.disable()", "Turn the equalizer on", "Turn the equalizer off",
            Px(16), rowC - Px(16));

        var th = Thumb("th_eq", L + r, true);
        string[] tips = { "Preamp", "60 Hz", "170 Hz", "310 Hz", "600 Hz", "1 kHz", "3 kHz", "6 kHz", "12 kHz", "14 kHz", "16 kHz" };
        for (int i = 0; i < 11; i++)
        {
            string val = i == 0 ? "equalizer.preamp" : $"equalizer.band({i - 1})";
            El("Slider", ("x", cols[i]), ("y", railTop + r), ("points", $"(0,{L}),(0,0)"), ("value", val),
                ("up", th.U), ("over", th.O), ("down", th.D), ("thickness", Px(12)), ("tooltiptext", tips[i]));
        }
        Img(coverId, 0, railBot, W, H - railBot, action: "move");
        Close("Layout");
        Close("Window");
    }

    // ------------------------------------------------------------------ full-screen controller

    void BuildFsc()
    {
        int W = Px(780), H = Px(104);
        int bw = Px(40), gap = Px(4);
        int r = Px(10), bt = 2 * Math.Max(1, Px(2));

        var bg = Frame(W, H, FscBg, FscBorder, true, true, true, true, R, true);
        int pad = Px(18), timeW = Px(56), c1 = Px(22), rs = pad + timeW + Px(6);
        int c2 = Px(68), by = c2 - bw / 2, mx = Px(10);
        int vrs = mx + bw + Px(4), vL = Px(88);
        using (var g = G(bg))
        {
            FillRect(g, Rail, new RectangleF(rs, c1 - bt / 2f, W - 2 * rs, bt));
            FillRect(g, Rail, new RectangleF(vrs, c2 - bt / 2f, vL + 2 * r, bt));
        }
        string seekCover = Bmp("f_cover_seek", Crop(bg, new Rectangle(0, c1 - r, rs, 2 * r)));
        string volCover = Bmp("f_cover_vol", Crop(bg, new Rectangle(0, c2 - r, vrs, 2 * r)));
        string bgId = Bmp("f_bg", bg);

        Open("Window", ("id", "fullscreenController"), ("position", "South"), ("ymargin", Px(36)), ("dragdrop", "false"));
        Open("Layout", ("id", "fsc_layout"), ("width", W), ("height", H), ("minwidth", W), ("maxwidth", W), ("minheight", H), ("maxheight", H));
        Img(bgId, 0, 0, W, H);

        SliderH("time", new Imgs("th_seek_u", "th_seek_o", "th_seek_d"), rs + r, c1, W - 2 * rs - 2 * r, "$T / $D",
            visible: "not vlc.isStopped");
        Img(seekCover, 0, c1 - r, rs, 2 * r);
        Txt("$T", "f_time", TextSec, pad, TextY("f_time", c1), timeW, "left");
        Txt("$D", "f_time", TextSec, W - pad - timeW, TextY("f_time", c1), timeW, "right");

        SliderH("volume", new Imgs("th_vol_u", "th_vol_o", "th_vol_d"), vrs + r, c2, vL, "Volume: $V%");
        Img(volCover, 0, c2 - r, vrs, 2 * r);
        Chk("vlc.isMute",
            IconBtn("f_vol", '', bw, bw, FscBg),
            IconBtn("f_mute", '', bw, bw, FscBg, refGlyph: ''),
            "vlc.mute()", "vlc.mute()", "Mute (M)", "Unmute (M)", mx, by);

        TransportGroup("f", FscBg, W, c2, kx: false);

        int x = W - mx - bw;
        Btn(IconBtn("f_exitfs", '', bw, bw, FscBg), x, by, "vlc.fullscreen()", "Exit full screen (Esc)");
        x -= bw + gap;
        Btn(IconBtn("f_video", '', bw, bw, FscBg), x, by, "dialogs.videoPopup()", "Video: aspect ratio, crop, deinterlace…");
        x -= bw + gap + Px(8);
        Btn(IconBtn("f_sub", '', bw, bw, FscBg), x, by, "dialogs.popup()", SubTip);
        x -= bw + gap;
        Btn(IconBtn("f_audio", '', bw, bw, FscBg), x, by, "dialogs.audioPopup()", "Audio track");

        Close("Layout");
        Close("Window");
    }
}
