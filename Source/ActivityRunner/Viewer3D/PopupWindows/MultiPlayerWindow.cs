using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Physics;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class MultiPlayerWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label labelTime;
        private Label labelStatus;
        private Label labelPLayersOnline;
        private Label labelTrains;
        private VerticalScrollboxControlLayout scrollboxPlayers;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly List<(string, double, Train)> onlineTrains = new List<(string, double, Train)>();
        private int columnWidth;
        private bool connectionLost;

        public MultiPlayerWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Multiplayer Info"), relativeLocation, new Point(260, 200), catalog)
        {
            Resize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling).AddLayoutVertical();
            columnWidth = layout.RemainingWidth / 4;
            ControlLayout line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Time:")));
            line.Add(labelTime = new Label(this, columnWidth, line.RemainingHeight, FormatStrings.FormatTime(Simulator.Instance.ClockTime + (MultiPlayerManager.MultiplayerState == MultiplayerState.Client ? MultiPlayerManager.Instance().ServerTimeDifference : 0))));
            layout.AddHorizontalSeparator();
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Player:")));
            line.Add(new Label(this, columnWidth * 3, line.RemainingHeight, MultiPlayerManager.Instance().UserName));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Status:")));
            line.Add(labelStatus = new Label(this, columnWidth * 3, line.RemainingHeight, MultiPlayerManager.Instance().GetMultiPlayerStatus()));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, line.RemainingHeight, $"Online:"));
            line.Add(labelPLayersOnline = new Label(this, columnWidth * 3, line.RemainingHeight, $"{MultiPlayerManager.OnlineTrains.Players.Count + 1} {Catalog.GetPluralString("player", "players", MultiPlayerManager.OnlineTrains.Players.Count + 1)}"));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Trains:")));
            line.Add(labelTrains = new Label(this, columnWidth * 3, line.RemainingHeight, $"{Simulator.Instance.Trains.Count} {Catalog.GetPluralString("train", "trains", Simulator.Instance.Trains.Count)}"));
            if (MultiPlayerManager.MultiplayerState != MultiplayerState.None)
            {
                layout.AddHorizontalSeparator();
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, columnWidth * 2, line.RemainingHeight, Catalog.GetString("Player Train")));
                line.Add(new Label(this, columnWidth * 2, line.RemainingHeight, Catalog.GetString("Distance")));
                layout.AddHorizontalSeparator();
                scrollboxPlayers = new VerticalScrollboxControlLayout(this, layout.RemainingWidth, layout.RemainingHeight);
                layout.Add(scrollboxPlayers);
                columnWidth = scrollboxPlayers.RemainingWidth / 4;
            }
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
            {
                labelTime.Text = FormatStrings.FormatTime(Simulator.Instance.ClockTime + (MultiPlayerManager.MultiplayerState == MultiplayerState.Client ? MultiPlayerManager.Instance().ServerTimeDifference : 0));
                labelStatus.Text = MultiPlayerManager.Instance().GetMultiPlayerStatus();
                labelPLayersOnline.Text = $"{MultiPlayerManager.OnlineTrains.Players.Count + 1} {Catalog.GetPluralString("player", "players", MultiPlayerManager.OnlineTrains.Players.Count + 1)}";
                labelTrains.Text = $"{Simulator.Instance.Trains.Count} {Catalog.GetPluralString("train", "trains", Simulator.Instance.Trains.Count)}";
                if (MultiPlayerManager.MultiplayerState != MultiplayerState.None)
                    UpdateTrains();
                else if (!connectionLost)
                {
                    connectionLost = true;
                    Resize();
                }
            }
        }

        private void Resize()
        {
            Resize(MultiPlayerManager.MultiplayerState == MultiplayerState.None ? new Point(260, 120) : new Point(260, 200));
        }

        private void UpdateTrains()
        {
            List<WindowControl> controls = new List<WindowControl>(scrollboxPlayers.Client.Controls);
            scrollboxPlayers.Clear();

            onlineTrains.Clear();
            Train playerTrain = Simulator.Instance.PlayerLocomotive.Train;
            foreach (OnlinePlayer p in MultiPlayerManager.OnlineTrains.Players.Values)
            {
                if (p.Train == null || p.Train.Cars.Count <= 0)
                    continue;
                double d = WorldLocation.GetDistanceSquared(p.Train.RearTDBTraveller.WorldLocation, playerTrain.RearTDBTraveller.WorldLocation);
                onlineTrains.Add((p.Username, Math.Sqrt(d) + StaticRandom.NextDouble(), p.Train));
            }
            onlineTrains.Sort(delegate ((string, double distance, Train) x, (string, double distance, Train) y)
            {
                return x.distance.CompareTo(y.distance);
            });
            foreach((string name, double distance, Train train) in onlineTrains)
            {
                if (controls.Where((x) => x.Tag == train).FirstOrDefault() is ControlLayout line)
                {
                    scrollboxPlayers.Client.Add(line);
                    (line.Controls[1] as Label).Text = $"{FormatStrings.FormatDistanceDisplay((int)distance, Simulator.Instance.Route.MilepostUnitsMetric)}";
                }
                else
                {
                    line = scrollboxPlayers.Client.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, columnWidth * 2, line.RemainingHeight, name));
                    line.Add(new Label(this, columnWidth * 2, line.RemainingHeight, $"{FormatStrings.FormatDistanceDisplay((int)distance, Simulator.Instance.Route.MilepostUnitsMetric)}"));
                    line.Tag = train;
                }
            }
            scrollboxPlayers.UpdateContent();
        }
    }
}
