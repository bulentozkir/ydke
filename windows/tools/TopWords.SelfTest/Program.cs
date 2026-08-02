using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using TopWords.Windows;

namespace TopWords.SelfTest;

/// <summary>
/// Measures how far each page overflows the app window, using the real host
/// configuration (same injected bootstrap, same blocked ad requests).
///
/// Run with <c>--no-bootstrap</c> to get the unmodified baseline, so a layout
/// change can be judged against real before/after numbers rather than opinion.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var width = GetInt(args, "--width", 1000);
        var height = GetInt(args, "--height", 800);
        var withBootstrap = !args.Contains("--no-bootstrap");
        var exitCode = 0;

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
                await RunAsync(web, width, height, withBootstrap);
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

    private static async Task RunAsync(WebView2 web, int width, int height, bool withBootstrap)
    {
        // A throwaway profile keeps first-run behaviour reproducible.
        var profile = Path.Combine(Path.GetTempPath(), "TopWords.SelfTest", Guid.NewGuid().ToString("N"));
        var environment = await CoreWebView2Environment.CreateAsync(null, profile);
        await web.EnsureCoreWebView2Async(environment);

        var core = web.CoreWebView2;

        foreach (var pattern in AppConfig.BlockedResourcePatterns)
        {
            core.AddWebResourceRequestedFilter(pattern, CoreWebView2WebResourceContext.All);
        }

        var blocked = 0;
        core.WebResourceRequested += (_, e) =>
        {
            Interlocked.Increment(ref blocked);
            e.Response = environment.CreateWebResourceResponse(null, 204, "No Content", string.Empty);
        };

        if (withBootstrap)
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync(EmbeddedResources.Bootstrap.Value);
        }

        await core.CallDevToolsProtocolMethodAsync("Page.enable", "{}");
        await core.CallDevToolsProtocolMethodAsync(
            "Emulation.setDeviceMetricsOverride",
            JsonSerializer.Serialize(new { width, height, deviceScaleFactor = 1, mobile = false }));

        Console.WriteLine($"viewport {width}x{height}  bootstrap={(withBootstrap ? "on" : "off")}");
        Console.WriteLine(new string('-', 78));

        var pages = await DiscoverPagesAsync(core);
        var worst = 0.0;

        foreach (var path in pages)
        {
            var report = await MeasureAsync(core, AppConfig.AppOrigin + path);
            if (report is null)
            {
                continue;
            }

            var scrollH = report.Value.GetProperty("scrollH").GetInt32();
            var vh = report.Value.GetProperty("vh").GetInt32();
            var ratio = vh == 0 ? 0 : (double)scrollH / vh;
            worst = Math.Max(worst, ratio);

            var title = report.Value.GetProperty("title").GetString() ?? "";
            if (title.Length > 34) { title = title[..34]; }

            Console.WriteLine($"{path,-22} {title,-36} {scrollH,6}px  {ratio,5:0.00}x screens");

            foreach (var element in report.Value.GetProperty("tall").EnumerateArray().Take(4))
            {
                Console.WriteLine(
                    $"      {element.GetProperty("h").GetInt32(),5}px  " +
                    $"{element.GetProperty("w").GetInt32(),5}w  " +
                    $"{element.GetProperty("t").GetString()}");
            }
        }

        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"worst page = {worst:0.00} screens tall   ad requests blocked = {blocked}");
    }

    private static async Task<IReadOnlyList<string>> DiscoverPagesAsync(CoreWebView2 core)
    {
        var report = await MeasureAsync(core, AppConfig.StartUrl);

        var pages = new List<string> { "/" };
        if (report is null)
        {
            return pages;
        }

        foreach (var href in report.Value.GetProperty("links").EnumerateArray())
        {
            var value = href.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var path = value.StartsWith('/') ? value : "/" + value;
            if (!pages.Contains(path))
            {
                pages.Add(path);
            }
        }

        return pages.Take(10).ToList();
    }

    private static async Task<JsonElement?> MeasureAsync(CoreWebView2 core, string url)
    {
        var done = new TaskCompletionSource();
        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => done.TrySetResult();

        core.NavigationCompleted += OnCompleted;
        core.Navigate(url);

        var finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        core.NavigationCompleted -= OnCompleted;

        if (finished != done.Task)
        {
            Console.Error.WriteLine($"timeout: {url}");
            return null;
        }

        // The app renders its content from JS, so wait for it to settle.
        await Task.Delay(2200);

        var raw = await core.ExecuteScriptAsync(ProbeScript);

        // ExecuteScriptAsync returns the result JSON-encoded, so unwrap the string first.
        var inner = JsonSerializer.Deserialize<string>(raw);
        if (string.IsNullOrEmpty(inner))
        {
            return null;
        }

        return JsonDocument.Parse(inner).RootElement.Clone();
    }

    private static int GetInt(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value)
            ? value
            : fallback;
    }

    private const string ProbeScript = """
        (function () {
          var se = document.scrollingElement || document.documentElement;
          function box(el) {
            var r = el.getBoundingClientRect();
            var cls = (el.getAttribute('class') || '').split(/\s+/).filter(Boolean).slice(0, 2).join('.');
            return {
              t: el.tagName.toLowerCase() + (cls ? '.' + cls : ''),
              h: Math.round(r.height),
              w: Math.round(r.width)
            };
          }
          var tall = Array.prototype.slice.call(document.querySelectorAll('body *'))
            .filter(function (el) {
              var r = el.getBoundingClientRect();
              return r.height > 60 && el.children.length <= 14;
            })
            .map(box)
            .sort(function (a, b) { return b.h - a.h; })
            .slice(0, 8);
          var links = Array.prototype.slice.call(document.querySelectorAll('a[href]'))
            .map(function (a) { return a.getAttribute('href'); })
            .filter(function (h) {
              return h && h.indexOf('http') !== 0 && h.charAt(0) !== '#' && h.indexOf('mailto') !== 0;
            })
            .filter(function (v, i, a) { return a.indexOf(v) === i; });
          return JSON.stringify({
            title: document.title,
            vw: window.innerWidth,
            vh: window.innerHeight,
            scrollH: se.scrollHeight,
            tall: tall,
            links: links
          });
        })()
        """;
}
