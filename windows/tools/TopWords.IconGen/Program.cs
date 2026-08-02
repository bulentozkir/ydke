using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace TopWords.IconGen;

/// <summary>
/// Rasterises <c>icon.svg</c> into the full Windows MSIX tile matrix (blocker W1),
/// the PWA manifest PNGs shared with Android blocker B1, and an <c>app.ico</c>.
///
/// Rendering goes through WebView2's DevTools protocol rather than the control's
/// own capture API, because <c>Emulation.setDeviceMetricsOverride</c> pins
/// <c>deviceScaleFactor</c> to 1. Output is therefore pixel-exact regardless of
/// the display scaling on the machine that runs the build.
/// </summary>
internal static class Program
{
    /// <summary>Logo occupies ~66% of a Start tile, per Microsoft's tile guidance.</summary>
    private const double TilePad = 0.17;

    private const double SplashPad = 0.28;

    private static readonly int[] IcoSizes = { 16, 24, 32, 48, 64, 128, 256 };

    [STAThread]
    private static int Main(string[] args)
    {
        var repoRoot = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var svgPath = Path.Combine(repoRoot, "assets", "source", "icon.svg");

        if (!File.Exists(svgPath))
        {
            Console.Error.WriteLine($"Source SVG not found: {svgPath}");
            return 1;
        }

        var exitCode = 0;

        // Off-screen host window: WebView2 needs a real HWND to render into.
        using var form = new Form
        {
            Width = 900,
            Height = 700,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-4000, -4000),
        };

        using var web = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(web);

