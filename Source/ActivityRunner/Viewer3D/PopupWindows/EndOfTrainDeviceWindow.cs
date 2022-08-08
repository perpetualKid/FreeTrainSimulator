using System;
using System.Collections.Generic;
using System.IO;

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
        private readonly Viewer viewer;
        private readonly List<string> availableEotContent = new List<string>();
        private readonly List<Label> eotLabels = new List<Label>();

        public EndOfTrainDeviceWindow(WindowManager owner, Point relativeLocation, Viewer viewer) :
            base(owner, "Available EOT", relativeLocation, new Point(200, 100))
        {
            this.viewer = viewer;
            foreach (string directory in Directory.EnumerateDirectories(Simulator.Instance.RouteFolder.ContentFolder.EndOfTrainDevicesFolder))
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*.eot"))
                {
                    availableEotContent.Add(file);
                }
            }
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            eotLabels.Clear();
            layout = base.Layout(layout, headerScaling);
            ControlLayout textLine = layout.AddLayoutHorizontalLineOfText();
            textLine.Add(new Label(this, textLine.RemainingWidth, textLine.RemainingHeight, Catalog.GetString("Device Name")));
            layout.AddHorizontalSeparator();

            ControlLayout scrollBox = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);
            foreach(string eotDevice in availableEotContent)
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
            return layout;
        }

        private void DeviceLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            if (sender is Label currentLabel)
            {
                //foreach (Label label in eotLabels)
                //    label.TextColor = label == currentLabel ? Color.OrangeRed : Color.White;

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
                    _ = new EOTMountCommand(viewer.Log, true, currentLabel.Tag as string);
                    currentLabel.TextColor = Color.OrangeRed;
                }
                else if ((currentLabel.Tag as string).Equals(playerLocomotive.Train.Cars[^1].WagFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    _ = new EOTMountCommand(viewer.Log, false, currentLabel.Tag as string);
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
            string playerEot = Simulator.Instance.PlayerLocomotive?.Train?.EndOfTrainDevice?.WagFilePath;

            foreach (Label label in eotLabels)
            {
                label.TextColor = (label.Tag as string).Equals(playerEot, StringComparison.OrdinalIgnoreCase) ? Color.OrangeRed : Color.White;
            }
            return base.Open();
        }
    }
}
