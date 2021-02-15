using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Microsoft.Xna.Framework;

using Orts.View.Xna;

namespace Orts.TrackViewer.WinForms.Controls
{
    public static class ComboBoxExtension
    {
        private static readonly System.Drawing.SolidBrush backgroundBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Transparent);
        private static readonly System.Drawing.SolidBrush fontBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Transparent);

        private static readonly object[] sortedColors = ColorExtension.ColorCodes.OrderByDescending(kvp => (kvp.Value.R << 8) + (kvp.Value.G << 8) + kvp.Value.B).Select(c => c.Key).Cast<object>().ToArray();

        internal static void DisplayXnaColors(this ToolStripComboBox comboBox, string defaultColor, ColorPreference preference)
        {
            // Make the ComboBox owner-drawn.
            comboBox.ComboBox.DrawMode = DrawMode.OwnerDrawVariable;

            comboBox.Items.Clear();
            comboBox.Items.AddRange(sortedColors);
            // Subscribe to the DrawItem event.
            //cbo.MeasureItem += cboDrawImageAndText_MeasureItem;
            comboBox.ComboBox.DrawItem += ComboBox_DrawItem;
            comboBox.SelectedItem = defaultColor;
            comboBox.Tag = preference;
        }

        private static void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index > -1)
            {
                string colorName = (sender as ComboBox).Items[e.Index].ToString();
                Color color = ColorExtension.FromName(colorName);
                backgroundBrush.Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                fontBrush.Color = color.ToComplementSystemDrawingColor();
                e.Graphics.DrawString(colorName, e.Font, fontBrush, e.Bounds, System.Drawing.StringFormat.GenericDefault);
                // If the ListBox has focus, draw a focus rectangle around the selected item.
                e.DrawFocusRectangle();
            }
        }
    }

}