        form.Shown += async (_, _) =>
        {
            try
            {
                await GenerateAsync(web, svgPath, repoRoot);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                exitCode = 1;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return exitCode;
    }

    private static async Task GenerateAsync(WebView2 web, string svgPath, string repoRoot)
    {
        var userData = Path.Combine(Path.GetTempPath(), "TopWords.IconGen");
        var environment = await CoreWebView2Environment.CreateAsync(null, userData);
        await web.EnsureCoreWebView2Async(environment);

        var core = web.CoreWebView2;
        var svg = await File.ReadAllTextAsync(svgPath);

        var ready = new TaskCompletionSource();
        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => ready.TrySetResult();

        core.NavigationCompleted += OnCompleted;
        core.NavigateToString(BuildHtml(svg));
        await ready.Task;
        core.NavigationCompleted -= OnCompleted;

        await core.CallDevToolsProtocolMethodAsync("Page.enable", "{}");
        await core.CallDevToolsProtocolMethodAsync(
            "Emulation.setDefaultBackgroundColorOverride",
            """{"color":{"r":0,"g":0,"b":0,"a":0}}""");

        var msixDir = Path.Combine(repoRoot, "assets", "msix");
        var webDir = Path.Combine(repoRoot, "assets", "web");
        Directory.CreateDirectory(msixDir);
        Directory.CreateDirectory(webDir);

        var written = 0;

        foreach (var spec in Specs())
        {
            var png = await CaptureAsync(core, spec);
            var target = Path.Combine(repoRoot, spec.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, png);
            written++;
        }

        // app.ico for the executable and window chrome.
        var icoFrames = new List<(int Size, byte[] Png)>();
        foreach (var size in IcoSizes)
        {
            var png = await CaptureAsync(core, new IconSpec(string.Empty, size, size, 0));
            icoFrames.Add((size, png));
        }

        var icoPath = Path.Combine(repoRoot, "src", "TopWords.Windows", "Assets", "app.ico");
        Directory.CreateDirectory(Path.GetDirectoryName(icoPath)!);
        WriteIco(icoPath, icoFrames);

        Console.WriteLine($"Generated {written} PNG assets + app.ico ({icoFrames.Count} frames).");
    }

    private static IEnumerable<IconSpec> Specs()
    {
        // --- Windows MSIX tiles ------------------------------------------------
        var scales = new (string Suffix, double Factor)[]
        {
            ("scale-100", 1.00),
            ("scale-125", 1.25),
            ("scale-150", 1.50),
            ("scale-200", 2.00),
            ("scale-400", 4.00),
        };

        foreach (var (suffix, factor) in scales)
        {
            yield return Tile($"Square44x44Logo.{suffix}", 44, 44, factor, 0);
            yield return Tile($"Square71x71Logo.{suffix}", 71, 71, factor, TilePad);
            yield return Tile($"Square150x150Logo.{suffix}", 150, 150, factor, TilePad);
            yield return Tile($"Square310x310Logo.{suffix}", 310, 310, factor, TilePad);
            yield return Tile($"Wide310x150Logo.{suffix}", 310, 150, factor, TilePad);
            yield return Tile($"StoreLogo.{suffix}", 50, 50, factor, 0);
            yield return Tile($"SplashScreen.{suffix}", 620, 300, factor, SplashPad);
        }

        // Target-size variants drive the taskbar, Start list and Alt+Tab.
        foreach (var size in new[] { 16, 24, 32, 48, 256 })
        {
            yield return Tile($"Square44x44Logo.targetsize-{size}", size, size, 1, 0);
            yield return Tile($"Square44x44Logo.targetsize-{size}_altform-unplated", size, size, 1, 0);
        }

        // --- PWA manifest icons (shared with Android blocker B1) ---------------
        yield return new IconSpec(Path.Combine("assets", "web", "icon-192.png"), 192, 192, 0);
        yield return new IconSpec(Path.Combine("assets", "web", "icon-512.png"), 512, 512, 0);

        // Maskable icons get cropped to a circle, so they need a safe zone and an
        // opaque background rather than the SVG's rounded transparent corners.
        yield return new IconSpec(
            Path.Combine("assets", "web", "icon-maskable-512.png"), 512, 512, 0.10, "#0f172a");
    }

    private static IconSpec Tile(string name, int baseWidth, int baseHeight, double factor, double pad)
    {
        var width = (int)Math.Round(baseWidth * factor);
        var height = (int)Math.Round(baseHeight * factor);
        return new IconSpec(Path.Combine("assets", "msix", name + ".png"), width, height, pad);
    }

    private static async Task<byte[]> CaptureAsync(CoreWebView2 core, IconSpec spec)
    {
        var padPixels = (int)Math.Round(Math.Min(spec.Width, spec.Height) * spec.PadFraction);
        var background = spec.Background ?? "transparent";

        await core.ExecuteScriptAsync(
            $"document.documentElement.style.setProperty('--pad','{padPixels}px');" +
            $"document.body.style.background={JsonSerializer.Serialize(background)};");

        var metrics = JsonSerializer.Serialize(new
        {
            width = spec.Width,
            height = spec.Height,
            deviceScaleFactor = 1,
            mobile = false,
        });

        await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", metrics);

        var response = await core.CallDevToolsProtocolMethodAsync(
            "Page.captureScreenshot",
            """{"format":"png","captureBeyondViewport":true}""");

        using var document = JsonDocument.Parse(response);
        var data = document.RootElement.GetProperty("data").GetString()
            ?? throw new InvalidOperationException("captureScreenshot returned no data.");

        return Convert.FromBase64String(data);
    }

    private static string BuildHtml(string svg) =>
        $$"""
          <!DOCTYPE html>
          <html><head><meta charset="utf-8"><style>
            html,body{margin:0;padding:0;width:100%;height:100%;background:transparent}
            #wrap{box-sizing:border-box;width:100vw;height:100vh;padding:var(--pad,0);
                  display:flex;align-items:center;justify-content:center}
            #wrap>svg{width:100%;height:100%;display:block}
          </style></head>
          <body><div id="wrap">{{svg}}</div></body></html>
          """;

    /// <summary>
    /// Writes a PNG-compressed .ico. Supported on Windows Vista and later, and the
    /// only practical way to embed a 256×256 frame.
    /// </summary>
    private static void WriteIco(string path, IReadOnlyList<(int Size, byte[] Png)> frames)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);              // reserved
        writer.Write((ushort)1);              // type: icon
        writer.Write((ushort)frames.Count);

        var offset = 6 + (16 * frames.Count);

        foreach (var (size, png) in frames)
        {
            // 0 means 256 in the ICO directory.
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);            // palette size
            writer.Write((byte)0);            // reserved
            writer.Write((ushort)1);          // colour planes
            writer.Write((ushort)32);         // bits per pixel
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in frames)
        {
            writer.Write(png);
        }
    }

    private sealed record IconSpec(
        string RelativePath,
        int Width,
        int Height,
        double PadFraction,
        string? Background = null);
}
