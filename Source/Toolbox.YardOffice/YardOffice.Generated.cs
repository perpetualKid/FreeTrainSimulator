/* Generated by MyraPad at 9/2/2022 5:46:57 PM */
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

namespace Toolbox.YardOffice
{
	partial class YardOffice: VerticalStackPanel
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

			menuItemAbout = new MenuItem();
			menuItemAbout.Text = "About";
			menuItemAbout.Id = "menuItemAbout";

			menuItemHelp = new MenuItem();
			menuItemHelp.Text = "&Help";
			menuItemHelp.Id = "menuItemHelp";
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

			YOTotal = new TextBox();
			YOTotal.Text = "Total:    ";
			YOTotal.Id = "YOTotal";

			YOTotalBox = new TextBox();
			YOTotalBox.Text = "                           ";
			YOTotalBox.Background = new Myra.Graphics2D.Brushes.SolidBrush("#000000FF");
			YOTotalBox.Id = "YOTotalBox";

			var horizontalStackPanel1 = new HorizontalStackPanel();
			horizontalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel1.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel1.Widgets.Add(YOTotal);
			horizontalStackPanel1.Widgets.Add(YOTotalBox);

			YOShow = new TextBox();
			YOShow.Text = "Show:";
			YOShow.Id = "YOShow";

			YOConsistType = new ComboBox();
			YOConsistType.MaxWidth = 25;
			YOConsistType.Id = "YOConsistType";

			var horizontalStackPanel2 = new HorizontalStackPanel();
			horizontalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel2.Widgets.Add(YOShow);
			horizontalStackPanel2.Widgets.Add(YOConsistType);

			YOFilter = new TextBox();
			YOFilter.Text = "Filter:";
			YOFilter.Id = "YOFilter";

			YOFilterType = new ComboBox();
			YOFilterType.MaxWidth = 25;
			YOFilterType.Id = "YOFilterType";

			var horizontalStackPanel3 = new HorizontalStackPanel();
			horizontalStackPanel3.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel3.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel3.Widgets.Add(YOFilter);
			horizontalStackPanel3.Widgets.Add(YOFilterType);

			YOConsistList = new ListBox();
			YOConsistList.Id = "YOConsistList";

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
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			verticalStackPanel1.Widgets.Add(horizontalStackPanel1);
			verticalStackPanel1.Widgets.Add(horizontalStackPanel2);
			verticalStackPanel1.Widgets.Add(horizontalStackPanel3);
			verticalStackPanel1.Widgets.Add(YOConsistList);

			YO1Total = new TextBox();
			YO1Total.Text = "Total:    ";
			YO1Total.Id = "YO1Total";

			YO1TotalBox = new TextBox();
			YO1TotalBox.Text = "                           ";
			YO1TotalBox.Background = new Myra.Graphics2D.Brushes.SolidBrush("#000000FF");
			YO1TotalBox.Id = "YO1TotalBox";

			var horizontalStackPanel4 = new HorizontalStackPanel();
			horizontalStackPanel4.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel4.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel4.Widgets.Add(YO1Total);
			horizontalStackPanel4.Widgets.Add(YO1TotalBox);

			YO1Type = new TextBox();
			YO1Type.Text = "Type:";
			YO1Type.Id = "YO1Type";

			YO1TypeChoice = new ComboBox();
			YO1TypeChoice.MaxWidth = 25;
			YO1TypeChoice.Id = "YO1TypeChoice";

			var horizontalStackPanel5 = new HorizontalStackPanel();
			horizontalStackPanel5.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel5.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel5.Widgets.Add(YO1Type);
			horizontalStackPanel5.Widgets.Add(YO1TypeChoice);

			YO1Coupling = new TextBox();
			YO1Coupling.Text = "Coupling:";
			YO1Coupling.Id = "YO1Coupling";

			YO1CouplingFilter = new ComboBox();
			YO1CouplingFilter.MaxWidth = 25;
			YO1CouplingFilter.Id = "YO1CouplingFilter";

			var horizontalStackPanel6 = new HorizontalStackPanel();
			horizontalStackPanel6.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel6.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel6.Widgets.Add(YO1Coupling);
			horizontalStackPanel6.Widgets.Add(YO1CouplingFilter);

			YO1Search = new TextBox();
			YO1Search.Text = "Search:";
			YO1Search.Id = "YO1Search";

			YO1SearchBox = new TextBox();
			YO1SearchBox.Text = "                           ";
			YO1SearchBox.Background = new Myra.Graphics2D.Brushes.SolidBrush("#000000FF");
			YO1SearchBox.Id = "YO1SearchBox";

			var horizontalStackPanel7 = new HorizontalStackPanel();
			horizontalStackPanel7.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel7.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel7.Widgets.Add(YO1Search);
			horizontalStackPanel7.Widgets.Add(YO1SearchBox);

