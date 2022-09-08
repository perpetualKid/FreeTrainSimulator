/* Generated by MyraPad at 9/7/2022 12:11:41 PM */
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

using SolidBrush = Myra.Graphics2D.Brushes.SolidBrush;

namespace Toolbox.YO2
{
	partial class M_ViewWindow: Window
	{
		private void BuildUI()
		{
			_Content_List = new CheckBox();
			_Content_List.Text = "Consist List";
			_Content_List.GridColumn = 1;
			_Content_List.GridRow = 1;
			_Content_List.Id = "_Content_List";

			_Block_List = new CheckBox();
			_Block_List.Text = "Block List";
			_Block_List.GridColumn = 1;
			_Block_List.GridRow = 2;
			_Block_List.Id = "_Block_List";

			_Set_List = new CheckBox();
			_Set_List.Text = "Set List";
			_Set_List.GridColumn = 1;
			_Set_List.GridRow = 3;
			_Set_List.Id = "_Set_List";

			_Equip_List = new CheckBox();
			_Equip_List.Text = "Equip List";
			_Equip_List.GridColumn = 1;
			_Equip_List.GridRow = 4;
			_Equip_List.Id = "_Equip_List";

			_Consist_Units = new CheckBox();
			_Consist_Units.Text = "Consist Units";
			_Consist_Units.GridColumn = 1;
			_Consist_Units.GridRow = 5;
			_Consist_Units.Id = "_Consist_Units";

			_Equip_3D_View = new CheckBox();
			_Equip_3D_View.Text = "Equip 3D View";
			_Equip_3D_View.GridColumn = 1;
			_Equip_3D_View.GridRow = 6;
			_Equip_3D_View.Id = "_Equip_3D_View";

			_Consist_3D_View = new CheckBox();
			_Consist_3D_View.Text = "Consist 3D View";
			_Consist_3D_View.GridColumn = 1;
			_Consist_3D_View.GridRow = 7;
			_Consist_3D_View.Id = "_Consist_3D_View";

			var grid1 = new Grid();
			grid1.ColumnSpacing = 1;
			grid1.RowSpacing = 1;
			grid1.DefaultColumnProportion = new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			};
			grid1.DefaultRowProportion = new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			};
			grid1.Widgets.Add(_Content_List);
			grid1.Widgets.Add(_Block_List);
			grid1.Widgets.Add(_Set_List);
			grid1.Widgets.Add(_Equip_List);
			grid1.Widgets.Add(_Consist_Units);
			grid1.Widgets.Add(_Equip_3D_View);
			grid1.Widgets.Add(_Consist_3D_View);

			var verticalStackPanel1 = new VerticalStackPanel();
			verticalStackPanel1.Spacing = 8;
			verticalStackPanel1.Widgets.Add(grid1);

			_SaveWindowView = new TextButton();
			_SaveWindowView.Text = "          Save           ";
			_SaveWindowView.TextColor = Microsoft.Xna.Framework.Color.Black;
			_SaveWindowView.BorderThickness = new Thickness(1);
			_SaveWindowView.Background = new SolidBrush("#B2B2B2FF");
			_SaveWindowView.Border = new SolidBrush("#FFFFFFFF");
			_SaveWindowView.Id = "_SaveWindowView";

			var verticalStackPanel2 = new VerticalStackPanel();
			verticalStackPanel2.Widgets.Add(_SaveWindowView);

			var verticalStackPanel3 = new VerticalStackPanel();
			verticalStackPanel3.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel3.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel3.Widgets.Add(verticalStackPanel1);
			verticalStackPanel3.Widgets.Add(verticalStackPanel2);

			
			Title = "Views";
			DragDirection = Myra.Graphics2D.UI.DragDirection.None;
			Left = 608;
			Top = 136;
			Id = "M_ViewWindow";
			Content = verticalStackPanel3;
		}

		
		public CheckBox _Content_List;
		public CheckBox _Block_List;
		public CheckBox _Set_List;
		public CheckBox _Equip_List;
		public CheckBox _Consist_Units;
		public CheckBox _Equip_3D_View;
		public CheckBox _Consist_3D_View;
		public TextButton _SaveWindowView;
	}
}
