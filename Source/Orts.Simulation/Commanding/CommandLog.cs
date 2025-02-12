﻿// COPYRIGHT 2012 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Orts.Simulation.Commanding
{
    /// <summary>
    /// User may specify an automatic pause in the replay at a time measured from the end of the replay.
    /// </summary>
    public enum ReplayPauseState
    {
        Before,
        Due,        // Set by CommandLog.Replay(), tested by Viewer.Update()
        During,
        Done
    };

    public class CommandLog
    {

        public Collection<ICommand> CommandList { get; private set; } = new Collection<ICommand>();
        public Simulator Simulator { get; set; }
        public bool ReplayComplete { get; set; }
        public double ReplayEndsAt { get; set; }
        public ReplayPauseState PauseState { get; set; }
        public bool CameraReplaySuspended { get; set; }

        private double completeTime;
        private DateTime? resumeTime;
        private const double completeDelayS = 2;

        /// <summary>
        /// Preferred constructor.
        /// </summary>
        public CommandLog(Simulator simulator)
        {
            Simulator = simulator;
        }

        /// <summary>
        /// When a command is created, it adds itself to the log.
        /// </summary>
        /// <param name="Command"></param>
        public void CommandAdd(ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command, nameof(command));
            command.Time = Simulator.ClockTime; // Note time that command was issued
            CommandList.Add(command);
        }

        /// <summary>
        /// Replays any commands that have become due.
        /// Issues commands from the replayCommandList at the same time that they were originally issued.
        /// <para>
        /// Assumes replayCommandList is already sorted by time.
        /// </para>
        /// </summary>
        public void Update(Collection<ICommand> replayCommandList)
        {
            ArgumentNullException.ThrowIfNull(replayCommandList, nameof(replayCommandList));
            double elapsedTime = Simulator.ClockTime;

            if (PauseState == ReplayPauseState.Before)
            {
                if (elapsedTime > ReplayEndsAt - Simulator.UserSettings.ReplayPauseDuration)
                {
                    PauseState = ReplayPauseState.Due;  // For Viewer.Update() to detect and pause.
                }
            }

            if (replayCommandList.Count > 0)
            {
                var c = replayCommandList[0];
                // Without a small margin, an activity event can pause simulator just before the ResumeActicityCommand is due, 
                // so resume never happens.
                double margin = (Simulator.GamePaused) ? 0.5 : 0;   // margin of 0.5 seconds
                if (elapsedTime >= c.Time - margin)
                {
                    if (c is PausedCommand)
                    {
                        // Wait for the right duration and then action the command.
                        // ActivityCommands need dedicated code as the clock is no longer advancing.
                        if (resumeTime == null)
                        {
                            var resumeCommand = (PausedCommand)c;
                            resumeTime = DateTime.UtcNow.AddSeconds(resumeCommand.PauseDurationS);
                        }
                        else
                        {
                            if (DateTime.UtcNow >= resumeTime)
                            {
                                resumeTime = null;  // cancel trigger
                                ReplayCommand(elapsedTime, replayCommandList, c);
                            }
                        }
                    }
                    else
                    {
                        // When the player uses a camera command during replay, replay continues but any camera commands in the 
                        // replayCommandList are skipped until the player pauses and exit from the Quit Menu.
                        // This allows some editing of the camera during a replay.
                        if (!(c is CameraCommand && CameraReplaySuspended))
                        {
                            ReplayCommand(elapsedTime, replayCommandList, c);
                        }
                        completeTime = elapsedTime + completeDelayS;  // Postpone the time for "Replay complete" message
                    }
                }
            }
            else
            {
                if (completeTime != 0 && elapsedTime > completeTime)
                {
                    completeTime = 0;       // Reset trigger so this only happens once
                    ReplayComplete = true;  // Flag seen by Viewer3D which announces "Replay complete".
                }
            }
        }

        private void ReplayCommand(double elapsedTime, Collection<ICommand> replayCommandList, ICommand c)
        {
            c.Redo();                           // Action the command
            CommandList.Add(c);               // Add to the log of commands
            replayCommandList.RemoveAt(0);    // Remove it from the head of the replay list
        }

        /// <summary>
        /// Copies the command objects from the log into the file specified, first creating the file.
        /// </summary>
        /// <param name="filePath"></param>
        public void SaveLog(string filePath)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(filePath, FileMode.Create);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
                BinaryFormatter formatter = new BinaryFormatter();
#pragma warning restore SYSLIB0011 // Type or member is obsolete
                // Re-sort based on time as tests show that some commands are deferred.
                CommandList = new Collection<ICommand>(CommandList.OrderBy(c => c.Time).ToList());
                formatter.Serialize(stream, CommandList);
            }
            catch (IOException)
            {
                // Do nothing but warn, ignoring errors.
                Trace.TraceWarning("SaveLog error writing command log " + filePath);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    Trace.WriteLine("\nList of commands to replay saved");
                }
            }
        }

        /// <summary>
        /// Copies the command objects from the file specified into the log, replacing the log's contents.
        /// </summary>
        /// <param name="fullFilePath"></param>
        public void LoadLog(string filePath)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(filePath, FileMode.Open);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
                BinaryFormatter formatter = new BinaryFormatter();
#pragma warning restore SYSLIB0011 // Type or member is obsolete
                CommandList = (Collection<ICommand>)formatter.Deserialize(stream);
            }
            catch (IOException)
            {
                // Do nothing but warn, ignoring errors.
                Trace.TraceWarning("LoadLog error reading command log " + filePath);
            }
            finally
            {
                if (stream != null)
                { stream.Close(); }
            }
        }

        public static void ReportReplayCommands(Collection<ICommand> list)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(list));
            Trace.WriteLine("\nList of commands to replay:");
            foreach (ICommand c in list)
            {
                c.Report();
            }

        }
    }
}
