using System;
using System.ComponentModel;
using System.Windows;

using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

namespace FreeTrainSimulator.Toolbox.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MapHost.HostedMenuReady += MapHost_HostedMenuReady;
        }

        private void MapHost_HostedMenuReady(object sender, EventArgs e)
        {
            if (MapHost.HostedMenu != null)
                MainMenu.DataContext = new ToolboxMenuViewModel(MapHost.HostedMenu, Dispatcher);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            MapHost.Dispose();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            MapHost.Dispose();
            base.OnClosed(e);
        }
    }
}
