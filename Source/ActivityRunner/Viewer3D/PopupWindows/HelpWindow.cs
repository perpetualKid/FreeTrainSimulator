using System.ComponentModel;
using System.IO;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Settings;
using Orts.Settings.Util;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class HelpWindow : WindowBase
    {
        private enum TabSettings
        {
            [Description("Key Commands")]
            KeyboardShortcuts,
            [Description("Briefing")]
            ActivityBriefing,
            [Description("Timetable")]
            ActivityTimetable,
            [Description("Work Orders")]
            ActivityWorkOrders,
            [Description("Evaluation")]
            ActivityEvaluation,
            [Description("Briefing")]
            TimetableBriefing,
            [Description("Procedures")]
            LocomotiveProcedures,
        }

        private TabControl<TabSettings> tabControl;
        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly UserSettings settings;
        private readonly Viewer viewer;

        private ActivityTask lastActivityTask;
        private bool stoppedAt;
        private long lastEvalautionVersion = -1;
        private int lastLastEventID = -1;

        public HelpWindow(WindowManager owner, Point relativeLocation, Viewer viewer, UserSettings settings) :
            base(owner, "Help", relativeLocation, new Point(560, 380))
        {
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            this.settings = settings;
            this.viewer = viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            tabControl = new TabControl<TabSettings>(this, layout.RemainingWidth, layout.RemainingHeight, true);
            #region Keyboard tab
            tabControl.TabLayouts[TabSettings.KeyboardShortcuts] = (layoutContainer) =>
            {
                System.Drawing.Font keyFont = FontManager.Scaled(Owner.DefaultFontName, System.Drawing.FontStyle.Regular)[Owner.DefaultFontSize - 1];
                layoutContainer = layoutContainer.AddLayoutScrollboxVertical(layoutContainer.RemainingWidth);
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Center;

                int keyWidth = layoutContainer.RemainingWidth / KeyboardMap.MapWidth;
                int keyHeight = 3 * keyWidth + 2;
                ControlLayout keyboardMap = layoutContainer.AddLayoutPanel(KeyboardMap.MapWidth * keyWidth, keyHeight * KeyboardMap.KeyboardLayout.Length);
                KeyboardMap.DrawKeyboardMap((keyBox, keyScanCode, keyName) =>
                {
                    Color color = KeyboardMap.GetScanCodeColor(KeyboardMap.GetScanCodeCommands(keyScanCode, settings.Input.UserCommands));
                    if (color == Color.Transparent)
                        color = Color.Black;
                    KeyboardMap.Scale(ref keyBox, keyWidth, keyHeight);
                    keyboardMap.Add(new KeyLabel(this, keyBox.Left - keyboardMap.CurrentLeft, keyBox.Top - keyboardMap.CurrentTop, keyBox.Width - 1, keyBox.Height - 1, keyName, keyFont, color));
                });

                layoutContainer.AddHorizontalSeparator();
                ControlLayoutHorizontal headerLine = layoutContainer.AddLayoutHorizontalLineOfText();
                int width = headerLine.RemainingWidth / 2;
                headerLine.Add(new Label(this, width, headerLine.RemainingHeight, Catalog.GetString("Function")));
                headerLine.Add(new Label(this, width, headerLine.RemainingHeight, Catalog.GetString("Key")));
                layoutContainer.AddHorizontalSeparator();
                foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
                {
                    ControlLayoutHorizontal line = layoutContainer.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, width, line.RemainingHeight, command.GetLocalizedDescription()));
                    line.Add(new Label(this, width, line.RemainingHeight, settings.Input.UserCommands[command].ToString()));
                }
            };
            #endregion
            if (Simulator.Instance.ActivityFile != null)
            {
                #region Activity Briefing tab
                tabControl.TabLayouts[TabSettings.ActivityBriefing] = (layoutContainer) =>
                {
                    TextBox activityBriefing = new TextBox(this, layoutContainer.RemainingWidth, layoutContainer.RemainingHeight, Simulator.Instance.ActivityFile.Activity?.Header?.Briefing, true);
                    layoutContainer.Add(activityBriefing);
                };
                #endregion
                #region Activity Timetable tab
                tabControl.TabLayouts[TabSettings.ActivityTimetable] = (layoutContainer) =>
                {
                    int columnWidth = layoutContainer.RemainingWidth / 7;
                    var line = layoutContainer.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, columnWidth * 3, line.RemainingHeight, Catalog.GetString("Station")));
                    line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Arrive"), HorizontalAlignment.Center));
                    line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Actual"), HorizontalAlignment.Center));
                    line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Depart"), HorizontalAlignment.Center));
                    line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Actual"), HorizontalAlignment.Center));
                    layoutContainer.AddHorizontalSeparator();
                    ControlLayout scrollbox = layoutContainer.AddLayoutScrollboxVertical(layoutContainer.RemainingWidth);

                    if (viewer.Simulator.ActivityRun != null)
                    {
                        foreach (ActivityTaskPassengerStopAt activityTask in viewer.Simulator.ActivityRun.Tasks.OfType<ActivityTaskPassengerStopAt>())
                        {
                            line = scrollbox.AddLayoutHorizontalLineOfText();
                            line.Add(new Label(this, columnWidth * 3, line.RemainingHeight, activityTask.PlatformEnd1.Station));
                            line.Add(new Label(this, columnWidth, line.RemainingHeight, $"{activityTask.ScheduledArrival}", HorizontalAlignment.Center));
                            line.Add(new Label(this, columnWidth, line.RemainingHeight,
                                $"{(activityTask.ActualArrival.HasValue ? activityTask.ActualArrival : activityTask.IsCompleted.HasValue && activityTask.NextTask != null ? Catalog.GetString("(missed)") : string.Empty)}", HorizontalAlignment.Center)
                            { TextColor = Popups.NextStationWindow.GetArrivalColor(activityTask.ScheduledArrival, activityTask.ActualArrival) });
                            line.Add(new Label(this, columnWidth, line.RemainingHeight, $"{activityTask.ScheduledDeparture}", HorizontalAlignment.Center));
                            line.Add(new Label(this, columnWidth, line.RemainingHeight,
                                $"{(activityTask.ActualDeparture.HasValue ? activityTask.ActualDeparture : activityTask.IsCompleted.HasValue && activityTask.NextTask != null ? Catalog.GetString("(missed)") : string.Empty)}", HorizontalAlignment.Center)
                            { TextColor = Popups.NextStationWindow.GetDepartColor(activityTask.ScheduledDeparture, activityTask.ActualDeparture) });
                        }
                    }
                };
                #endregion
                #region Activity Work Orders
                tabControl.TabLayouts[TabSettings.ActivityWorkOrders] = (layoutContainer) =>
                {
                    int columnWidth = layoutContainer.RemainingWidth / 20;
                    {
                        ControlLayout line = layoutContainer.AddLayoutHorizontalLineOfText();
                        line.Add(new Label(this, columnWidth * 4, line.RemainingHeight, Catalog.GetString("Task")));
                        line.Add(new Label(this, columnWidth * 6, line.RemainingHeight, Catalog.GetString("Car(s)")));
                        line.Add(new Label(this, columnWidth * 7, line.RemainingHeight, Catalog.GetString("Location")));
                        line.Add(new Label(this, columnWidth * 6, line.RemainingHeight, Catalog.GetString("Status")));
                    }
                    layoutContainer.AddHorizontalSeparator();
                    ControlLayout scrollbox = layoutContainer.AddLayoutScrollboxVertical(layoutContainer.RemainingWidth);
                    bool addSeparator = false;
                    foreach (EventWrapper eventWrapper in Simulator.Instance.ActivityRun.EventList ?? Enumerable.Empty<EventWrapper>())
                    {
                        if (eventWrapper.ActivityEvent is ActionActivityEvent activityEvent)
                        {
                            if (addSeparator)
                                scrollbox.AddHorizontalSeparator();
                            ControlLayout line = scrollbox.AddLayoutHorizontalLineOfText();
                            // Task column
                            switch (activityEvent.Type)
                            {
                                case EventType.AssembleTrain:
                                case EventType.AssembleTrainAtLocation:
                                    line.Add(new Label(this, columnWidth * 4, line.RemainingHeight, Catalog.GetString("Assemble Train")));
                                    if (activityEvent.Type == EventType.AssembleTrainAtLocation)
                                    {
                                        line = scrollbox.AddLayoutHorizontalLineOfText();
                                        line.Add(new Label(this, columnWidth * 4, line.RemainingHeight, Catalog.GetString("At Location")));
                                    }
                                    break;
                                case EventType.DropOffWagonsAtLocation:
                                    line.Add(new Label(this, columnWidth * 4, line.RemainingHeight, Catalog.GetString("Drop Off")));
                                    break;
                                case EventType.PickUpPassengers:
                                case EventType.PickUpWagons:
                                    line.Add(new Label(this, columnWidth * 4, line.RemainingHeight, Catalog.GetString("Pick Up")));
                                    break;
                            }
                            if (activityEvent.WorkOrderWagons != null)
                            {
                                string location = "";
                                bool locationShown = false;
                                int wagonIdx = 0;
                                string locationFirst = "";
                                foreach (WorkOrderWagon wagonItem in activityEvent.WorkOrderWagons)
                                {
                                    if (locationShown)
                                    {
                                        line = scrollbox.AddLayoutHorizontalLineOfText();
                                        line.AddSpace(columnWidth * 4, 0);
                                    }

                                    // Car(s) column
                                    // Wagon.UiD contains train and wagon indexes packed into single 32-bit value, e.g. 32678 - 0
                                    uint trainIndex = wagonItem.UiD >> 16;         // Extract upper 16 bits
                                    uint wagonIndex = wagonItem.UiD & 0x0000FFFF;  // Extract lower 16 bits
                                    string wagonName = $"{trainIndex} - {wagonIndex}";
                                    string wagonType = "";
                                    bool wagonFound = false;
                                    if (Simulator.Instance.ActivityFile.Activity.ActivityObjects != null)
                                    {
                                        foreach (ActivityObject activityObject in Simulator.Instance.ActivityFile.Activity.ActivityObjects)
                                        {
                                            if (activityObject.ID == trainIndex)
                                            {
                                                foreach (Wagon trainWagon in activityObject.TrainSet.Wagons)
                                                {
                                                    if (trainWagon.UiD == wagonIndex)
                                                    {
                                                        wagonType = trainWagon.Name;
                                                        wagonFound = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            if (wagonFound)
                                                break;
                                        }
                                        if (!wagonFound)
                                        {
                                            foreach (var car in viewer.PlayerTrain.Cars)
                                            {
                                                if (car.UiD == wagonItem.UiD)
                                                {
                                                    wagonType = Path.GetFileNameWithoutExtension(car.WagFilePath);
                                                    wagonFound = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    line.Add(new Label(this, columnWidth * 6, line.RemainingHeight, $"{wagonName} ({wagonType})"));

                                    // Location column
                                    if (locationShown &&
                                        !((activityEvent.Type == EventType.PickUpPassengers) || (activityEvent.Type == EventType.PickUpWagons)))
                                    {
                                        line.AddSpace(columnWidth * 7, 0);
                                    }
                                    else
                                    {
                                        int sidingId = activityEvent.Type == EventType.AssembleTrainAtLocation || activityEvent.Type == EventType.DropOffWagonsAtLocation
                                            ? activityEvent.SidingId : wagonItem.SidingId;
                                        foreach (TrackItem item in RuntimeData.Instance.TrackDB.TrackItems)
                                        {
                                            if (item is SidingItem siding && siding.TrackItemId == sidingId)
                                            {
                                                location = siding.ItemName;
                                                break;
                                            }
                                        }
                                        if (locationFirst != location)
                                        {
                                            line.Add(new Label(this, columnWidth * 7, line.RemainingHeight, location));
                                        }
                                        else if (location.Length == 0 | (activityEvent.Type == EventType.PickUpPassengers) || (activityEvent.Type == EventType.PickUpWagons))
                                            line.AddSpace(columnWidth * 7, 0);
                                        locationFirst = location;
                                        locationShown = true;
                                    }
                                    // Status column
                                    if (eventWrapper.TimesTriggered == 1 && wagonIdx == 0)
                                    {
                                        line.Add(new Label(this, columnWidth * 6, line.RemainingHeight, Catalog.GetString("Done")));
                                    }
                                    else
                                        line.Add(new Label(this, columnWidth, line.RemainingHeight, ""));
                                    wagonIdx++;

                                    addSeparator = true;
                                }
                            }
                        }

                    }
                };
                #endregion
                #region Activity Evaluation
                if (settings.ActivityEvalulation)
                {

                }
                #endregion
            }
            else if (Simulator.Instance.TimetableMode)
            {
                tabControl.TabLayouts[TabSettings.TimetableBriefing] = (layoutContainer) =>
                {
                    TextBox timetableBriefing = new TextBox(this, layoutContainer.RemainingWidth, layoutContainer.RemainingHeight, (viewer.SelectedTrain as Orts.Simulation.Timetables.TTTrain)?.Briefing, false);
                    layoutContainer.Add(timetableBriefing);
                };
            }
            tabControl.TabLayouts[TabSettings.LocomotiveProcedures] = (layoutContainer) =>
            {
                TextBox proceduresText = new TextBox(this, layoutContainer.RemainingWidth, layoutContainer.RemainingHeight, (Simulator.Instance.PlayerLocomotive as MSTSLocomotive)?.EngineOperatingProcedures, false);
                layoutContainer.Add(proceduresText);
            };
            layout.Add(tabControl);
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                if (Simulator.Instance.ActivityRun != null)
                {
                    if (tabControl.CurrentTab == TabSettings.ActivityTimetable | tabControl.CurrentTab == TabSettings.ActivityEvaluation)
                    {
                        if (lastActivityTask != Simulator.Instance.ActivityRun.ActivityTask || stoppedAt != (lastActivityTask is ActivityTaskPassengerStopAt preTest && preTest.ActualArrival != null))
                        {
                            lastActivityTask = Simulator.Instance.ActivityRun.ActivityTask;
                            stoppedAt = (lastActivityTask is ActivityTaskPassengerStopAt stopAtTask && stopAtTask.ActualArrival != null);
                            tabControl.UpdateTabLayout(tabControl.CurrentTab);
                        }
                    }
                    else if (tabControl.CurrentTab == TabSettings.ActivityWorkOrders || tabControl.CurrentTab == TabSettings.ActivityEvaluation)
                    {
                        if (Simulator.Instance.ActivityRun.EventList != null)
                        {
                            if (Simulator.Instance.ActivityRun.LastTriggeredActivityEvent != null && (lastLastEventID == -1 ||
                                (Simulator.Instance.ActivityRun.LastTriggeredActivityEvent.ActivityEvent.ID != lastLastEventID)))
                            {
                                lastLastEventID = Simulator.Instance.ActivityRun.LastTriggeredActivityEvent.ActivityEvent.ID;
                                tabControl.UpdateTabLayout(tabControl.CurrentTab);
                            }
                        }
                    }
                    else if (tabControl.CurrentTab == TabSettings.ActivityEvaluation && 
                        (Simulator.Instance.ActivityRun.Completed || ActivityEvaluation.Instance.Version != lastEvalautionVersion))
                    {
                        lastEvalautionVersion = ActivityEvaluation.Instance.Version;
                        tabControl.UpdateTabLayout(tabControl.CurrentTab);
                    }
                }
            }
            base.Update(gameTime, shouldUpdate);
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayHelpWindow, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayHelpWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                tabControl?.TabAction();
            }
        }
    }
}
