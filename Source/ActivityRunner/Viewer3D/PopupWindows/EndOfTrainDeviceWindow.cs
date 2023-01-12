using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class EndOfTrainDeviceWindow : WindowBase
    {
        private readonly List<string> availableEotContent = new List<string>();
        private readonly List<Label> eotLabels = new List<Label>();

        public EndOfTrainDeviceWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Available EOT"), relativeLocation, new Point(200, 100), catalog)
        {
            if (Directory.Exists(Simulator.Instance.RouteFolder.ContentFolder.EndOfTrainDevicesFolder))
            {
                foreach (string directory in Directory.EnumerateDirectories(Simulator.Instance.RouteFolder.ContentFolder.EndOfTrainDevicesFolder))
                {
                    foreach (string file in Directory.EnumerateFiles(directory, "*.eot"))
                    {
                        availableEotContent.Add(file);
                    }
                }
            }
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            eotLabels.Clear();
            layout = base.Layout(layout, headerScaling);
            if (availableEotContent.Count > 0)
            {
                ControlLayout textLine = layout.AddLayoutHorizontalLineOfText();
                textLine.Add(new Label(this, textLine.RemainingWidth, textLine.RemainingHeight, Catalog.GetString("Device Name")));
                layout.AddHorizontalSeparator();

                ControlLayout scrollBox = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
                foreach (string eotDevice in availableEotContent)
                {
                    textLine = scrollBox.AddLayoutHorizontalLineOfText();
                    Label deviceLabel = new Label(this, textLine.RemainingWidth, textLine.RemainingHeight, Path.GetFileNameWithoutExtension(eotDevice))
                    {
                        Tag = eotDevice
                    };
                    deviceLabel.OnClick += DeviceLabel_OnClick;
                    textLine.Add(deviceLabel);
                    eotLabels.Add(deviceLabel);
                }
            }
            else
            {
                ControlLayout textLine = layout.AddLayoutHorizontalLineOfText();
                textLine.Add(new Label(this, textLine.RemainingWidth, textLine.RemainingHeight, Catalog.GetString("No EOT devices available")));
            }
            return layout;
        }

        private void DeviceLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            if (sender is Label currentLabel)
            {
                TrainCar playerLocomotive = Simulator.Instance.PlayerLocomotive;
                if (playerLocomotive == null)
                    return;
                if (playerLocomotive.AbsSpeedMpS > Simulator.MaxStoppedMpS)
                {
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Can't attach EOT if player train not stopped"));
                    return;
                }
                if (!(currentLabel.Tag as string).Equals(playerLocomotive.Train.Cars[^1].WagFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (playerLocomotive.Train?.EndOfTrainDevice != null)
                    {
                        Simulator.Instance.Confirmer.Information(Catalog.GetString("Player train already has a mounted EOT"));
                        return;
                    }
                    //Ask to mount EOT
                    _ = new EOTMountCommand(Simulator.Instance.Log, true, currentLabel.Tag as string);
                    currentLabel.TextColor = Color.OrangeRed;
                }
                else if ((currentLabel.Tag as string).Equals(playerLocomotive.Train.Cars[^1].WagFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    _ = new EOTMountCommand(Simulator.Instance.Log, false, currentLabel.Tag as string);
                    currentLabel.TextColor = Color.White;
                }
                else
                {
                    Simulator.Instance.Confirmer.Information(Catalog.GetString("Can't mount an EOT if another one is mounted"));
                    return;
                }
            }
        }

        public override bool Open()
        {
            bool result = base.Open();
            if (result)
            {
                string playerEot = Simulator.Instance.PlayerLocomotive?.Train?.EndOfTrainDevice?.WagFilePath;

                foreach (Label label in eotLabels)
                {
                    label.TextColor = (label.Tag as string).Equals(playerEot, StringComparison.OrdinalIgnoreCase) ? Color.OrangeRed : Color.White;
                }
            }
            return result;
        }
    }
}