			YO1numadd = new TextBox();
			YO1numadd.Text = "Num to Add:";
			YO1numadd.Id = "YO1numadd";

			YO1numaddBox = new TextBox();
			YO1numaddBox.Text = "                           ";
			YO1numaddBox.Background = new Myra.Graphics2D.Brushes.SolidBrush("#000000FF");
			YO1numaddBox.Id = "YO1numaddBox";

			var horizontalStackPanel8 = new HorizontalStackPanel();
			horizontalStackPanel8.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel8.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel8.Widgets.Add(YO1numadd);
			horizontalStackPanel8.Widgets.Add(YO1numaddBox);

			var textButton1 = new TextButton();
			textButton1.Text = "Add Beg";
			textButton1.BorderThickness = new Thickness(1);
			textButton1.Border = new Myra.Graphics2D.Brushes.SolidBrush("#D9D9D9FF");

			var textButton2 = new TextButton();
			textButton2.Text = "Add Cur";
			textButton2.BorderThickness = new Thickness(1);
			textButton2.Border = new Myra.Graphics2D.Brushes.SolidBrush("#D9D9D9FF");

			var textButton3 = new TextButton();
			textButton3.Text = "Add End";
			textButton3.BorderThickness = new Thickness(1);
			textButton3.Border = new Myra.Graphics2D.Brushes.SolidBrush("#D9D9D9FF");

			var textButton4 = new TextButton();
			textButton4.Text = "Add Rand";
			textButton4.BorderThickness = new Thickness(1);
			textButton4.Border = new Myra.Graphics2D.Brushes.SolidBrush("#D9D9D9FF");

			var horizontalStackPanel9 = new HorizontalStackPanel();
			horizontalStackPanel9.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel9.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel9.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel9.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel9.Widgets.Add(textButton1);
			horizontalStackPanel9.Widgets.Add(textButton2);
			horizontalStackPanel9.Widgets.Add(textButton3);
			horizontalStackPanel9.Widgets.Add(textButton4);

			YO1ConsistList = new ListBox();
			YO1ConsistList.Id = "YO1ConsistList";

			var verticalStackPanel2 = new VerticalStackPanel();
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			verticalStackPanel2.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			verticalStackPanel2.Widgets.Add(horizontalStackPanel4);
			verticalStackPanel2.Widgets.Add(horizontalStackPanel5);
			verticalStackPanel2.Widgets.Add(horizontalStackPanel6);
			verticalStackPanel2.Widgets.Add(horizontalStackPanel7);
			verticalStackPanel2.Widgets.Add(horizontalStackPanel8);
			verticalStackPanel2.Widgets.Add(horizontalStackPanel9);
			verticalStackPanel2.Widgets.Add(YO1ConsistList);

			var panel1 = new Panel();
			panel1.BorderThickness = new Thickness(2);

			var panel2 = new Panel();
			panel2.BorderThickness = new Thickness(2);

			var horizontalStackPanel10 = new HorizontalStackPanel();
			horizontalStackPanel10.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel10.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Auto,
			});
			horizontalStackPanel10.Proportions.Add(new Proportion
			{
				Type = Myra.Graphics2D.UI.ProportionType.Fill,
			});
			horizontalStackPanel10.Background = new Myra.Graphics2D.Brushes.SolidBrush("#202020FF");
			horizontalStackPanel10.Widgets.Add(verticalStackPanel1);
			horizontalStackPanel10.Widgets.Add(verticalStackPanel2);
			horizontalStackPanel10.Widgets.Add(panel1);
			horizontalStackPanel10.Widgets.Add(panel2);

			var panel3 = new Panel();
			panel3.BorderThickness = new Thickness(2);

			var panel4 = new Panel();
			panel4.BorderThickness = new Thickness(2);

			
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
			Widgets.Add(horizontalStackPanel10);
			Widgets.Add(panel3);
			Widgets.Add(panel4);
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
		public MenuItem menuItemAbout;
		public MenuItem menuItemHelp;
		public HorizontalMenu MmainMenu;
		public TextBox YOTotal;
		public TextBox YOTotalBox;
		public TextBox YOShow;
		public ComboBox YOConsistType;
		public TextBox YOFilter;
		public ComboBox YOFilterType;
		public ListBox YOConsistList;
		public TextBox YO1Total;
		public TextBox YO1TotalBox;
		public TextBox YO1Type;
		public ComboBox YO1TypeChoice;
		public TextBox YO1Coupling;
		public ComboBox YO1CouplingFilter;
		public TextBox YO1Search;
		public TextBox YO1SearchBox;
		public TextBox YO1numadd;
		public TextBox YO1numaddBox;
		public ListBox YO1ConsistList;
	}
}
