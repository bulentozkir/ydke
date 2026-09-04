using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace YDKE_Windows;

internal sealed class AppearancePalette
{
    public const string DefaultBackground = "#202124";
    public const string DefaultButton = "#0B5CAD";
    public const string DefaultBox = "#2B2D31";

    private AppearancePalette(Color background, Color button, Color box, double fontScale)
    {
        Background = background;
        BackgroundForeground = BestForeground(background);
        Button = button;
        ButtonForeground = BestForeground(button);
        ButtonBorder = ContrastingBorder(button, ButtonForeground);
        Box = box;
        BoxForeground = BestForeground(box);
        Border = ContrastingBorder(box, BoxForeground);
        FontScale = Math.Clamp(fontScale, 0.85, 1.40);
        Theme = RelativeLuminance(background) > 0.45 ? ElementTheme.Light : ElementTheme.Dark;
    }

    public static AppearancePalette Current { get; private set; } = From(new UserSettings());

    public Color Background { get; }

    public Color BackgroundForeground { get; }

    public Color Button { get; }

    public Color ButtonForeground { get; }

    public Color ButtonBorder { get; }

    public Color Box { get; }

    public Color BoxForeground { get; }

    public Color Border { get; }

    public double FontScale { get; }

    public ElementTheme Theme { get; }

    public SolidColorBrush BackgroundBrush => new(Background);

    public SolidColorBrush BackgroundForegroundBrush => new(BackgroundForeground);

    public SolidColorBrush ButtonBrush => new(Button);

    public SolidColorBrush ButtonForegroundBrush => new(ButtonForeground);

    public SolidColorBrush ButtonBorderBrush => new(ButtonBorder);

    public SolidColorBrush BoxBrush => new(Box);

    public SolidColorBrush BoxForegroundBrush => new(BoxForeground);

    public SolidColorBrush BorderBrush => new(Border);

    public static void SetCurrent(UserSettings settings) => Current = From(settings);

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static Color Parse(string? value, string fallback)
    {
        var source = value?.Trim();
        if (source is null || source.Length != 7 || source[0] != '#') source = fallback;
        try
        {
            return Color.FromArgb(
                255,
                Convert.ToByte(source[1..3], 16),
                Convert.ToByte(source[3..5], 16),
                Convert.ToByte(source[5..7], 16));
        }
        catch (FormatException)
        {
            return Parse(fallback, DefaultBackground);
        }
    }

    public static double ContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static AppearancePalette From(UserSettings settings) => new(
        Parse(settings.AppBackgroundColor, DefaultBackground),
        Parse(settings.ButtonColor, DefaultButton),
        Parse(settings.BoxColor, DefaultBox),
        settings.FontScale);

    private static Color BestForeground(Color background)
    {
        var light = Color.FromArgb(255, 248, 250, 252);
        var dark = Color.FromArgb(255, 15, 23, 42);
        var lightRatio = ContrastRatio(background, light);
        var darkRatio = ContrastRatio(background, dark);
        var preferred = lightRatio >= darkRatio ? light : dark;
        if (Math.Max(lightRatio, darkRatio) >= 4.5) return preferred;
        return ContrastRatio(background, Microsoft.UI.Colors.White) >=
            ContrastRatio(background, Microsoft.UI.Colors.Black)
            ? Microsoft.UI.Colors.White
            : Microsoft.UI.Colors.Black;
    }

    private static Color ContrastingBorder(Color background, Color foreground)
    {
        for (var amount = 0.22; amount <= 0.82; amount += 0.10)
        {
            var candidate = Mix(background, foreground, amount);
            if (ContrastRatio(background, candidate) >= 3) return candidate;
        }
        return foreground;
    }

    private static Color Mix(Color first, Color second, double amount) => Color.FromArgb(
        255,
        (byte)Math.Round(first.R + ((second.R - first.R) * amount)),
        (byte)Math.Round(first.G + ((second.G - first.G) * amount)),
        (byte)Math.Round(first.B + ((second.B - first.B) * amount)));

    private static double RelativeLuminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte component)
    {
        var value = component / 255.0;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}