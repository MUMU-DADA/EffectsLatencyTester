using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EffectsLatencyTester;

public enum ThemeMode
{
    Dark,
    Light,
}

public sealed record ThemePalette(
    Color WindowBackground,
    Color SurfaceBackground,
    Color InputBackground,
    Color Border,
    Color Foreground,
    Color MutedForeground,
    Color Accent,
    Color AccentForeground,
    Color StatusBackground,
    Color ButtonBackground,
    Color ButtonForeground,
    Color ButtonBorder,
    Color PlotBackground,
    Color PlotGrid,
    Color PlotAxis,
    Color PlotBorder,
    Color PlotCursor,
    Color PlotTime1,
    Color PlotTime2,
    Color PlotOutput,
    Color PlotInput,
    Color PlotLabel,
    Color PlotMutedLabel)
{
    public static ThemePalette Dark { get; } = new(
        Color.FromRgb(17, 21, 26),
        Color.FromRgb(24, 30, 36),
        Color.FromRgb(13, 17, 21),
        Color.FromRgb(76, 88, 100),
        Color.FromRgb(232, 237, 242),
        Color.FromRgb(158, 174, 188),
        Color.FromRgb(0, 120, 215),
        Colors.White,
        Color.FromRgb(24, 30, 36),
        Color.FromRgb(0, 120, 215),
        Colors.White,
        Color.FromRgb(0, 120, 215),
        Color.FromRgb(5, 8, 12),
        Color.FromRgb(42, 50, 58),
        Color.FromRgb(112, 124, 136),
        Color.FromRgb(76, 88, 100),
        Colors.White,
        Color.FromRgb(255, 209, 102),
        Color.FromRgb(124, 255, 176),
        Color.FromRgb(255, 66, 110),
        Color.FromRgb(0, 229, 255),
        Color.FromRgb(224, 232, 240),
        Color.FromRgb(158, 174, 188));

    public static ThemePalette Light { get; } = new(
        Color.FromRgb(245, 247, 250),
        Colors.White,
        Colors.White,
        Color.FromRgb(185, 195, 205),
        Color.FromRgb(27, 36, 45),
        Color.FromRgb(91, 103, 115),
        Color.FromRgb(0, 102, 204),
        Colors.White,
        Color.FromRgb(232, 237, 242),
        Colors.White,
        Color.FromRgb(27, 36, 45),
        Color.FromRgb(185, 195, 205),
        Color.FromRgb(250, 252, 254),
        Color.FromRgb(213, 221, 229),
        Color.FromRgb(113, 126, 140),
        Color.FromRgb(156, 168, 180),
        Color.FromRgb(27, 36, 45),
        Color.FromRgb(178, 103, 0),
        Color.FromRgb(10, 118, 64),
        Color.FromRgb(196, 26, 82),
        Color.FromRgb(0, 126, 145),
        Color.FromRgb(38, 50, 61),
        Color.FromRgb(98, 112, 128));
}

public static class ThemeManager
{
    public const string WindowBackgroundBrush = nameof(WindowBackgroundBrush);
    public const string SurfaceBackgroundBrush = nameof(SurfaceBackgroundBrush);
    public const string InputBackgroundBrush = nameof(InputBackgroundBrush);
    public const string BorderBrush = nameof(BorderBrush);
    public const string ForegroundBrush = nameof(ForegroundBrush);
    public const string MutedForegroundBrush = nameof(MutedForegroundBrush);
    public const string AccentBrush = nameof(AccentBrush);
    public const string AccentForegroundBrush = nameof(AccentForegroundBrush);
    public const string StatusBackgroundBrush = nameof(StatusBackgroundBrush);
    public const string ButtonBackgroundBrush = nameof(ButtonBackgroundBrush);
    public const string ButtonForegroundBrush = nameof(ButtonForegroundBrush);
    public const string ButtonBorderBrush = nameof(ButtonBorderBrush);
    private const string DarkComboBoxStyleKey = "DarkComboBoxStyle";
    private const string DarkComboBoxItemStyleKey = "DarkComboBoxItemStyle";
    private const string DarkButtonStyleKey = "DarkButtonStyle";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EffectsLatencyTester",
        "theme.txt");

    public static ThemeMode CurrentMode { get; private set; } = ThemeMode.Dark;

    public static ThemePalette CurrentPalette { get; private set; } = ThemePalette.Dark;

    public static event EventHandler<ThemePalette>? ThemeChanged;

    public static void Initialize()
    {
        Apply(LoadTheme(), save: false);
    }

    public static void SetTheme(ThemeMode mode)
    {
        Apply(mode, save: true);
    }

    private static void Apply(ThemeMode mode, bool save)
    {
        CurrentMode = mode;
        CurrentPalette = mode == ThemeMode.Light ? ThemePalette.Light : ThemePalette.Dark;

        if (Application.Current is not null)
        {
            var resources = Application.Current.Resources;
            SetBrush(resources, WindowBackgroundBrush, CurrentPalette.WindowBackground);
            SetBrush(resources, SurfaceBackgroundBrush, CurrentPalette.SurfaceBackground);
            SetBrush(resources, InputBackgroundBrush, CurrentPalette.InputBackground);
            SetBrush(resources, BorderBrush, CurrentPalette.Border);
            SetBrush(resources, ForegroundBrush, CurrentPalette.Foreground);
            SetBrush(resources, MutedForegroundBrush, CurrentPalette.MutedForeground);
            SetBrush(resources, AccentBrush, CurrentPalette.Accent);
            SetBrush(resources, AccentForegroundBrush, CurrentPalette.AccentForeground);
            SetBrush(resources, StatusBackgroundBrush, CurrentPalette.StatusBackground);
            SetBrush(resources, ButtonBackgroundBrush, CurrentPalette.ButtonBackground);
            SetBrush(resources, ButtonForegroundBrush, CurrentPalette.ButtonForeground);
            SetBrush(resources, ButtonBorderBrush, CurrentPalette.ButtonBorder);
            ApplyControlStyles(resources, mode);
        }

        if (save)
        {
            SaveTheme(mode);
        }

        ThemeChanged?.Invoke(null, CurrentPalette);
    }

    private static void ApplyControlStyles(ResourceDictionary resources, ThemeMode mode)
    {
        if (mode == ThemeMode.Dark)
        {
            resources[typeof(ComboBox)] = resources[DarkComboBoxStyleKey];
            resources[typeof(ComboBoxItem)] = resources[DarkComboBoxItemStyleKey];
            resources[typeof(Button)] = resources[DarkButtonStyleKey];
        }
        else
        {
            // Removing the implicit styles restores the native WPF light controls.
            resources.Remove(typeof(ComboBox));
            resources.Remove(typeof(ComboBoxItem));
            resources.Remove(typeof(Button));
        }
    }
    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static ThemeMode LoadTheme()
    {
        try
        {
            var value = File.ReadAllText(SettingsPath).Trim();
            return Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode)
                ? mode
                : ThemeMode.Dark;
        }
        catch (IOException)
        {
            return ThemeMode.Dark;
        }
        catch (UnauthorizedAccessException)
        {
            return ThemeMode.Dark;
        }
    }

    private static void SaveTheme(ThemeMode mode)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsPath, mode.ToString());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}