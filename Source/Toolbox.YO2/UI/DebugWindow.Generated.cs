/* Generated by MyraPad at 9/9/2022 10:32:55 AM */
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI.Properties;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using System.Numerics;
#endif

namespace Toolbox.YO2
{
	partial class DebugWindow: Window
	{
		private void BuildUI()
		{
			var label1 = new Label();
			label1.Text = "Window size x";

			var label2 = new Label();
			label2.Text = "Window size y";
			label2.GridRow = 1;

			_WindowSize_X = new TextBox();
			_WindowSize_X.GridColumn = 1;
			_WindowSize_X.Id = "_WindowSize_X";

			_WindowSize_Y = new TextBox();
			_WindowSize_Y.GridColumn = 1;
			_WindowSize_Y.GridRow = 1;
			_WindowSize_Y.Id = "_WindowSize_Y";

			var grid1 = new Grid();
			grid1.DefaultColumnProportion = new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			};
			grid1.Widgets.Add(label1);
			grid1.Widgets.Add(label2);
			grid1.Widgets.Add(_WindowSize_X);
			grid1.Widgets.Add(_WindowSize_Y);

			var verticalStackPanel1 = new VerticalStackPanel();
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Widgets.Add(grid1);

			
			Title = "Debug Window";
			Left = 601;
			Top = 209;
			Id = "M_Debug_window";
			Content = verticalStackPanel1;
		}

		
		public TextBox _WindowSize_X;
		public TextBox _WindowSize_Y;
	}
}
