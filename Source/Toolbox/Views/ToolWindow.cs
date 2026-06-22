using System;
using System.Windows;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Attached properties for the dockable tool-window views. Lets each view declare its own default floating
    /// (undocked) size in its XAML header, next to the <c>d:DesignWidth</c>/<c>d:DesignHeight</c> design hints,
    /// so the size lives with the view rather than being hard-coded on the shell's anchorables.
    /// </summary>
    internal static class ToolWindow
    {
        /// <summary>
        /// Default size of the hosting <c>LayoutAnchorable</c> when it floats. The shell reads this off each
        /// view and applies it to the anchorable's <c>FloatingWidth</c>/<c>FloatingHeight</c> before capturing
        /// the default dock layout, so the value flows into both the initial layout and the reset baseline.
        /// Left unset (<see cref="Size.Empty"/>), AvalonDock's own default is kept.
        /// </summary>
        public static readonly DependencyProperty DefaultFloatingSizeProperty = DependencyProperty.RegisterAttached(
            "DefaultFloatingSize",
            typeof(Size),
            typeof(ToolWindow),
            new PropertyMetadata(Size.Empty));

        public static Size GetDefaultFloatingSize(DependencyObject element)
        {
            ArgumentNullException.ThrowIfNull(element);
            return (Size)element.GetValue(DefaultFloatingSizeProperty);
        }

        public static void SetDefaultFloatingSize(DependencyObject element, Size value)
        {
            ArgumentNullException.ThrowIfNull(element);
            element.SetValue(DefaultFloatingSizeProperty, value);
        }
    }
}
