using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using FreeTrainSimulator.Graphics.Xna;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Converts an optional <see cref="System.Drawing.Color"/> from the game-side snapshot into a WPF
    /// <see cref="SolidColorBrush"/> for tool-window row rendering.
    /// </summary>
    public sealed class DrawingColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Drawing.Color color)
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));

            // Theme-aware fallback so rows without explicit formatting follow the active WPF theme.
            return SystemColors.ControlTextBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts a boolean bold flag into a WPF <see cref="FontWeight"/>.
    /// </summary>
    public sealed class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? FontWeights.Bold : FontWeights.Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts an XNA color name (e.g. "Blue") into a WPF <see cref="SolidColorBrush"/> for rendering color
    /// swatches in the settings tool window. Unknown names resolve to transparent.
    /// </summary>
    public sealed class ColorNameToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorName)
            {
                Microsoft.Xna.Framework.Color color = ColorExtension.FromName(colorName);
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
