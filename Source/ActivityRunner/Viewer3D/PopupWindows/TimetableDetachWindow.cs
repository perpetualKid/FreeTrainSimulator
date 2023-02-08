﻿using System.Collections.Generic;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Timetables;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class TimetableDetachWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label remainingTrain;
        private Label otherTrain;
        private Label buttonDetach;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private DetachInfo detachRequest;
        private TTTrain playerTrain;

        public TimetableDetachWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) : 
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Timetable Detach Menu"), relativeLocation, new Point(600, 126), catalog)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            ControlLayout line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, line.RemainingWidth, line.RemainingHeight, Catalog.GetString("This train is about to split.")));

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(remainingTrain = new Label(this, line.RemainingWidth, line.RemainingHeight, null));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(otherTrain = new Label(this, line.RemainingWidth, line.RemainingHeight, null));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, line.RemainingWidth, line.RemainingHeight, Catalog.GetString("Use 'cab switch' command to select cab in required train part.")));
            layout.AddSpace(0, Owner.TextFontDefault.Height);
            layout.AddHorizontalSeparator();
            layout.Add(buttonDetach = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString("Perform Detach"), HorizontalAlignment.Center));
            buttonDetach.OnClick += ButtonDetach_OnClick;
            return layout;
        }

        private void ButtonDetach_OnClick(object sender, MouseClickEventArgs e)
        {
            if (detachRequest != null)
            {
                detachRequest.DetachPlayerTrain(playerTrain, detachRequest.DetachFormedTrain);
                if (playerTrain.DetachDetails.ContainsKey(playerTrain.DetachActive[0]))
                {
                    playerTrain.DetachDetails.Remove(playerTrain.DetachActive[0]);
                }

                detachRequest.Valid = false;
                _ = Close();
            }
        }

        public override bool Open()
        {
            bool result = base.Open();
            if (result)
            {
                playerTrain = Simulator.Instance.PlayerLocomotive.Train as TTTrain;
                if (playerTrain?.DetachActive[1] >= 0)
                {
                    List<DetachInfo> detachList = playerTrain.DetachDetails[playerTrain.DetachActive[0]];
                    detachRequest = detachList[playerTrain.DetachActive[1]];
                    string formedTrain = Catalog.GetString("static consist");
                    if (!string.IsNullOrEmpty(detachRequest.DetachFormedTrainName))
                        formedTrain += $" : {detachRequest.DetachFormedTrainName}";

                    string formedPortion = Catalog.GetString("Rear");
                    string otherPortion = Catalog.GetString("Front");
                    if (playerTrain.DetachPosition)
                    {
                        (formedPortion, otherPortion) = (otherPortion, formedPortion);
                    }

                    if (detachRequest.CheckPlayerPowerPortion(playerTrain))
                    {
                        remainingTrain.Text = Catalog.GetString($"This portion will continue as train : {playerTrain.Name}");
                        otherTrain.Text = Catalog.GetString($"{formedPortion} portion will form train : {formedTrain}");
                    }
                    else
                    {
                        remainingTrain.Text = Catalog.GetString($"This portion will continue as train : {formedTrain}");
                        otherTrain.Text = Catalog.GetString($"{otherPortion} portion will form train : {playerTrain.Name}");
                    }
                }
            }
            return result;
        }
    }
}
