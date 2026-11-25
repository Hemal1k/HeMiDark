using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HeMiDark;

static class Program
{
    /// <summary>Colour variants (accents for dark mode – light tones, a black icon on them reads well).</summary>
    static readonly (string key, string name, string hex)[] Palettes =
    {
        ("blue",     "Blue",     "#4CC2FF"),
        ("teal",     "Teal",     "#3DD6C4"),
        ("green",    "Green",    "#6CCB5F"),
        ("yellow",   "Yellow",   "#FFD43B"),
        ("orange",   "Orange",   "#FFA24C"),
        ("red",      "Red",      "#FF7A7A"),
        ("pink",     "Pink",     "#FF8CC6"),
        ("purple",   "Purple",   "#B9A2FF"),
        ("graphite", "Graphite", "#D6D6D6"),
    };

    static readonly int[] AllScales = { 100, 125, 140, 150, 175, 200 };
    const int MinScale = 100, MaxScale = 400;

    /// <summary>
    /// Builds the skins into build\skins\&lt;colour&gt;\HeMi-Dark-&lt;colour&gt;-&lt;scale&gt;.vlt – every colour and scaling, or
    /// fewer with --colors blue,green / --scales 100,150, or one custom color with --accent #RRGGBB (or "system").
    /// release.ps1 packs them into the zip.
    /// </summary>
    static int Main(string[] args)
    {
        string root = FindRoot();
        var scales = AllScales.ToList();
        var variants = Palettes.Select(p => (p.key, p.name, color: ColorTranslator.FromHtml(p.hex))).ToList();
        for (int i = 0; i < args.Length; i++)
        {
            string sw = args[i];
            if ((sw is "--scales" or "--colors" or "--accent") && i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"{sw} needs a value");
                return 2;
            }
            switch (sw)
            {
                case "--scales":
                    scales = new();
                    foreach (string s in args[++i].Split(','))
                    {
                        if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int pct) || pct < MinScale || pct > MaxScale)
                        {
                            Console.Error.WriteLine($"--scales: \"{s}\" is not a scale from {MinScale} to {MaxScale}");
                            return 2;
                        }
                        scales.Add(pct);
                    }
                    break;
                case "--colors":
                    var keys = args[++i].Split(',');
                    string? unknown = keys.FirstOrDefault(k => Palettes.All(p => p.key != k));
                    if (unknown != null)
                    {
                        Console.Error.WriteLine($"--colors: unknown color \"{unknown}\" (known: {string.Join(", ", Palettes.Select(p => p.key))})");
                        return 2;
                    }
                    variants = variants.Where(v => keys.Contains(v.key)).ToList();
                    break;
                case "--accent": // an own colour, or "system" = the accent of Windows
                    string a = args[++i];
                    if (a != "system" && !Regex.IsMatch(a, "^#[0-9A-Fa-f]{6}$"))
                    {
                        Console.Error.WriteLine($"--accent: \"{a}\" is not #RRGGBB or system");
                        return 2;
                    }
                    variants = new() { ("custom", "Custom", a == "system" ? ReadAccent() : ColorTranslator.FromHtml(a)) };
                    break;
                default: Console.Error.WriteLine($"Unknown switch {sw}"); return 2;
            }
        }
        // checked before build\skins is deleted, so a wrong call leaves the skins built before
        if (variants.Count == 0 || scales.Count == 0)
        {
            Console.Error.WriteLine("Nothing to build (check --colors / --scales).");
            return 2;
        }
        if (!Gfx.HasFont(Gfx.IconFontName))
        {
            Console.Error.WriteLine($"The font {Gfx.IconFontName} is missing (part of Windows 11, or https://aka.ms/SegoeFluentIcons).");
            return 1;
        }

        string version = Version();
        string fontDir = Path.Combine(root, "src", "Assets", "Fonts");
        string skins = Path.Combine(root, "build", "skins");
        if (Directory.Exists(skins)) Directory.Delete(skins, true);
        Console.WriteLine($"HeMi Dark {version} (icons: {Gfx.IconFamily.Name})");
        foreach (var (key, name, color) in variants)
        {
            Console.WriteLine($"{name} #{color.R:X2}{color.G:X2}{color.B:X2}");
            foreach (int pct in scales)
            {
                string work = Path.Combine(root, "build", "work", $"{key}-{pct}");
                string vlt = Path.Combine(skins, key, $"HeMi-Dark-{key}-{pct}.vlt");
                using var sb = new SkinBuild(fontDir, work, pct, color, name);
                sb.Build(vlt);
                Console.WriteLine($"  {vlt}");
            }
        }
        string workRoot = Path.Combine(root, "build", "work");
        if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true);
        return 0;
    }

    static string Version()
    {
        string v = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        int plus = v.IndexOf('+');
        return plus < 0 ? v : v[..plus];
    }

    /// <summary>The accent of Windows for dark mode (AccentPalette[1] = SystemAccentColorLight2).</summary>
    static Color ReadAccent()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (k?.GetValue("AccentPalette") is byte[] p && p.Length >= 8)
                return Color.FromArgb(p[4], p[5], p[6]);
        }
        catch { /* the default below */ }
        Console.Error.WriteLine("Windows accent not found, using #4CC2FF");
        return ColorTranslator.FromHtml("#4CC2FF");
    }

    /// <summary>The solution root (the folder with HeMiDark.slnx).</summary>
    static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "HeMiDark.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("HeMiDark.slnx not found.");
    }
}
