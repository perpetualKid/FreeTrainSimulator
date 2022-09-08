/* Generated by MyraPad at 9/8/2022 5:46:05 PM */
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

using Color = Microsoft.Xna.Framework.Color;
using SolidBrush = Myra.Graphics2D.Brushes.SolidBrush;
using Image = Myra.Graphics2D.UI.Image;

namespace Toolbox.YO2
{
	partial class M_OpenWindow: Window
	{
		private void BuildUI()
		{
			var image1 = new Image();
			image1.Renderable = MyraEnvironment.DefaultAssetManager.Load<TextureRegion>("../Content/TitleBarWindow.png");

			var textButton1 = new TextButton();
			textButton1.Text = "                          Search For Folder Containing \'Trains\'                  " +
    "     ";
			textButton1.TextColor = Color.Black;
			textButton1.BorderThickness = new Thickness(2);
			textButton1.Background = new SolidBrush("#D9D9D9FF");
			textButton1.Border = new SolidBrush("#FFFFFFFF");

			var textBox1 = new TextBox();
			textBox1.Text = "                                      Or Select from Below                     ";
			textBox1.Background = new SolidBrush("#404040FF");

			_OpenWin_List = new ListBox();
			_OpenWin_List.Width = 540;
			_OpenWin_List.Height = 180;
			_OpenWin_List.Id = "_OpenWin_List";

			var textButton2 = new TextButton();
			textButton2.Text = "                                        Load                                     " +
    "    ";
			textButton2.BorderThickness = new Thickness(2);
			textButton2.Background = new SolidBrush("#4BD961FF");
			textButton2.Border = new SolidBrush("#FFFFFFFF");

			_OpenWin_Cancel_button = new TextButton();
			_OpenWin_Cancel_button.Text = "    Cancel    ";
			_OpenWin_Cancel_button.BorderThickness = new Thickness(2);
			_OpenWin_Cancel_button.Background = new SolidBrush("#FE3930FF");
			_OpenWin_Cancel_button.Border = new SolidBrush("#FFFFFFFF");
			_OpenWin_Cancel_button.Id = "_OpenWin_Cancel_button";

			var horizontalStackPanel1 = new HorizontalStackPanel();
			horizontalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel1.Widgets.Add(textButton2);
			horizontalStackPanel1.Widgets.Add(_OpenWin_Cancel_button);

			var verticalStackPanel1 = new VerticalStackPanel();
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Widgets.Add(image1);
			verticalStackPanel1.Widgets.Add(textButton1);
			verticalStackPanel1.Widgets.Add(textBox1);
			verticalStackPanel1.Widgets.Add(_OpenWin_List);
			verticalStackPanel1.Widgets.Add(horizontalStackPanel1);

			
			Title = "";
			Left = 408;
			Top = 5;
			Id = "M_OpenWindow";
			Content = verticalStackPanel1;
		}

		
		public ListBox _OpenWin_List;
		public TextButton _OpenWin_Cancel_button;
	}
}
