using System;
using System.Windows;

using AvalonDock.Layout;

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

        /// <summary>
        /// Initializes missing auto-hide dimensions from the view's default size without replacing user sizes.
        /// </summary>
        public static void ApplyDefaultAutoHideSize(LayoutAnchorable anchorable)
        {
            ArgumentNullException.ThrowIfNull(anchorable);
            if (anchorable.Content is not FrameworkElement view)
                return;

            Size size = GetDefaultFloatingSize(view);
            if (size.IsEmpty)
                return;

            if ((!double.IsFinite(anchorable.AutoHideWidth) || anchorable.AutoHideWidth <= 0)
                && double.IsFinite(size.Width) && size.Width > 0)
                anchorable.AutoHideWidth = size.Width;
            if ((!double.IsFinite(anchorable.AutoHideHeight) || anchorable.AutoHideHeight <= 0)
                && double.IsFinite(size.Height) && size.Height > 0)
                anchorable.AutoHideHeight = size.Height;
        }
    }
}
