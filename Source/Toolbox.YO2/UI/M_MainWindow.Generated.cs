/* Generated by MyraPad at 9/9/2022 1:27:58 PM */
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
	partial class M_MainWindow: VerticalStackPanel
	{
		private void BuildUI()
		{
			menuItemSelect = new MenuItem();
			menuItemSelect.Text = "Se&Lect";
			menuItemSelect.Id = "menuItemSelect";

			menuItemQuit = new MenuItem();
			menuItemQuit.Text = "&Quit";
			menuItemQuit.Id = "menuItemQuit";

			menuItemRoute = new MenuItem();
			menuItemRoute.Text = "&Route";
			menuItemRoute.Id = "menuItemRoute";
			menuItemRoute.Items.Add(menuItemSelect);
			menuItemRoute.Items.Add(menuItemQuit);

			menuItemConsist = new MenuItem();
			menuItemConsist.Text = "&Consist";
			menuItemConsist.Id = "menuItemConsist";

			menuItemEng = new MenuItem();
			menuItemEng.Text = "&Eng";
			menuItemEng.Id = "menuItemEng";

			menuItemReplace = new MenuItem();
			menuItemReplace.Text = "&Replace";
			menuItemReplace.Id = "menuItemReplace";

			menuItemView = new MenuItem();
			menuItemView.Text = "&View";
			menuItemView.Id = "menuItemView";

			menuItem3DView = new MenuItem();
			menuItem3DView.Text = "&3D View";
			menuItem3DView.Id = "menuItem3DView";

			menuItemSettings = new MenuItem();
			menuItemSettings.Text = "&Settings";
			menuItemSettings.Id = "menuItemSettings";

			menuItemDebug = new MenuItem();
			menuItemDebug.Text = "Debug";
			menuItemDebug.Id = "menuItemDebug";

			menuItemAbout = new MenuItem();
			menuItemAbout.Text = "About";
			menuItemAbout.Id = "menuItemAbout";

			menuItemHelp = new MenuItem();
			menuItemHelp.Text = "&Help";
			menuItemHelp.Id = "menuItemHelp";
			menuItemHelp.Items.Add(menuItemDebug);
			menuItemHelp.Items.Add(menuItemAbout);

			MmainMenu = new HorizontalMenu();
			MmainMenu.VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment.Stretch;
			MmainMenu.Id = "MmainMenu";
			MmainMenu.Items.Add(menuItemRoute);
			MmainMenu.Items.Add(menuItemConsist);
			MmainMenu.Items.Add(menuItemEng);
			MmainMenu.Items.Add(menuItemReplace);
			MmainMenu.Items.Add(menuItemView);
			MmainMenu.Items.Add(menuItem3DView);
			MmainMenu.Items.Add(menuItemSettings);
			MmainMenu.Items.Add(menuItemHelp);

			var horizontalSeparator1 = new HorizontalSeparator();

			var textBox1 = new TextBox();
			textBox1.Text = "Count:";

			_TrainCount = new TextBox();
			_TrainCount.GridColumn = 1;
			_TrainCount.Id = "_TrainCount";

			var textBox2 = new TextBox();
			textBox2.Text = "Show:";
			textBox2.GridRow = 1;

			var listItem1 = new ListItem();
			listItem1.Text = "Train Consists    ";

			var listItem2 = new ListItem();
			listItem2.Text = "Activity Consists ";

			_TrainShow = new ComboBox();
			_TrainShow.GridColumn = 1;
			_TrainShow.GridRow = 1;
			_TrainShow.Id = "_TrainShow";
			_TrainShow.Items.Add(listItem1);
			_TrainShow.Items.Add(listItem2);

			var textBox3 = new TextBox();
			textBox3.Text = "Filter:";
			textBox3.GridRow = 2;

			var listItem3 = new ListItem();
			listItem3.Text = "All                       ";

			var listItem4 = new ListItem();
			listItem4.Text = "Broken               ";

			var listItem5 = new ListItem();
			listItem5.Text = "Unsaved            ";

			var listItem6 = new ListItem();
			listItem6.Text = "Last Query";

			_TrainFilter = new ComboBox();
			_TrainFilter.GridColumn = 1;
			_TrainFilter.GridRow = 2;
			_TrainFilter.Id = "_TrainFilter";
			_TrainFilter.Items.Add(listItem3);
			_TrainFilter.Items.Add(listItem4);
			_TrainFilter.Items.Add(listItem5);
			_TrainFilter.Items.Add(listItem6);

			var grid1 = new Grid();
			grid1.ShowGridLines = true;
			grid1.ColumnsProportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Part,
			});
			grid1.ColumnsProportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Part,
				Value = 2,
			});
			grid1.Widgets.Add(textBox1);
			grid1.Widgets.Add(_TrainCount);
			grid1.Widgets.Add(textBox2);
			grid1.Widgets.Add(_TrainShow);
			grid1.Widgets.Add(textBox3);
			grid1.Widgets.Add(_TrainFilter);

			var verticalStackPanel1 = new VerticalStackPanel();
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			verticalStackPanel1.Widgets.Add(grid1);

			M_TrainPanel = new Panel();
			M_TrainPanel.Width = 250;
			M_TrainPanel.Background = new SolidBrush("#FF9503FF");
			M_TrainPanel.Id = "M_TrainPanel";
			M_TrainPanel.Widgets.Add(verticalStackPanel1);

			var grid2 = new Grid();
			grid2.RowsProportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Part,
				Value = 3,
			});
			grid2.RowsProportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Part,
			});
			grid2.Widgets.Add(M_TrainPanel);

			var horizontalStackPanel1 = new HorizontalStackPanel();
			horizontalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel1.Widgets.Add(grid2);

			
			Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			Widgets.Add(MmainMenu);
			Widgets.Add(horizontalSeparator1);
			Widgets.Add(horizontalStackPanel1);
		}

		
		public MenuItem menuItemSelect;
		public MenuItem menuItemQuit;
		public MenuItem menuItemRoute;
		public MenuItem menuItemConsist;
		public MenuItem menuItemEng;
		public MenuItem menuItemReplace;
		public MenuItem menuItemView;
		public MenuItem menuItem3DView;
		public MenuItem menuItemSettings;
		public MenuItem menuItemDebug;
		public MenuItem menuItemAbout;
		public MenuItem menuItemHelp;
		public HorizontalMenu MmainMenu;
		public TextBox _TrainCount;
		public ComboBox _TrainShow;
		public ComboBox _TrainFilter;
		public Panel M_TrainPanel;
	}
}