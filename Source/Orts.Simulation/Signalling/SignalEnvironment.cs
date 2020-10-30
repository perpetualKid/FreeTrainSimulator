// COPYRIGHT 2013 by the Open Rails project.
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

// This module covers all classes and code for signal, speed post, track occupation and track reservation control

// Debug flags :
// #define DEBUG_PRINT
// prints details of the derived signal structure
// #define DEBUG_REPORTS
// print details of train behaviour
// #define DEBUG_DEADLOCK
// print details of deadlock processing

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Threading;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.MultiPlayer;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Signalling
{


    //================================================================================================//
    //================================================================================================//
    /// <summary>
    /// Class Signals
    /// </summary>
    public class SignalEnvironment
    {

        //================================================================================================//
        // local data
        //================================================================================================//

        internal readonly Simulator Simulator;

        /// Gets an array of all the SignalObjects.
        public IList<Signal> SignalObjects { get; private set; }

        private readonly TrackDB trackDB;
        private TrackSectionsFile tsectiondat;
        private List<SignalWorldInfo> SignalWorldList = new List<SignalWorldInfo>();
        private Dictionary<uint, SignalReferenceInfo> SignalRefList;
        private Dictionary<uint, Signal> SignalHeadList;
        public static SIGSCRfile SignaScriptsFile { get; private set; }
        public int OrtsSignalTypeCount { get; private set; }

        private int foundSignals;

        private static int updatecount;

        public List<TrackCircuitSection> TrackCircuitList => TrackCircuitSection.TrackCircuitList;
        private Dictionary<int, CrossOverInfo> CrossoverList = new Dictionary<int, CrossOverInfo>();
        public List<PlatformDetails> PlatformDetailsList = new List<PlatformDetails>();
        public Dictionary<int, int> PlatformXRefList = new Dictionary<int, int>();
        private Dictionary<int, uint> PlatformSidesList = new Dictionary<int, uint>();
        public Dictionary<string, List<int>> StationXRefList = new Dictionary<string, List<int>>();

        public bool UseLocationPassingPaths;                    // Use location-based style processing of passing paths (set by Simulator)
        internal Dictionary<int, DeadlockInfo> DeadlockInfoList;  // each deadlock info has unique reference
        public int deadlockIndex;                               // last used reference index
        public Dictionary<int, int> DeadlockReference;          // cross-reference between trackcircuitsection (key) and deadlockinforeference (value)

        public List<Milepost> MilepostList = new List<Milepost>();                     // list of mileposts
        private int foundMileposts;

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public SignalEnvironment(Simulator simulator, SignalConfigurationFile sigcfg, CancellationToken cancellation)
        {
            Simulator = simulator;

            SignalRefList = new Dictionary<uint, SignalReferenceInfo>();
            SignalHeadList = new Dictionary<uint, Signal>();
            Dictionary<int, int> platformList = new Dictionary<int, int>();

            OrtsSignalTypeCount = OrSignalTypes.Instance.FunctionTypes.Count;

            trackDB = simulator.TDB.TrackDB;
            tsectiondat = simulator.TSectionDat;

            // read SIGSCR files

            Trace.Write(" SIGSCR ");
            SignaScriptsFile = new SIGSCRfile(new SignalScripts(sigcfg.ScriptPath, sigcfg.ScriptFiles, sigcfg.SignalTypes));

            // build list of signal world file information

            BuildSignalWorld(simulator, sigcfg, cancellation);

            // build list of signals in TDB file

            BuildSignalList(trackDB.TrackItems, trackDB.TrackNodes, tsectiondat, Simulator.TDB, platformList, MilepostList);

            if (foundSignals > 0)
            {
                // Add CFG info

                AddCFG(sigcfg);

                // Add World info

                AddWorldInfo();

                // check for any backfacing heads in signals
                // if found, split signal

                SplitBackfacing(trackDB.TrackItems, trackDB.TrackNodes);
            }

            if (SignalObjects != null)
                SetNumSignalHeads();

            //
            // Create trackcircuit database
            //
            CreateTrackCircuits(trackDB.TrackItems, trackDB.TrackNodes, tsectiondat);

            //
            // Process platform information
            //

            ProcessPlatforms(platformList, trackDB.TrackItems, trackDB.TrackNodes, PlatformSidesList);

            //
            // Process tunnel information
            //

            ProcessTunnels();

            //
            // Process trough information
            //

            ProcessTroughs();

            //
            // Print all info (DEBUG only)
            //

#if DEBUG_PRINT

            PrintTCBase(trackDB.TrackNodes);

            if (File.Exists(@"C:\temp\SignalObjects.txt"))
            {
                File.Delete(@"C:\temp\SignalObjects.txt");
            }
            if (File.Exists(@"C:\temp\SignalShapes.txt"))
            {
                File.Delete(@"C:\temp\SignalShapes.txt");
            }

			var sob = new StringBuilder();
            for (var isignal = 0; isignal < signalObjects.Length - 1; isignal++)
            {
				var singleSignal = signalObjects[isignal];
                if (singleSignal == null)
                {
					sob.AppendFormat("\nInvalid entry : {0}\n", isignal);
                }
                else
                {
					sob.AppendFormat("\nSignal ref item     : {0}\n", singleSignal.thisRef);
					sob.AppendFormat("Track node + index  : {0} + {1}\n", singleSignal.trackNode, singleSignal.trRefIndex);

                    foreach (var thisHead in singleSignal.SignalHeads)
                    {
						sob.AppendFormat("Type name           : {0}\n", thisHead.signalType.Name);
						sob.AppendFormat("Type                : {0}\n", thisHead.signalType.FnType);
                        sob.AppendFormat("OR Type             : {0}\n", thisHead.signalType.ORTSFnType);
                        sob.AppendFormat("OR Type Index       : {0}\n", thisHead.signalType.ORTSFnTypeIndex);
                        sob.AppendFormat("item Index          : {0}\n", thisHead.trItemIndex);
						sob.AppendFormat("TDB  Index          : {0}\n", thisHead.TDBIndex);
						sob.AppendFormat("Junction Main Node  : {0}\n", thisHead.JunctionMainNode);
						sob.AppendFormat("Junction Path       : {0}\n", thisHead.JunctionPath);
                    }

					sob.AppendFormat("TC Reference   : {0}\n", singleSignal.TCReference);
					sob.AppendFormat("TC Direction   : {0}\n", singleSignal.TCDirection);
					sob.AppendFormat("TC Position    : {0}\n", singleSignal.TCOffset);
					sob.AppendFormat("TC TCNextTC    : {0}\n", singleSignal.TCNextTC);
                }
            }
			File.AppendAllText(@"C:\temp\SignalObjects.txt", sob.ToString());

			var ssb = new StringBuilder();
            foreach (var sshape in sigcfg.SignalShapes)
            {
				var thisshape = sshape.Value;
				ssb.Append("\n==========================================\n");
				ssb.AppendFormat("Shape key   : {0}\n", sshape.Key);
				ssb.AppendFormat("Filename    : {0}\n", thisshape.ShapeFileName);
				ssb.AppendFormat("Description : {0}\n", thisshape.Description);

                foreach (var ssobj in thisshape.SignalSubObjs)
                {
					ssb.AppendFormat("\nSubobj Index : {0}\n", ssobj.Index);
					ssb.AppendFormat("Matrix       : {0}\n", ssobj.MatrixName);
					ssb.AppendFormat("Description  : {0}\n", ssobj.Description);
					ssb.AppendFormat("Sub Type (I) : {0}\n", ssobj.SignalSubType);
                    if (ssobj.SignalSubSignalType != null)
                    {
						ssb.AppendFormat("Sub Type (C) : {0}\n", ssobj.SignalSubSignalType);
                    }
                    else
                    {
						ssb.AppendFormat("Sub Type (C) : not set \n");
                    }
					ssb.AppendFormat("Optional     : {0}\n", ssobj.Optional);
					ssb.AppendFormat("Default      : {0}\n", ssobj.Default);
					ssb.AppendFormat("BackFacing   : {0}\n", ssobj.BackFacing);
					ssb.AppendFormat("JunctionLink : {0}\n", ssobj.JunctionLink);
                }
				ssb.Append("\n==========================================\n");
            }
			File.AppendAllText(@"C:\temp\SignalShapes.txt", ssb.ToString());
#endif

            // Clear world lists to save memory

            SignalWorldList.Clear();
            SignalRefList.Clear();
            SignalHeadList.Clear();

            foreach (Signal signal in SignalObjects ?? Enumerable.Empty<Signal>())
            {
                signal?.ValidateSignal();
            }

            DeadlockInfoList = new Dictionary<int, DeadlockInfo>();
            deadlockIndex = 1;
            DeadlockReference = new Dictionary<int, int>();
        }

        //================================================================================================//
        /// <summary>
        /// Overlay constructor for restore after saved game
        /// </summary>

        public SignalEnvironment(Simulator simulator, SignalConfigurationFile sigcfg, BinaryReader inf, CancellationToken cancellation)
            : this(simulator, sigcfg, cancellation)
        {
            int signalIndex = inf.ReadInt32();
            while (signalIndex >= 0)
            {
                Signal thisSignal = SignalObjects[signalIndex];
                thisSignal.Restore(simulator, inf);
                signalIndex = inf.ReadInt32();
            }

            int tcListCount = inf.ReadInt32();

            if (tcListCount != TrackCircuitList.Count)
            {
                Trace.TraceError("Mismatch between saved : {0} and existing : {1} TrackCircuits", tcListCount, TrackCircuitList.Count);
                throw new InvalidDataException("Cannot resume route due to altered data");
            }
            else
            {
                foreach (TrackCircuitSection thisSection in TrackCircuitList)
                {
                    thisSection.Restore(simulator, inf);
                }
            }

            UseLocationPassingPaths = inf.ReadBoolean();

            DeadlockInfoList = new Dictionary<int, DeadlockInfo>();
            int totalDeadlocks = inf.ReadInt32();
            for (int iDeadlock = 0; iDeadlock <= totalDeadlocks - 1; iDeadlock++)
            {
                int thisDeadlockIndex = inf.ReadInt32();
                DeadlockInfo thisInfo = new DeadlockInfo(this, inf);
                DeadlockInfoList.Add(thisDeadlockIndex, thisInfo);
            }

            deadlockIndex = inf.ReadInt32();

            DeadlockReference = new Dictionary<int, int>();
            int totalReferences = inf.ReadInt32();
            for (int iReference = 0; iReference <= totalReferences - 1; iReference++)
            {
                int thisSectionIndex = inf.ReadInt32();
                int thisDeadlockIndex = inf.ReadInt32();
                DeadlockReference.Add(thisSectionIndex, thisDeadlockIndex);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Train links
        /// Train links must be restored separately as Trains is restored later as Signals
        /// </summary>

        public void RestoreTrains(List<Train> trains)
        {
            foreach (TrackCircuitSection thisSection in TrackCircuitList)
            {
                thisSection.CircuitState.RestoreTrains(trains, thisSection.Index);
            }

            // restore train information

            if (SignalObjects != null)
            {
                foreach (Signal thisSignal in SignalObjects)
                {
                    if (thisSignal != null)
                    {
                        thisSignal.RestoreTrains(trains);
                    }
                }

                // restore correct aspects
                foreach (Signal thisSignal in SignalObjects)
                {
                    if (thisSignal != null)
                    {
                        thisSignal.RestoreAspect();
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Save game
        /// </summary>

        public void Save(BinaryWriter outf)
        {
            if (SignalObjects != null)
            {
                foreach (Signal thisSignal in SignalObjects)
                {
                    if (thisSignal != null)
                    {
                        outf.Write(thisSignal.Index);
                        thisSignal.Save(outf);
                    }
                }
            }
            outf.Write(-1);

            outf.Write(TrackCircuitList.Count);
            foreach (TrackCircuitSection thisSection in TrackCircuitList)
            {
                thisSection.Save(outf);
            }

            outf.Write(UseLocationPassingPaths);

            outf.Write(DeadlockInfoList.Count);
            foreach (KeyValuePair<int, DeadlockInfo> deadlockDetails in DeadlockInfoList)
            {
                outf.Write(deadlockDetails.Key);
                deadlockDetails.Value.Save(outf);
            }

            outf.Write(deadlockIndex);

            outf.Write(DeadlockReference.Count);
            foreach (KeyValuePair<int, int> referenceDetails in DeadlockReference)
            {
                outf.Write(referenceDetails.Key);
                outf.Write(referenceDetails.Value);
            }

        }

        //================================================================================================//
        /// <summary>
        /// Read all world files to get signal flags
        /// </summary>

        private void BuildSignalWorld(Simulator simulator, SignalConfigurationFile sigcfg, CancellationToken cancellation)
        {

            // get all filesnames in World directory

            var WFilePath = simulator.RoutePath + @"\WORLD\";

            var Tokens = new List<TokenID>();
            Tokens.Add(TokenID.Signal);
            Tokens.Add(TokenID.Platform);

            // loop through files, use only extention .w, skip w+1000000+1000000.w file

            foreach (var fileName in Directory.GetFiles(WFilePath, "*.w"))
            {
                if (cancellation.IsCancellationRequested) return; // ping loader watchdog
                // validate file name a little bit

                if (Path.GetFileName(fileName).Length != 17)
                    continue;

                // read w-file, get SignalObjects only

                Trace.Write("W");
                WorldFile WFile;
                try
                {
                    WFile = new WorldFile(fileName, Tokens);
                }
                catch (FileLoadException error)
                {
                    Trace.WriteLine(error);
                    continue;
                }

                // loop through all signals

                foreach (var worldObject in WFile.Objects)
                {
                    if (worldObject is SignalObject signalObject)
                    {
                        if (signalObject.SignalUnits == null) continue; //this has no unit, will ignore it and treat it as static in scenary.cs

                        //check if signalheads are on same or adjacent tile as signal itself - otherwise there is an invalid match
                        uint? BadSignal = null;
                        foreach (var si in signalObject.SignalUnits)
                        {
                            if (this.trackDB.TrackItems == null || si.TrackItem >= this.trackDB.TrackItems.Count())
                            {
                                BadSignal = si.TrackItem;
                                break;
                            }
                            var item = this.trackDB.TrackItems[si.TrackItem];
                            if (Math.Abs(item.Location.TileX - worldObject.WorldPosition.TileX) > 1 || Math.Abs(item.Location.TileZ - worldObject.WorldPosition.TileZ) > 1)
                            {
                                BadSignal = si.TrackItem;
                                break;
                            }
                        }
                        if (BadSignal.HasValue)
                        {
                            Trace.TraceWarning("Signal referenced in .w file {0} {1} as TrItem {2} not present in .tdb file ", worldObject.WorldPosition.TileX, worldObject.WorldPosition.TileZ, BadSignal.Value);
                            continue;
                        }

                        // if valid, add signal

                        SignalWorldInfo signalWorldInfo = new SignalWorldInfo(signalObject, sigcfg);
                        SignalWorldList.Add(signalWorldInfo);
                        foreach (KeyValuePair<uint, uint> thisref in signalWorldInfo.HeadReference)
                        {
                            var thisSignalCount = SignalWorldList.Count - 1;    // Index starts at 0
                            var thisRefObject = new SignalReferenceInfo(thisSignalCount, thisref.Value);
                            if (!SignalRefList.ContainsKey(thisref.Key))
                            {
                                SignalRefList.Add(thisref.Key, thisRefObject);
                            }
                        }
                    }
                    else if (worldObject is PlatformObject platformObject)
                    {
                        if (!PlatformSidesList.ContainsKey(platformObject.TrackItemIds.TrackDbItems[0]))
                            PlatformSidesList.Add(platformObject.TrackItemIds.TrackDbItems[0], platformObject.PlatformData);
                        if (!PlatformSidesList.ContainsKey(platformObject.TrackItemIds.TrackDbItems[1])) //this was [0] but presumably wrong
                            PlatformSidesList.Add(platformObject.TrackItemIds.TrackDbItems[1], platformObject.PlatformData);
                    }
                }
            }

#if DEBUG_PRINT
			var srlb = new StringBuilder();
            foreach (var thisref in SignalRefList)
            {
                var TBDRef = thisref.Key;
				var signalRef = thisref.Value;
                var reffedObject = SignalWorldList[(int)signalRef.SignalWorldIndex];
                uint headref;
                if (!reffedObject.HeadReference.TryGetValue(TBDRef, out headref))
                {
                    srlb.AppendFormat("Incorrect Ref : {0}\n", TBDRef);
                    foreach (var headindex in reffedObject.HeadReference)
                    {
						srlb.AppendFormat("TDB : {0} + {1}\n", headindex.Key, headindex.Value);
                    }
                }
            }
			File.AppendAllText(@"WorldSignalList.txt", srlb.ToString());
#endif

        }  //BuildSignalWorld


        //================================================================================================//
        /// <summary>
        /// Update : perform signal updates
        /// </summary>

        public void Update(bool preUpdate)
        {
            if (MPManager.IsClient()) return; //in MP, client will not update

            if (foundSignals > 0)
            {

                // loop through all signals
                // update required part
                // in preupdate, process all

                int totalSignal = SignalObjects.Count - 1;

                int updatestep = (totalSignal / 20) + 1;
                if (preUpdate)
                {
                    updatestep = totalSignal;
                }

                for (int icount = updatecount; icount < Math.Min(totalSignal, updatecount + updatestep); icount++)
                {
                    Signal signal = SignalObjects[icount];
                    if (signal != null && !signal.Static) // to cater for orphans, and skip signals which do not require updates
                    {
                        signal.Update();
                    }
                }

                updatecount += updatestep;
                updatecount = updatecount > totalSignal ? 0 : updatecount;
            }
        }  //Update

        //================================================================================================//
        /// <summary></summary>
        /// Build signal list from TDB
        /// </summary>

        private void BuildSignalList(TrackItem[] TrItems, TrackNode[] trackNodes, TrackSectionsFile tsectiondat,
                TrackDatabaseFile tdbfile, Dictionary<int, int> platformList, List<Milepost> milepostList)
        {

            //  Determaine the number of signals in the track Objects list

            if (TrItems == null)
                return;                // No track Objects in route.
            int signalCount = TrItems.Where(item => item is SignalItem || (item is SpeedPostItem speedPost && speedPost.IsLimit)).Count();

            // set general items and create sections
            if (signalCount > 0)
            {
                SignalObjects = new Signal[signalCount];
            }

            Signal.Initialize(this, trackNodes, TrItems);

            for (int i = 1; i < trackNodes.Length; i++)
            {
                ScanSection(TrItems, trackNodes, i, tsectiondat, tdbfile, platformList, milepostList);
            }

            //  Only continue if one or more signals in route.

            if (signalCount > 0)
            {
                // using world cross-reference list, merge heads to single signal

                MergeHeads();

                // rebuild list - clear out null elements

                int firstfree = -1;
                for (int iSignal = 0; iSignal < SignalObjects.Count; iSignal++)
                {
                    if (SignalObjects[iSignal] == null && firstfree < 0)
                    {
                        firstfree = iSignal;
                    }
                    else if (SignalObjects[iSignal] != null && firstfree >= 0)
                    {
                        SignalObjects[firstfree] = SignalObjects[iSignal];
                        SignalObjects[iSignal] = null;
                        firstfree++;
                    }
                }

                if (firstfree < 0)
                    firstfree = SignalObjects.Count - 1;
                // restore all links and indices

                for (var iSignal = 0; iSignal < SignalObjects.Count; iSignal++)
                {
                    if (SignalObjects[iSignal] != null)
                    {
                        var thisObject = SignalObjects[iSignal];
                        thisObject.ResetIndex(iSignal);

                        foreach (var thisHead in thisObject.SignalHeads)
                        {
                            thisHead.ResetMain(thisObject);
                            var trackItem = TrItems[thisHead.TDBIndex];
                            var sigItem = trackItem as SignalItem;
                            var speedItem = trackItem as SpeedPostItem;
                            if (sigItem != null)
                            {
                                sigItem.SignalObject = thisObject.Index;
                            }
                            else if (speedItem != null)
                            {
                                speedItem.SignalObject = thisObject.Index;
                            }
                        }
                    }
                }

                foundSignals = firstfree;
                //SignalObjects = SignalObjects.Where(item => item != null).ToArray();

            }
            else
            {
                SignalObjects = Array.Empty<Signal>();
            }

        } //BuildSignalList


        //================================================================================================//
        /// <summary>
        /// Split backfacing signals
        /// </summary>

        private void SplitBackfacing(TrackItem[] TrItems, TrackNode[] TrackNodes)
        {

            List<Signal> newSignals = new List<Signal>();
            int newindex = foundSignals; //the last was placed into foundSignals-1, thus the new ones need to start from foundSignals

            //
            // Loop through all signals to check on Backfacing heads
            //

            for (int isignal = 0; isignal < SignalObjects.Count - 1; isignal++)
            {
                Signal singleSignal = SignalObjects[isignal];
                if (singleSignal != null && singleSignal.IsSignal && singleSignal.WorldObject?.Backfacing.Count > 0)
                {

                    //
                    // create new signal - copy of existing signal
                    // use Backfacing flags and reset head indication
                    //

                    Signal newSignal = new Signal(newindex, singleSignal);

                    newSignal.TrackItemRefIndex = 0;
                        
                    newSignal.WorldObject.UpdateFlags(singleSignal.WorldObject.FlagsSetBackfacing);
                    newSignal.WorldObject.HeadsSet.SetAll(false);

                    //
                    // loop through the list with headreferences, check this agains the list with backfacing heads
                    // use the TDBreference to find the actual head
                    //

                    List<int> removeHead = new List<int>();  // list to keep trace of heads which are moved //

                    foreach (KeyValuePair<uint, uint> thisHeadRef in singleSignal.WorldObject.HeadReference)
                    {
                        for (int iindex = singleSignal.WorldObject.Backfacing.Count - 1; iindex >= 0; iindex--)
                        {
                            int ihead = singleSignal.WorldObject.Backfacing[iindex];
                            if (thisHeadRef.Value == ihead)
                            {
                                for (int ihIndex = 0; ihIndex < singleSignal.SignalHeads.Count; ihIndex++)
                                {
                                    SignalHead thisHead = singleSignal.SignalHeads[ihIndex];

                                    //
                                    // backfacing head found - add to new signal, set to remove from exising signal
                                    //

                                    if (thisHead.TDBIndex == thisHeadRef.Key)
                                    {
                                        removeHead.Add(ihIndex);

                                        thisHead.ResetMain(newSignal);
                                        newSignal.SignalHeads.Add(thisHead);
                                    }
                                }
                            }

                            //
                            // update flags for available heads
                            //

                            newSignal.WorldObject.HeadsSet[ihead] = true;
                            singleSignal.WorldObject.HeadsSet[ihead] = false;
                        }
                    }

                    //
                    // check if there were actually any backfacing signal heads
                    //

                    if (removeHead.Count > 0)
                    {

                        //
                        // remove moved heads from existing signal
                        //

                        for (int ihead = singleSignal.SignalHeads.Count - 1; ihead >= 0; ihead--)
                        {
                            if (removeHead.Contains(ihead))
                            {
                                singleSignal.SignalHeads.RemoveAt(ihead);
                            }
                        }

                        //
                        // Check direction of heads to set correct direction for signal
                        //

                        if (singleSignal.SignalHeads.Count > 0)
                        {
                            SignalItem thisItemOld = TrItems[singleSignal.SignalHeads[0].TDBIndex] as SignalItem;
                            if (singleSignal.Direction != (TrackDirection)thisItemOld.Direction)
                            {
                                singleSignal.Direction = (TrackDirection)thisItemOld.Direction;
                                singleSignal.TdbTraveller.ReverseDirection();                           // reverse //
                            }
                        }

                        SignalItem thisItemNew = TrItems[newSignal.SignalHeads[0].TDBIndex] as SignalItem;
                        if (newSignal.Direction != (TrackDirection)thisItemNew.Direction)
                        {
                            newSignal.Direction = (TrackDirection)thisItemNew.Direction;
                            newSignal.TdbTraveller.ReverseDirection();                           // reverse //
                        }

                        //
                        // set correct trRefIndex for this signal, and set cross-reference for all backfacing trRef items
                        //

                        TrackVectorNode tvn = TrackNodes[newSignal.TrackNode] as TrackVectorNode;
                        for (int i = 0; i < tvn.TrackItemIndices.Length; i++)
                        {
                            int TDBRef = tvn.TrackItemIndices[i];
                            if (TrItems[TDBRef] != null)
                            {
                                if (TrItems[TDBRef] is SignalItem)
                                {
                                    foreach (SignalHead thisHead in newSignal.SignalHeads)
                                    {
                                        if (TDBRef == thisHead.TDBIndex)
                                        {
                                            SignalItem sigItem = (SignalItem)TrItems[TDBRef];
                                            sigItem.SignalObject = newSignal.Index;
                                            newSignal.TrackItemRefIndex = i;

                                            // remove this key from the original signal //

                                            singleSignal.WorldObject.HeadReference.Remove((uint)TDBRef);
                                        }
                                    }
                                }
                            }
                        }

                        //
                        // reset cross-references for original signal (it may have been set for a backfacing head)
                        //
                        tvn = TrackNodes[newSignal.TrackNode] as TrackVectorNode;
                        for (int i = 0; i < tvn.TrackItemIndices.Length; i++)
                        {
                            int TDBRef = tvn.TrackItemIndices[i];
                            if (TrItems[TDBRef] != null)
                            {
                                if (TrItems[TDBRef] is SignalItem)
                                {
                                    foreach (SignalHead thisHead in singleSignal.SignalHeads)
                                    {
                                        if (TDBRef == thisHead.TDBIndex)
                                        {
                                            SignalItem sigItem = (SignalItem)TrItems[TDBRef];
                                            sigItem.SignalObject = singleSignal.Index;
                                            singleSignal.TrackItemRefIndex = i;

                                            // remove this key from the new signal //

                                            newSignal.WorldObject.HeadReference.Remove((uint)TDBRef);
                                        }
                                    }
                                }
                            }
                        }

                        //
                        // add new signal to signal list
                        //

                        newindex++;
                        newSignals.Add(newSignal);

                        //
                        // revert existing signal to NULL if no heads remain
                        //

                        if (singleSignal.SignalHeads.Count <= 0)
                        {
                            SignalObjects[isignal] = null;
                        }
                    }
                }
            }

            //
            // add all new signals to the signalObject array
            // length of array was set to all possible signals, so there will be space to spare
            //

            newindex = foundSignals;
            foreach (Signal newSignal in newSignals)
            {
                SignalObjects[newindex] = newSignal;
                newindex++;
            }

            foundSignals = newindex;
        }

        //================================================================================================//
        /// <summary>
        /// ScanSection : This method checks a section in the TDB for signals or speedposts
        /// </summary>

        private void ScanSection(TrackItem[] TrItems, TrackNode[] trackNodes, int index,
                               TrackSectionsFile tsectiondat, TrackDatabaseFile tdbfile, Dictionary<int, int> platformList, List<Milepost> milepostList)
        {
            int lastSignal = -1;                // Index to last signal found in path; -1 if none
            int lastMilepost = -1;                // Index to last milepost found in path; -1 if none

            if (trackNodes[index] is TrackEndNode)
                return;

            //  Is it a vector node then it may contain objects.
            if (trackNodes[index] is TrackVectorNode tvn)
            {
                // Any objects ?
                for (int i = 0; i < tvn.TrackItemIndices.Length; i++)
                {
                    if (TrItems[tvn.TrackItemIndices[i]] != null)
                    {
                        int TDBRef = tvn.TrackItemIndices[i];

                        // Track Item is signal
                        if (TrItems[TDBRef] is SignalItem)
                        {
                            SignalItem sigItem = (SignalItem)TrItems[TDBRef];
                            sigItem.SignalObject = foundSignals;

                            bool validSignal = true;
                            lastSignal = AddSignal(index, i, sigItem, TDBRef, tsectiondat, tdbfile, ref validSignal);

                            if (validSignal)
                            {
                                sigItem.SignalObject = lastSignal;
                            }
                            else
                            {
                                sigItem.SignalObject = -1;
                            }
                        }

                        // Track Item is speedpost - check if really limit
                        else if (TrItems[TDBRef] is SpeedPostItem)
                        {
                            SpeedPostItem speedItem = (SpeedPostItem)TrItems[TDBRef];
                            if (speedItem.IsLimit)
                            {
                                speedItem.SignalObject = foundSignals;

                                lastSignal = AddSpeed(index, i, speedItem, TDBRef, tsectiondat, tdbfile);
                                speedItem.SignalObject = lastSignal;

                            }
                            else if (speedItem.IsMilePost)
                            {
                                speedItem.SignalObject = foundMileposts;
                                lastMilepost = AddMilepost(index, i, speedItem, TDBRef, tsectiondat, tdbfile);
                                speedItem.SignalObject = lastMilepost;
                            }
                        }
                        else if (TrItems[TDBRef] is PlatformItem)
                        {
                            if (platformList.ContainsKey(TDBRef))
                            {
                                Trace.TraceInformation("Double reference to platform ID {0} in nodes {1} and {2}\n", TDBRef, platformList[TDBRef], index);
                            }
                            else
                            {
                                platformList.Add(TDBRef, index);
                            }
                        }
                        else if (TrItems[TDBRef] is SidingItem)
                        {
                            if (platformList.ContainsKey(TDBRef))
                            {
                                Trace.TraceInformation("Double reference to siding ID {0} in nodes {1} and {2}\n", TDBRef, platformList[TDBRef], index);
                            }
                            else
                            {
                                platformList.Add(TDBRef, index);
                            }
                        }
                    }
                }
            }
        }   //ScanSection 

        //================================================================================================//
        /// <summary>
        /// Merge Heads
        /// </summary>

        public void MergeHeads()
        {
            //            foreach (SignalWorldObject thisWorldObject in SignalWorldList)
            //            {
            for (int iWorldIndex = 0; iWorldIndex < SignalWorldList.Count; iWorldIndex++)
            {
                SignalWorldInfo thisWorldObject = SignalWorldList[iWorldIndex];
                Signal MainSignal = null;

                if (thisWorldObject.HeadReference.Count > 1)
                {

                    foreach (KeyValuePair<uint, uint> thisReference in thisWorldObject.HeadReference)
                    {
                        if (SignalHeadList.ContainsKey(thisReference.Key))
                        {
                            if (MainSignal == null)
                            {
                                MainSignal = SignalHeadList[thisReference.Key];
                            }
                            else
                            {
                                Signal AddSignal = SignalHeadList[thisReference.Key];
                                if (MainSignal.TrackNode != AddSignal.TrackNode)
                                {
                                    Trace.TraceWarning("Signal head {0} in different track node than signal head {1} of same signal", MainSignal.trItem, thisReference.Key);
                                    MainSignal = null;
                                    break;
                                }
                                foreach (SignalHead thisHead in AddSignal.SignalHeads)
                                {
                                    MainSignal.SignalHeads.Add(thisHead);
                                    SignalObjects[AddSignal.Index] = null;
                                }
                            }
                        }
                        else
                        {
                            Trace.TraceInformation("Signal found in Worldfile but not in TDB - TDB Index : {0}", thisReference.Key);
                            MainSignal = null;
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// This method adds a new Signal to the list
        /// </summary>

        private int AddSignal(int trackNode, int nodeIndx, SignalItem sigItem, int TDBRef, TrackSectionsFile tsectiondat, TrackDatabaseFile tdbfile, ref bool validSignal)
        {
            validSignal = true;
            Traveller traveller = null;

            if (!(tdbfile.TrackDB.TrackNodes[trackNode] is TrackVectorNode tvn))
            {
                validSignal = false;
                Trace.TraceInformation("Reference to invalid track node {0} for Signal {1}\n", trackNode, TDBRef);
            }
            else
            {
                traveller = new Traveller(tsectiondat, tdbfile.TrackDB.TrackNodes, tvn, sigItem.Location, (Traveller.TravellerDirection)(1 - sigItem.Direction));
            }

            SignalObjects[foundSignals] = new Signal(foundSignals, traveller);
            SignalObjects[foundSignals].IsSignal = true;
            SignalObjects[foundSignals].IsSpeedSignal = false;
            SignalObjects[foundSignals].Direction = sigItem.Direction;
            SignalObjects[foundSignals].TrackNode = trackNode;
            SignalObjects[foundSignals].TrackItemRefIndex = nodeIndx;
            SignalObjects[foundSignals].AddHead(nodeIndx, TDBRef, sigItem);

            SignalObjects[foundSignals].WorldObject = null;

            if (SignalHeadList.ContainsKey((uint)TDBRef))
            {
                validSignal = false;
                Trace.TraceInformation("Invalid double TDBRef {0} in node {1}\n", TDBRef, trackNode);
            }

            if (!validSignal)
            {
                SignalObjects[foundSignals] = null;  // reset signal, do not increase signal count
            }
            else
            {
                SignalHeadList.Add((uint)TDBRef, SignalObjects[foundSignals]);
                foundSignals++;
            }

            return foundSignals - 1;
        } // AddSignal


        //================================================================================================//
        /// <summary>
        /// This method adds a new Speedpost to the list
        /// </summary>

        private int AddSpeed(int trackNode, int nodeIndx, SpeedPostItem speedItem, int TDBRef, TrackSectionsFile tsectiondat, TrackDatabaseFile tdbfile)
        {
            Traveller traveller = new Traveller(tsectiondat, tdbfile.TrackDB.TrackNodes, tdbfile.TrackDB.TrackNodes[trackNode] as TrackVectorNode, speedItem.Location, Traveller.TravellerDirection.Backward);

            SignalObjects[foundSignals] = new Signal(foundSignals, traveller);
            SignalObjects[foundSignals].IsSignal = false;
            SignalObjects[foundSignals].IsSpeedSignal = false;
            SignalObjects[foundSignals].Direction = 0;                  // preset - direction not yet known //
            SignalObjects[foundSignals].TrackNode = trackNode;
            SignalObjects[foundSignals].TrackItemRefIndex = nodeIndx;
            SignalObjects[foundSignals].AddHead(nodeIndx, TDBRef, speedItem);

            double delta_angle = SignalObjects[foundSignals].TdbTraveller.RotY - ((Math.PI / 2) - speedItem.Angle);
            float delta_float = MathHelper.WrapAngle((float)delta_angle);
            if (Math.Abs(delta_float) < (Math.PI / 2))
            {
                SignalObjects[foundSignals].Direction = ((TrackDirection)(int)SignalObjects[foundSignals].TdbTraveller.Direction).Next();
            }
            else
            {
                SignalObjects[foundSignals].Direction = (TrackDirection)(int)SignalObjects[foundSignals].TdbTraveller.Direction;
                SignalObjects[foundSignals].TdbTraveller.ReverseDirection();
            }

#if DEBUG_PRINT
            File.AppendAllText(@"C:\temp\speedpost.txt",
				String.Format("\nPlaced : at : {0} {1}:{2} {3}; angle - track : {4}:{5}; delta : {6}; dir : {7}\n",
				speedItem.TileX, speedItem.TileZ, speedItem.X, speedItem.Z,
				speedItem.Angle, signalObjects[foundSignals].tdbtraveller.RotY,
				delta_angle,
				signalObjects[foundSignals].direction));
#endif

            SignalObjects[foundSignals].WorldObject = null;
            foundSignals++;
            return foundSignals - 1;
        } // AddSpeed

        //================================================================================================//
        /// <summary>
        /// This method adds a new Milepost to the list
        /// </summary>

        private int AddMilepost(int trackNode, int nodeIndx, SpeedPostItem speedItem, int TDBRef, TrackSectionsFile tsectiondat, TrackDatabaseFile tdbfile)
        {
            Milepost milepost = new Milepost((uint)TDBRef, speedItem.Distance);
            MilepostList.Add(milepost);

#if DEBUG_PRINT
            File.AppendAllText(@"C:\temp\speedpost.txt",
				String.Format("\nMilepost placed : at : {0} {1}:{2} {3}. String: {4}\n",
				speedItem.TileX, speedItem.TileZ, speedItem.X, speedItem.Z, speedItem.SpeedInd));
#endif

            foundMileposts = MilepostList.Count;
            return foundMileposts - 1;
        } // AddMilepost

        //================================================================================================//
        /// <summary>
        /// Add the sigcfg reference to each signal object.
        /// </summary>
        private void AddCFG(SignalConfigurationFile signalConfig)
        {
            foreach (Signal signal in SignalObjects)
            {
                if (null != signal && signal.IsSignal)
                {
                    signal.SetSignalType(signalConfig);
                }
            }
        }//AddCFG

        //================================================================================================//
        /// <summary>
        /// Add info from signal world objects to signal
        /// </summary>

        private void AddWorldInfo()
        {

            // loop through all signal and all heads

            foreach (Signal signal in SignalObjects)
            {
                if (signal != null)
                {
                    foreach (SignalHead head in signal.SignalHeads)
                    {

                        // get reference using TDB index from head

                        uint TDBRef = Convert.ToUInt32(head.TDBIndex);
                        SignalReferenceInfo thisRef;

                        if (SignalRefList.TryGetValue(TDBRef, out thisRef))
                        {
                            uint signalIndex = thisRef.SignalWorldIndex;
                            if (signal.WorldObject == null)
                            {
                                signal.WorldObject = SignalWorldList[(int)signalIndex];
                            }
                            SignalRefList.Remove(TDBRef);
                        }
                    }
                }
            }

        }//AddWorldInfo

        //================================================================================================//
        /// <summary>
        /// FindByTrItem : find required signalObj + signalHead
        /// </summary>

        public KeyValuePair<Signal, SignalHead>? FindByTrItem(uint trItem)
        {
            foreach (var signal in SignalObjects)
                if (signal != null)
                    foreach (var head in signal.SignalHeads)
                        if ((trackDB.TrackNodes[signal.TrackNode] as TrackVectorNode).TrackItemIndices[head.TrackItemIndex] == (int)trItem)
                            return new KeyValuePair<Signal, SignalHead>(signal, head);
            return null;
        }//FindByTrItem

        //================================================================================================//
        /// <summary>
        /// Count number of normal signal heads
        /// </summary>

        public void SetNumSignalHeads()
        {
            foreach (Signal signal in SignalObjects)
            {
                signal?.SetNumberSignalHeads();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Find_Next_Object_InRoute : find next item along path of train - using Route List (only forward)
        /// Objects to search for : SpeedPost, Signal
        ///
        /// Usage :
        ///   always set : RouteList, RouteNodeIndex, distance along RouteNode, fnType
        ///
        ///   from train :
        ///     optional : maxdistance
        ///
        /// returned :
        ///   >= 0 : signal object reference
        ///   -1  : end of track 
        ///   -3  : no item within required distance
        ///   -5  : end of authority
        ///   -6  : end of (sub)route
        /// </summary>

        public TrackCircuitSignalItem Find_Next_Object_InRoute(Train.TCSubpathRoute routePath,
                int routeIndex, float routePosition, float maxDistance, SignalFunction fn_type, Train.TrainRouted thisTrain)
        {

            SignalItemFindState locstate = SignalItemFindState.None;
            // local processing state     //

            int actRouteIndex = routeIndex;      // present node               //
            Train.TCRouteElement thisElement = routePath[actRouteIndex];
            int actSection = thisElement.TCSectionIndex;
            TrackDirection actDirection = (TrackDirection)thisElement.Direction;
            TrackCircuitSection thisSection = TrackCircuitList[actSection];
            float totalLength = 0;
            float lengthOffset = routePosition;

            Signal foundObject = null;
            TrackCircuitSignalItem thisItem = null;

            //
            // loop through trackcircuits until :
            //  - end of track or route is found
            //  - end of authorization is found
            //  - required item is found
            //  - max distance is covered
            //

            while (locstate == SignalItemFindState.None)
            {

                // normal signal
                if (fn_type == SignalFunction.Normal)
                {
                    if (thisSection.EndSignals[actDirection] != null)
                    {
                        foundObject = thisSection.EndSignals[actDirection];
                        totalLength += (thisSection.Length - lengthOffset);
                        locstate = SignalItemFindState.Item;
                    }
                }

                // speedpost
                else if (fn_type == SignalFunction.Speed)
                {
                    TrackCircuitSignalList speedpostList = thisSection.CircuitItems.TrackCircuitSpeedPosts[actDirection];
                    locstate = SignalItemFindState.None;

                    for (int iPost = 0;
                             iPost < speedpostList.Count &&
                                     locstate == SignalItemFindState.None;
                             iPost++)
                    {
                        TrackCircuitSignalItem thisSpeedpost = speedpostList[iPost];
                        if (thisSpeedpost.SignalLocation > lengthOffset)
                        {
                            SpeedInfo thisSpeed = thisSpeedpost.Signal.this_sig_speed(SignalFunction.Speed);

                            // set signal in list if there is no train or if signal has active speed
                            if (thisTrain == null || (thisSpeed != null && (thisSpeed.Flag || thisSpeed.Reset ||
                                (thisTrain.Train.IsFreight && thisSpeed.FreightSpeed != -1) || (!thisTrain.Train.IsFreight && thisSpeed.PassengerSpeed != -1))))
                            {
                                locstate = SignalItemFindState.Item;
                                foundObject = thisSpeedpost.Signal;
                                totalLength += (thisSpeedpost.SignalLocation - lengthOffset);
                            }

                            // also set signal in list if it is a speed signal as state of speed signal may change
                            else if (thisSpeedpost.Signal.IsSpeedSignal)
                            {
                                locstate = SignalItemFindState.Item;
                                foundObject = thisSpeedpost.Signal;
                                totalLength += (thisSpeedpost.SignalLocation - lengthOffset);
                            }
                        }
                    }
                }
                // other fn_types
                else
                {
                    TrackCircuitSignalList signalList = thisSection.CircuitItems.TrackCircuitSignals[actDirection][(int)fn_type];
                    locstate = SignalItemFindState.None;

                    foreach (TrackCircuitSignalItem thisSignal in signalList)
                    {
                        if (thisSignal.SignalLocation > lengthOffset)
                        {
                            locstate = SignalItemFindState.Item;
                            foundObject = thisSignal.Signal;
                            totalLength += (thisSignal.SignalLocation - lengthOffset);
                            break;
                        }
                    }
                }

                // next section accessed via next route element

                if (locstate == SignalItemFindState.None)
                {
                    totalLength += (thisSection.Length - lengthOffset);
                    lengthOffset = 0;

                    int setSection = thisSection.ActivePins[(TrackDirection)thisElement.OutPin[0], (Location)thisElement.OutPin[1]].Link;
                    actRouteIndex++;

                    if (setSection < 0)
                    {
                        locstate = SignalItemFindState.EndOfAuthority;
                    }
                    else if (actRouteIndex >= routePath.Count)
                    {
                        locstate = SignalItemFindState.EndOfPath;
                    }
                    else if (maxDistance > 0 && totalLength > maxDistance)
                    {
                        locstate = SignalItemFindState.PassedMaximumDistance;
                    }
                    else
                    {
                        thisElement = routePath[actRouteIndex];
                        actSection = thisElement.TCSectionIndex;
                        actDirection = (TrackDirection)thisElement.Direction;
                        thisSection = TrackCircuitList[actSection];
                    }
                }
            }

            if (foundObject != null)
            {
                thisItem = new TrackCircuitSignalItem(foundObject, totalLength);
            }
            else
            {
                thisItem = new TrackCircuitSignalItem(locstate);
            }

            return (thisItem);
        }

        //================================================================================================//
        /// <summary>
        /// GetNextObject_InRoute : find next item along path of train - using Route List (only forward)
        ///
        /// Usage :
        ///   always set : Train (may be null), RouteList, RouteNodeIndex, distance along RouteNode, fn_type
        ///
        ///   from train :
        ///     optional : maxdistance
        ///
        /// returned :
        ///   >= 0 : signal object reference
        ///   -1  : end of track 
        ///   -2  : passed signal at danger
        ///   -3  : no item within required distance
        ///   -5  : end of authority
        ///   -6  : end of (sub)route
        /// </summary>


        // call without position
        public SignalItemInfo GetNextObject_InRoute(Train.TrainRouted thisTrain, Train.TCSubpathRoute routePath,
                    int routeIndex, float routePosition, float maxDistance, SignalItemType req_type)
        {

            Train.TCPosition thisPosition = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex];

            return (GetNextObject_InRoute(thisTrain, routePath, routeIndex, routePosition, maxDistance, req_type, thisPosition));
        }

        // call with position
        public SignalItemInfo GetNextObject_InRoute(Train.TrainRouted thisTrain, Train.TCSubpathRoute routePath,
                    int routeIndex, float routePosition, float maxDistance, SignalItemType req_type,
                    Train.TCPosition thisPosition)
        {

            TrackCircuitSignalItem foundItem = null;

            bool findSignal = false;
            bool findSpeedpost = false;

            float signalDistance = -1f;
            float speedpostDistance = -1f;

            if (req_type == SignalItemType.Any ||
                req_type == SignalItemType.Signal)
            {
                findSignal = true;
            }

            if (req_type == SignalItemType.Any ||
                req_type == SignalItemType.SpeedLimit)
            {
                findSpeedpost = true;
            }

            Train.TCSubpathRoute usedRoute = routePath;

            // if routeIndex is not valid, build temp route from present position to first node or signal

            if (routeIndex < 0)
            {
                bool thisIsFreight = thisTrain != null ? thisTrain.Train.IsFreight : false;

                List<int> tempSections = ScanRoute(thisTrain.Train, thisPosition.TCSectionIndex,
                    thisPosition.TCOffset, (TrackDirection)thisPosition.TCDirection,
                    true, 200f, false, true, true, false, true, false, false, true, false, thisIsFreight);


                Train.TCSubpathRoute tempRoute = new Train.TCSubpathRoute();
                int prevSection = -2;

                foreach (int sectionIndex in tempSections)
                {
                    Train.TCRouteElement thisElement =
                        new Train.TCRouteElement(TrackCircuitList[Math.Abs(sectionIndex)],
                            sectionIndex > 0 ? 0 : 1, this, prevSection);
                    tempRoute.Add(thisElement);
                    prevSection = Math.Abs(sectionIndex);
                }
                usedRoute = tempRoute;
                routeIndex = 0;
            }

            // always find signal to check for signal at danger

            SignalItemFindState signalState = SignalItemFindState.None;

            TrackCircuitSignalItem nextSignal =
                Find_Next_Object_InRoute(usedRoute, routeIndex, routePosition,
                        maxDistance, SignalFunction.Normal, thisTrain);

            signalState = nextSignal.SignalState;
            if (nextSignal.SignalState == SignalItemFindState.Item)
            {
                signalDistance = nextSignal.SignalLocation;
                Signal foundSignal = nextSignal.Signal;
                if (foundSignal.this_sig_lr(SignalFunction.Normal) == SignalAspectState.Stop)
                {
                    signalState = SignalItemFindState.PassedDanger;
                }
                else if (thisTrain != null && foundSignal.EnabledTrain != thisTrain)
                {
                    signalState = SignalItemFindState.PassedDanger;
                    nextSignal.SignalState = signalState;  // do not return OBJECT_FOUND - signal is not valid
                }

            }

            // look for speedpost only if required

            if (findSpeedpost)
            {
                TrackCircuitSignalItem nextSpeedpost =
                    Find_Next_Object_InRoute(usedRoute, routeIndex, routePosition,
                        maxDistance, SignalFunction.Speed, thisTrain);

                if (nextSpeedpost.SignalState == SignalItemFindState.Item)
                {
                    speedpostDistance = nextSpeedpost.SignalLocation;
                    Signal foundSignal = nextSpeedpost.Signal;
                }


                if (signalDistance > 0 && speedpostDistance > 0)
                {
                    if (signalDistance < speedpostDistance)
                    {
                        if (findSignal)
                        {
                            foundItem = nextSignal;
                        }
                        else
                        {
                            foundItem = nextSpeedpost;
                            if (signalState == SignalItemFindState.PassedDanger)
                            {
                                foundItem.SignalState = signalState;
                            }
                        }
                    }
                    else
                    {
                        foundItem = nextSpeedpost;
                    }
                }
                else if (signalDistance > 0)
                {
                    foundItem = nextSignal;
                }
                else if (speedpostDistance > 0)
                {
                    foundItem = nextSpeedpost;
                }
            }
            else if (findSignal)
            {
                foundItem = nextSignal;
            }


            SignalItemInfo returnItem;
            if (foundItem == null)
            {
                returnItem = new SignalItemInfo(SignalItemFindState.None);
            }
            else if (foundItem.SignalState != SignalItemFindState.Item)
            {
                returnItem = new SignalItemInfo(foundItem.SignalState);
            }
            else
            {
                returnItem = new SignalItemInfo(foundItem.Signal, foundItem.SignalLocation);
            }

            return (returnItem);
        }

        //================================================================================================//
        /// <summary>
        /// Gets the Track Monitor Aspect from the MSTS aspect (for the TCS) 
        /// </summary>

        public TrackMonitorSignalAspect TranslateToTCSAspect(SignalAspectState state)
        {
            switch (state)
            {
                case SignalAspectState.Stop:
                    return TrackMonitorSignalAspect.Stop;
                case SignalAspectState.Stop_And_Proceed:
                    return TrackMonitorSignalAspect.StopAndProceed;
                case SignalAspectState.Restricting:
                    return TrackMonitorSignalAspect.Restricted;
                case SignalAspectState.Approach_1:
                    return TrackMonitorSignalAspect.Approach1;
                case SignalAspectState.Approach_2:
                    return TrackMonitorSignalAspect.Approach2;
                case SignalAspectState.Approach_3:
                    return TrackMonitorSignalAspect.Approach3;
                case SignalAspectState.Clear_1:
                    return TrackMonitorSignalAspect.Clear1;
                case SignalAspectState.Clear_2:
                    return TrackMonitorSignalAspect.Clear2;
                default:
                    return TrackMonitorSignalAspect.None;
            }
        } // GetMonitorAspect

        //================================================================================================//
        /// <summary>
        /// Create Track Circuits
        /// <summary>

        private void CreateTrackCircuits(TrackItem[] TrItems, TrackNode[] trackNodes, TrackSectionsFile tsectiondat)
        {

            //
            // Create dummy element as first to keep indexes equal
            //

            TrackCircuitList.Add(new TrackCircuitSection(this));

            //
            // Create new default elements from existing base
            //

            for (int i = 1; i < trackNodes.Length; i++)
            {
                TrackNode trackNode = trackNodes[i];
                TrackCircuitSection defaultSection =
                    new TrackCircuitSection(trackNode, i, tsectiondat);
                TrackCircuitList.Add(defaultSection);
            }

            //
            // loop through original default elements
            // collect track items
            //

            int originalNodes = TrackCircuitList.Count;
            for (int iNode = 1; iNode < originalNodes; iNode++)
            {
                ProcessNodes(iNode, TrItems, trackNodes, tsectiondat);
            }

            // Delete MilepostList as it is no more needed
            MilepostList.Clear();
            foundMileposts = -1;
            MilepostList = null;

            //
            // loop through original default elements
            // split on crossover items
            //

            originalNodes = TrackCircuitList.Count;
            int nextNode = originalNodes;
            foreach (KeyValuePair<int, CrossOverInfo> CrossOver in CrossoverList)
            {
                nextNode = SplitNodesCrossover(CrossOver.Value, tsectiondat, nextNode);
            }

            //
            // loop through original default elements
            // split on normal signals
            //

            originalNodes = TrackCircuitList.Count;
            nextNode = originalNodes;

            for (int iNode = 1; iNode < originalNodes; iNode++)
            {
                nextNode = SplitNodesSignals(iNode, nextNode);
            }

            //
            // loop through all items
            // perform link test
            //

            originalNodes = TrackCircuitList.Count;
            nextNode = originalNodes;
            for (int iNode = 1; iNode < originalNodes; iNode++)
            {
                nextNode = performLinkTest(iNode, nextNode);
            }

            //
            // loop through all items
            // reset active links
            // set fixed active links for none-junction links
            // set trailing junction flags
            //

            originalNodes = TrackCircuitList.Count;
            for (int iNode = 1; iNode < originalNodes; iNode++)
            {
                setActivePins(iNode);
            }

            //
            // Set cross-reference
            //

            for (int iNode = 1; iNode < originalNodes; iNode++)
            {
                setCrossReference(iNode, trackNodes);
            }
            for (int iNode = 1; iNode < originalNodes; iNode++)
            {
                setCrossReferenceCrossOver(iNode, trackNodes);
            }

            //
            // Set cross-reference for signals
            //

            foreach(TrackCircuitSection section in TrackCircuitList)
            {
                Signal.SetSignalCrossReference(section);
            }

            //
            // Set default next signal and fixed route information
            //

            for (int iSignal = 0; SignalObjects != null && iSignal < SignalObjects.Count; iSignal++)
            {
                Signal thisSignal = SignalObjects[iSignal];
                if (thisSignal != null)
                {
                    thisSignal.SetSignalDefaultNextSignal();
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// ProcessNodes
        /// </summary>

        public void ProcessNodes(int iNode, TrackItem[] TrItems, TrackNode[] trackNodes, TrackSectionsFile tsectiondat)
        {

            //
            // Check if original tracknode had trackitems
            //

            TrackCircuitSection thisCircuit = TrackCircuitList[iNode];

            if (trackNodes[thisCircuit.OriginalIndex] is TrackVectorNode tvn && tvn.TrackItemIndices.Length > 0)
            {
                //
                // Create TDBtraveller at start of section to calculate distances
                //

                TrackVectorSection firstSection = tvn.TrackVectorSections[0];
                Traveller TDBTrav = new Traveller(tsectiondat, trackNodes, tvn, firstSection.Location, (Traveller.TravellerDirection)1);

                //
                // Process all items (do not split yet)
                //

                float[] lastDistance = new float[2] { -1.0f, -1.0f };
                for (int iRef = 0; iRef < tvn.TrackItemIndices.Length; iRef++)
                {
                    int TDBRef = tvn.TrackItemIndices[iRef];
                    if (TrItems[TDBRef] != null)
                    {
                        lastDistance = InsertNode(thisCircuit, TrItems[TDBRef], TDBTrav, trackNodes, lastDistance);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// InsertNode
        /// </summary>

        public float[] InsertNode(TrackCircuitSection thisCircuit, TrackItem thisItem,
                        Traveller TDBTrav, TrackNode[] trackNodes, float[] lastDistance)
        {

            float[] newLastDistance = new float[2];
            lastDistance.CopyTo(newLastDistance, 0);

            //
            // Insert signal
            //

            if (thisItem is SignalItem)
            {
                try
                {
                    SignalItem tryItem = (SignalItem)thisItem;
                }
                catch (Exception error)
                {
                    Trace.TraceWarning(error.Message);
                    Trace.TraceWarning("Signal item not consistent with signal database");
                    return newLastDistance;
                }

                SignalItem sigItem = (SignalItem)thisItem;
                if (sigItem.SignalObject >= 0)
                {
                    Signal thisSignal = SignalObjects[sigItem.SignalObject];
                    if (thisSignal == null)
                    {
                        Trace.TraceWarning("Signal item with TrItemID = {0} not consistent with signal database", sigItem.TrackItemId);
                        return newLastDistance;
                    }
                    float signalDistance = thisSignal.DistanceTo(TDBTrav);
                    if (thisSignal.Direction == TrackDirection.Reverse)
                    {
                        signalDistance = thisCircuit.Length - signalDistance;
                    }

                    for (int fntype = 0; fntype < OrtsSignalTypeCount; fntype++)
                    {
                        if (thisSignal.isORTSSignalType(fntype))
                        {
                            TrackCircuitSignalItem thisTCItem =
                                    new TrackCircuitSignalItem(thisSignal, signalDistance);

                            TrackDirection directionList = thisSignal.Direction == 0 ? TrackDirection.Reverse : TrackDirection.Ahead;
                            TrackCircuitSignalList signalList = thisCircuit.CircuitItems.TrackCircuitSignals[directionList][fntype];

                            // if signal is SPEED type, insert in speedpost list
                            if (fntype == (int)SignalFunction.Speed)
                            {
                                signalList = thisCircuit.CircuitItems.TrackCircuitSpeedPosts[directionList];
                            }

                            bool signalset = false;
                            foreach (TrackCircuitSignalItem inItem in signalList)
                            {
                                if (inItem.Signal == thisSignal)
                                {
                                    signalset = true;
                                }
                            }

                            if (!signalset)
                            {
                                if (directionList == 0)
                                {
                                    signalList.Insert(0, thisTCItem);
                                }
                                else
                                {
                                    signalList.Add(thisTCItem);
                                }
                            }
                        }
                    }
                    newLastDistance[(int)thisSignal.Direction] = signalDistance;
                }
            }

            //
            // Insert speedpost
            //

            else if (thisItem is SpeedPostItem)
            {
                SpeedPostItem speedItem = (SpeedPostItem)thisItem;
                if (speedItem.SignalObject >= 0)
                {
                    if (!speedItem.IsMilePost)
                    {
                        Signal thisSpeedpost = SignalObjects[speedItem.SignalObject];
                        float speedpostDistance = thisSpeedpost.DistanceTo(TDBTrav);
                        if (thisSpeedpost.Direction == TrackDirection.Reverse)
                        {
                            speedpostDistance = thisCircuit.Length - speedpostDistance;
                        }

                        if (speedpostDistance == lastDistance[(int)thisSpeedpost.Direction]) // if at same position as last item
                        {
                            speedpostDistance = speedpostDistance + 0.001f;  // shift 1 mm so it will be found
                        }

                        TrackCircuitSignalItem thisTCItem =
                                new TrackCircuitSignalItem(thisSpeedpost, speedpostDistance);

                        TrackDirection directionList = thisSpeedpost.Direction == 0 ? TrackDirection.Reverse : TrackDirection.Ahead;
                        TrackCircuitSignalList thisSignalList = thisCircuit.CircuitItems.TrackCircuitSpeedPosts[directionList];

                        if (directionList == 0)
                        {
                            thisSignalList.Insert(0, thisTCItem);
                        }
                        else
                        {
                            thisSignalList.Add(thisTCItem);
                        }

                        newLastDistance[(int)thisSpeedpost.Direction] = speedpostDistance;
                    }

                    // Milepost
                    else if (speedItem.IsMilePost)
                    {
                        Milepost thisMilepost = MilepostList[speedItem.SignalObject];
                        TrackItem milepostTrItem = Simulator.TDB.TrackDB.TrackItems[thisMilepost.TrackItemId];
                        float milepostDistance = TDBTrav.DistanceTo(milepostTrItem.Location);

                        TrackCircuitMilepost thisTCItem =
                                new TrackCircuitMilepost(thisMilepost, milepostDistance, thisCircuit.Length - milepostDistance);

                        List<TrackCircuitMilepost> thisMilepostList =
                                thisCircuit.CircuitItems.TrackCircuitMileposts;
                        thisMilepostList.Add(thisTCItem);
                    }
                }
            }

            //
            // Insert crossover in special crossover list
            //

            else if (thisItem is CrossoverItem crossOver)
            {
                float cdist = TDBTrav.DistanceTo(trackNodes[thisCircuit.OriginalIndex], crossOver.Location);

                int thisId = (int)crossOver.TrackItemId;
                int crossId = (int)crossOver.TrackNode;
                CrossOverInfo exItem = null;

                // search in Dictionary for combined item //

                if (CrossoverList.ContainsKey(crossId))
                {
                    CrossoverList[crossId].Update(cdist, thisCircuit.Index);
                }
                else
                {
                    exItem = new CrossOverInfo(cdist, 0f, thisCircuit.Index, -1, thisId, crossId, crossOver.ShapeId);

                    CrossoverList.Add(thisId, exItem);
                }
            }

            return (newLastDistance);
        }

        //================================================================================================//
        /// <summary>
        /// Split on Signals
        /// </summary>

        private int SplitNodesSignals(int thisNode, int nextNode)
        {
            int thisIndex = thisNode;
            int newIndex = -1;
            List<int> addIndex = new List<int>();

            //
            // in direction 0, check original item only
            // keep list of added items
            //

            TrackCircuitSection thisSection = TrackCircuitList[thisIndex];

            newIndex = -1;
            if (thisSection.CircuitType == TrackCircuitType.Normal)
            {
                addIndex.Add(thisNode);

                List<TrackCircuitSignalItem> sectionSignals =
                         thisSection.CircuitItems.TrackCircuitSignals[0][(int)SignalFunction.Normal];

                while (sectionSignals.Count > 0)
                {
                    TrackCircuitSignalItem thisSignal = sectionSignals[0];
                    sectionSignals.RemoveAt(0);

                    newIndex = nextNode;
                    nextNode++;

                    TrackCircuitSection.SplitSection(thisIndex, newIndex, thisSection.Length - thisSignal.SignalLocation);
                    TrackCircuitSection newSection = TrackCircuitList[newIndex];
                    newSection.EndSignals[TrackDirection.Ahead] = thisSignal.Signal;
                    thisSection = TrackCircuitList[thisIndex];
                    addIndex.Add(newIndex);

                    // restore list (link is lost as item is replaced)
                    sectionSignals = thisSection.CircuitItems.TrackCircuitSignals[0][(int)SignalFunction.Normal];
                }
            }

            //
            // in direction Heading.Reverse, check original item and all added items
            //

            foreach (int actIndex in addIndex)
            {
                thisIndex = actIndex;

                while (thisIndex > 0)
                {
                    thisSection = TrackCircuitList[thisIndex];

                    newIndex = -1;
                    if (thisSection.CircuitType == TrackCircuitType.Normal)
                    {

                        List<TrackCircuitSignalItem> sectionSignals = thisSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse][(int)SignalFunction.Normal];

                        if (sectionSignals.Count > 0)
                        {
                            TrackCircuitSignalItem thisSignal = sectionSignals[0];
                            sectionSignals.RemoveAt(0);

                            newIndex = nextNode;
                            nextNode++;

                            TrackCircuitSection.SplitSection(thisIndex, newIndex, thisSignal.SignalLocation);
                            TrackCircuitSection newSection = TrackCircuitList[newIndex];
                            newSection.EndSignals[TrackDirection.Ahead] = null;
                            thisSection = TrackCircuitList[thisIndex];
                            thisSection.EndSignals[TrackDirection.Reverse] = thisSignal.Signal;

                            // restore list (link is lost as item is replaced)
                            sectionSignals = thisSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse][(int)SignalFunction.Normal];
                        }
                    }
                    thisIndex = thisSection.CircuitItems.TrackCircuitSignals[TrackDirection.Reverse][(int)SignalFunction.Normal].Count > 0 ? thisIndex : newIndex;
                }
            }

            return (nextNode);
        }

        //================================================================================================//
        /// <summary>
        /// Split CrossOvers
        /// </summary>
        private int SplitNodesCrossover(CrossOverInfo CrossOver,
                TrackSectionsFile tsectiondat, int nextNode)
        {
            bool processCrossOver = true;
            int sectionIndex0 = 0;
            int sectionIndex1 = 0;

            if (CrossOver.Details[Location.NearEnd].SectionIndex < 0 || CrossOver.Details[Location.FarEnd].SectionIndex < 0)
            {
                Trace.TraceWarning($"Incomplete crossover : indices {CrossOver.Details[Location.NearEnd].ItemIndex} and {CrossOver.Details[Location.FarEnd].ItemIndex}");
                processCrossOver = false;
            }
            if (CrossOver.Details[Location.NearEnd].SectionIndex == CrossOver.Details[Location.FarEnd].SectionIndex)
            {
                Trace.TraceWarning($"Invalid crossover : indices {CrossOver.Details[Location.NearEnd].ItemIndex} and {CrossOver.Details[Location.FarEnd].ItemIndex} : equal section : {CrossOver.Details[Location.NearEnd].SectionIndex}");
                processCrossOver = false;
            }

            if (processCrossOver)
            {
                sectionIndex0 = GetCrossOverSectionIndex(CrossOver.Details[Location.NearEnd]);
                sectionIndex1 = GetCrossOverSectionIndex(CrossOver.Details[Location.FarEnd]);

                if (sectionIndex0 < 0 || sectionIndex1 < 0)
                {
                    processCrossOver = false;
                }
            }

            if (processCrossOver)
            {
                int newSection0 = nextNode;
                nextNode++;
                int newSection1 = nextNode;
                nextNode++;
                int jnSection = nextNode;
                nextNode++;

                TrackCircuitSection.SplitSection(sectionIndex0, newSection0, CrossOver.Details[Location.NearEnd].Position);
                TrackCircuitSection.SplitSection(sectionIndex1, newSection1, CrossOver.Details[Location.FarEnd].Position);

                TrackCircuitSection.AddCrossoverJunction(sectionIndex0, newSection0, sectionIndex1, newSection1,
                                jnSection, CrossOver, tsectiondat);
            }

            return (nextNode);
        }

        //================================================================================================//
        /// <summary>
        /// Get cross-over section index
        /// </summary>

        private int GetCrossOverSectionIndex(CrossOverInfo.Detail crossOver)
        {
            int sectionIndex = crossOver.SectionIndex;
            float position = crossOver.Position;
            TrackCircuitSection section = TrackCircuitList[sectionIndex];

            while (position > 0 && position > section.Length)
            // while (position > 0 && position > section.Length && section.OriginalIndex == firstSectionOriginalIndex)
            {
                int prevSection = sectionIndex;
                position -= section.Length;
                crossOver.Position = position;
                sectionIndex = section.Pins[TrackDirection.Reverse, Location.NearEnd].Link;

                if (sectionIndex > 0)
                {
                    section = TrackCircuitList[sectionIndex];
                    if (section.CircuitType == TrackCircuitType.Crossover)
                    {
                        if (section.Pins[TrackDirection.Ahead, Location.NearEnd].Link == prevSection)
                        {
                            sectionIndex = section.Pins[TrackDirection.Reverse, Location.NearEnd].Link;
                        }
                        else
                        {
                            sectionIndex = section.Pins[TrackDirection.Reverse, Location.FarEnd].Link;
                        }
                        section = TrackCircuitList[sectionIndex];
                    }
                }
                else
                {
                    position = -1;  // no position found //
                }
            }

            if (position < 0)
            {
                Trace.TraceWarning($"Cannot locate CrossOver {crossOver.ItemIndex} in Section {crossOver.SectionIndex}");
                sectionIndex = -1;
            }

            return (sectionIndex);
        }

        //================================================================================================//
        /// <summary>
        /// Check pin links
        /// </summary>

        private int performLinkTest(int thisNode, int nextNode)
        {

            TrackCircuitSection thisSection = TrackCircuitList[thisNode];

            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                foreach (Location pinLocation in EnumExtension.GetValues<Location>())
                {
                    int linkedNode = thisSection.Pins[direction, pinLocation].Link;
                    TrackDirection linkedDirection = thisSection.Pins[direction, pinLocation].Direction.Next();

                    if (linkedNode > 0)
                    {
                        TrackCircuitSection linkedSection = TrackCircuitList[linkedNode];

                        bool linkfound = false;
                        bool doublelink = false;
                        int doublenode = -1;

                        foreach (Location linkedPin in EnumExtension.GetValues<Location>())
                        {
                            if (linkedSection.Pins[linkedDirection, linkedPin].Link == thisNode)
                            {
                                linkfound = true;
                                if (linkedSection.ActivePins[linkedDirection, linkedPin].Link == -1)
                                {
                                    linkedSection.ActivePins[linkedDirection, linkedPin] = linkedSection.ActivePins[linkedDirection, linkedPin].FromLink(thisNode);
                                }
                                else
                                {
                                    doublelink = true;
                                    doublenode = linkedSection.ActivePins[linkedDirection, linkedPin].Link;
                                }
                            }
                        }

                        if (!linkfound)
                        {
                            Trace.TraceWarning("Ignored invalid track node {0} pin [{1},{2}] link to track node {3}", thisNode, direction, pinLocation, linkedNode);
                            int endNode = nextNode;
                            nextNode++;
                            TrackCircuitSection.InsertEndNode(thisNode, direction, pinLocation, endNode);
                        }

                        if (doublelink)
                        {
                            Trace.TraceWarning("Ignored invalid track node {0} pin [{1},{2}] link to track node {3}; already linked to track node {4}", thisNode, direction, pinLocation, linkedNode, doublenode);
                            int endNode = nextNode;
                            nextNode++;
                            TrackCircuitSection.InsertEndNode(thisNode, direction, pinLocation, endNode);
                        }
                    }
                    else if (linkedNode == 0)
                    {
                        Trace.TraceWarning("Ignored invalid track node {0} pin [{1},{2}] link to track node {3}", thisNode, direction, pinLocation, linkedNode);
                        int endNode = nextNode;
                        nextNode++;
                        TrackCircuitSection.InsertEndNode(thisNode, direction, pinLocation, endNode);
                    }
                }
            }

            return (nextNode);
        }

        //================================================================================================//
        /// <summary>
        /// set active pins for non-junction links
        /// </summary>

        private void setActivePins(int thisNode)
        {
            TrackCircuitSection thisSection = TrackCircuitList[thisNode];

            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                foreach (Location pinLocation in EnumExtension.GetValues<Location>())
                {
                    if (thisSection.Pins[direction, pinLocation].Link > 0)
                    {
                        TrackCircuitSection nextSection = null;

                        if (thisSection.CircuitType == TrackCircuitType.Junction)
                        {
                            int nextIndex = thisSection.Pins[direction, pinLocation].Link;
                            nextSection = TrackCircuitList[nextIndex];

                            if (thisSection.Pins[direction, Location.FarEnd].Link > 0)    // Junction end
                            {
                                thisSection.ActivePins[direction, pinLocation] = thisSection.Pins[direction, pinLocation].FromLink(-1);
                            }
                            else
                            {
                                thisSection.ActivePins[direction, pinLocation] = thisSection.Pins[direction, pinLocation];
                            }
                        }
                        else if (thisSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            int nextIndex = thisSection.Pins[direction, pinLocation].Link;
                            nextSection = TrackCircuitList[nextIndex];

                            thisSection.ActivePins[direction, pinLocation] = thisSection.Pins[direction, pinLocation].FromLink(-1);
                        }
                        else
                        {
                            int nextIndex = thisSection.Pins[direction, pinLocation].Link;
                            nextSection = TrackCircuitList[nextIndex];

                            thisSection.ActivePins[direction, pinLocation] = thisSection.Pins[direction, pinLocation];
                        }


                        if (nextSection != null && nextSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            thisSection.ActivePins[direction, pinLocation] = thisSection.ActivePins[direction, pinLocation].FromLink(-1);
                        }
                        else if (nextSection != null && nextSection.CircuitType == TrackCircuitType.Junction)
                        {
                            TrackDirection nextDirection = thisSection.Pins[direction, pinLocation].Direction.Next();
                            //                          int nextDirection = thisSection.Pins[iDirection, iPin].Direction;
                            if (nextSection.Pins[nextDirection, Location.FarEnd].Link > 0)
                            {
                                thisSection.ActivePins[direction, pinLocation] = thisSection.ActivePins[direction, pinLocation].FromLink(-1);
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// set cross-reference to tracknodes
        /// </summary>

        private void setCrossReference(int thisNode, TrackNode[] trackNodes)
        {
            TrackCircuitSection thisSection = TrackCircuitList[thisNode];
            if (thisSection.OriginalIndex > 0 && thisSection.CircuitType != TrackCircuitType.Crossover)
            {
                TrackNode thisTrack = trackNodes[thisSection.OriginalIndex];
                float offset0 = thisSection.OffsetLength[Location.NearEnd];
                float offset1 = thisSection.OffsetLength[Location.FarEnd];

                TrackCircuitSectionCrossReference newReference = new TrackCircuitSectionCrossReference(thisSection.Index, thisSection.Length, thisSection.OffsetLength.ToArray());

                bool inserted = false;

                TrackCircuitCrossReferences thisXRef = thisTrack.TrackCircuitCrossReferences;
                for (int iPart = 0; iPart < thisXRef.Count && !inserted; iPart++)
                {
                    TrackCircuitSectionCrossReference thisReference = thisXRef[iPart];
                    if (offset0 < thisReference.OffsetLength[0])
                    {
                        thisXRef.Insert(iPart, newReference);
                        inserted = true;
                    }
                    else if (offset1 > thisReference.OffsetLength[1])
                    {
                        thisXRef.Insert(iPart, newReference);
                        inserted = true;
                    }
                }

                if (!inserted)
                {
                    thisTrack.TrackCircuitCrossReferences.Add(newReference);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// set cross-reference to tracknodes for CrossOver items
        /// </summary>

        private void setCrossReferenceCrossOver(int thisNode, TrackNode[] trackNodes)
        {
            TrackCircuitSection thisSection = TrackCircuitList[thisNode];
            if (thisSection.OriginalIndex > 0 && thisSection.CircuitType == TrackCircuitType.Crossover)
            {
                foreach (Location pinLocation in EnumExtension.GetValues<Location>())
                {
                    int prevIndex = thisSection.Pins[TrackDirection.Ahead, pinLocation].Link;
                    TrackCircuitSection prevSection = TrackCircuitList[prevIndex];

                    TrackCircuitSectionCrossReference newReference = new TrackCircuitSectionCrossReference(thisSection.Index, thisSection.Length, thisSection.OffsetLength.ToArray());
                    TrackNode thisTrack = trackNodes[prevSection.OriginalIndex];
                    TrackCircuitCrossReferences thisXRef = thisTrack.TrackCircuitCrossReferences;

                    bool inserted = false;
                    for (int iPart = 0; iPart < thisXRef.Count && !inserted; iPart++)
                    {
                        TrackCircuitSectionCrossReference thisReference = thisXRef[iPart];
                        if (thisReference.Index == prevIndex)
                        {
                            newReference.OffsetLength[0] = thisReference.OffsetLength[0];
                            newReference.OffsetLength[1] = thisReference.OffsetLength[1] + thisReference.Length;
                            thisXRef.Insert(iPart, newReference);
                            inserted = true;
                        }
                    }

                    if (!inserted)
                    {
                        Trace.TraceWarning("ERROR : cannot find XRef for leading track to crossover {0}",
                            thisNode);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set physical switch
        /// </summary>

        public void setSwitch(int nodeIndex, int switchPos, TrackCircuitSection thisSection)
        {
            if (MPManager.NoAutoSwitch()) return;
            TrackJunctionNode thisNode = trackDB.TrackNodes[nodeIndex] as TrackJunctionNode;
            thisNode.SelectedRoute = switchPos;
            thisSection.JunctionLastRoute = switchPos;

            // update any linked signals
            foreach (int thisSignalIndex in thisSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = SignalObjects[thisSignalIndex];
                thisSignal.Update();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Node control track clearance update request
        /// </summary>

        public void requestClearNode(Train.TrainRouted thisTrain, Train.TCSubpathRoute routePart)
        {
#if DEBUG_REPORTS
			File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Request for clear node from train {0} at section {1} starting from {2}\n",
				thisTrain.Train.Number,
				thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex,
				thisTrain.Train.LastReservedSection[thisTrain.TrainRouteDirectionIndex]));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Request for clear node from train {0} at section {1} starting from {2}\n",
                    thisTrain.Train.Number,
                    thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex,
                    thisTrain.Train.LastReservedSection[thisTrain.TrainRouteDirectionIndex]));
            }

            // check if present clearance is beyond required maximum distance

            int sectionIndex = -1;
            Train.TCRouteElement thisElement = null;
            TrackCircuitSection thisSection = null;

            List<int> sectionsInRoute = new List<int>();

            float clearedDistanceM = 0.0f;
            Train.END_AUTHORITY endAuthority = Train.END_AUTHORITY.NO_PATH_RESERVED;
            int routeIndex = -1;
            float maxDistance = Math.Max(thisTrain.Train.AllowedMaxSpeedMpS * thisTrain.Train.maxTimeS, thisTrain.Train.minCheckDistanceM);

            int lastReserved = thisTrain.Train.LastReservedSection[thisTrain.TrainRouteDirectionIndex];
            int endListIndex = -1;

            bool furthestRouteCleared = false;

            Train.TCSubpathRoute thisRoute = new Train.TCSubpathRoute(thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex]);
            Train.TCPosition thisPosition = new Train.TCPosition();
            thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].CopyTo(ref thisPosition);

            // for loop detection, set occupied sections in sectionsInRoute list - but remove present position
            foreach (TrackCircuitSection occSection in thisTrain.Train.OccupiedTrack)
            {
                sectionsInRoute.Add(occSection.Index);
            }

            // correct for invalid combination of present position and occupied sections
            if (sectionsInRoute.Count > 0 && thisPosition.TCSectionIndex != sectionsInRoute.First() && thisPosition.TCSectionIndex != sectionsInRoute.Last())
            {
                if (thisTrain.Train.PresentPosition[1].TCSectionIndex == sectionsInRoute.First())
                {
                    bool remove = true;
                    for (int iindex = sectionsInRoute.Count - 1; iindex >= 0 && remove; iindex--)
                    {
                        if (sectionsInRoute[iindex] == thisPosition.TCSectionIndex)
                        {
                            remove = false;
                        }
                        else
                        {
                            sectionsInRoute.RemoveAt(iindex);
                        }
                    }
                }
                else if (thisTrain.Train.PresentPosition[1].TCSectionIndex == sectionsInRoute.Last())
                {
                    bool remove = true;
                    for (int iindex = 0; iindex < sectionsInRoute.Count && remove; iindex++)
                    {
                        if (sectionsInRoute[iindex] == thisPosition.TCSectionIndex)
                        {
                            remove = false;
                        }
                        else
                        {
                            sectionsInRoute.RemoveAt(iindex);
                        }
                    }
                }
            }

            sectionsInRoute.Remove(thisPosition.TCSectionIndex);

            // check if last reserved on present route

            if (lastReserved > 0)
            {

                endListIndex = thisRoute.GetRouteIndex(lastReserved, thisPosition.RouteListIndex);

                // check if backward in route - if so, route is valid and obstacle is in present section

                if (endListIndex < 0)
                {
                    int prevListIndex = -1;
                    for (int iNode = thisPosition.RouteListIndex; iNode >= 0 && prevListIndex < 0; iNode--)
                    {
                        thisElement = thisRoute[iNode];
                        if (thisElement.TCSectionIndex == lastReserved)
                        {
                            prevListIndex = iNode;
                        }
                    }

                    if (prevListIndex < 0)     // section is really off route - perform request from present position
                    {
                        BreakDownRoute(thisPosition.TCSectionIndex, thisTrain);
                    }
                }
            }

            if (thisTrain.Train.CheckTrain)
            {
                if (endListIndex >= 0)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        String.Format("Index in route list : {0} = {1}\n",
                        endListIndex, thisRoute[endListIndex].TCSectionIndex));
                }
                else
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        String.Format("Index in route list : {0}\n",
                        endListIndex));
                }
            }

            // if section is (still) set, check if this is at maximum distance

            if (endListIndex >= 0)
            {
                routeIndex = endListIndex;
                clearedDistanceM = thisTrain.Train.GetDistanceToTrain(lastReserved, 0.0f);

                if (clearedDistanceM > maxDistance)
                {
                    endAuthority = Train.END_AUTHORITY.MAX_DISTANCE;
                    furthestRouteCleared = true;
                    if (thisTrain.Train.CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                            String.Format("Cleared Distance : {0} > Max Distance \n",
                            FormatStrings.FormatDistance(clearedDistanceM, true)));
                    }

                }
                else
                {
                    for (int iIndex = thisPosition.RouteListIndex + 1; iIndex < routeIndex; iIndex++)
                    {
                        sectionsInRoute.Add(thisRoute[iIndex].TCSectionIndex);
                    }
                }
            }
            else
            {
                routeIndex = thisPosition.RouteListIndex;   // obstacle is in present section
            }

            if (routeIndex < 0) return;//by JTang

            int lastRouteIndex = routeIndex;
            float offset = 0.0f;
            if (routeIndex == thisPosition.RouteListIndex)
            {
                offset = thisPosition.TCOffset;
            }

            // if authority type is loop and loop section is still occupied by train, no need for any checks

            if (thisTrain.Train.LoopSection >= 0)
            {
                thisSection = TrackCircuitList[thisTrain.Train.LoopSection];

                // test if train is really occupying this section
                Train.TCSubpathRoute tempRoute = BuildTempRoute(thisTrain.Train, thisTrain.Train.PresentPosition[1].TCSectionIndex, thisTrain.Train.PresentPosition[1].TCOffset,
                    (TrackDirection)thisTrain.Train.PresentPosition[1].TCDirection, thisTrain.Train.Length, true, true, false);

                if (tempRoute.GetRouteIndex(thisSection.Index, 0) < 0)
                {
                    thisTrain.Train.OccupiedTrack.Clear();
                    foreach (Train.TCRouteElement thisOccupyElement in tempRoute)
                    {
                        thisTrain.Train.OccupiedTrack.Add(TrackCircuitList[thisOccupyElement.TCSectionIndex]);
                    }
                }

                if (thisSection.CircuitState.OccupiedByThisTrain(thisTrain.Train) ||
                    (thisSection.CircuitState.TrainReserved != null && thisSection.CircuitState.TrainReserved.Train == thisTrain.Train))
                {
                    furthestRouteCleared = true;
                    endAuthority = Train.END_AUTHORITY.LOOP;
                }
                else
                {
                    // update trains ValidRoute to avoid continuation at wrong entry
                    int rearIndex = thisTrain.Train.PresentPosition[1].RouteListIndex;
                    int nextIndex = routePart.GetRouteIndex(thisTrain.Train.LoopSection, rearIndex);
                    int firstIndex = routePart.GetRouteIndex(thisTrain.Train.LoopSection, 0);

                    if (firstIndex != nextIndex)
                    {
                        for (int iIndex = 0; iIndex < rearIndex; iIndex++)
                        {
                            thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex][iIndex].TCSectionIndex = -1; // invalidate route upto loop point
                        }
                        routePart = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex];
                    }

                    thisTrain.Train.LoopSection = -1;
                }
            }

            // try to clear further ahead if required

            if (!furthestRouteCleared)
            {

                if (thisTrain.Train.CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        String.Format("Starting check from : Index in route list : {0} = {1}\n",
                        routeIndex, thisRoute[routeIndex].TCSectionIndex));
                }

                // check if train ahead still in last available section

                bool routeAvailable = true;
                thisSection = TrackCircuitList[routePart[routeIndex].TCSectionIndex];

                float posOffset = thisPosition.TCOffset;
                int posDirection = thisPosition.TCDirection;

                if (routeIndex > thisPosition.RouteListIndex)
                {
                    posOffset = 0;
                    posDirection = routePart[routeIndex].Direction;
                }

                Dictionary<Train, float> trainAhead =
                        thisSection.TestTrainAhead(thisTrain.Train, posOffset, posDirection);

                if (thisTrain.Train.CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        String.Format("Train ahead in section {0} : {1}\n",
                        thisSection.Index, trainAhead.Count));
                }

                if (trainAhead.Count > 0)
                {
                    routeAvailable = false;

                    // if section is junction or crossover, use next section as last, otherwise use this section as last
                    if (thisSection.CircuitType != TrackCircuitType.Junction && thisSection.CircuitType != TrackCircuitType.Crossover)
                    {
                        lastRouteIndex = routeIndex - 1;
                    }

                    if (thisTrain.Train.CheckTrain)
                    {
                        if (lastRouteIndex >= 0)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                            String.Format("Set last valid section : Index in route list : {0} = {1}\n",
                            lastRouteIndex, thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex][lastRouteIndex].TCSectionIndex));
                        }
                        else
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "First Section in Route\n");
                        }
                    }
                }

                // train ahead has moved on, check next sections

                int startRouteIndex = routeIndex;

                while (routeIndex < routePart.Count && routeAvailable && !furthestRouteCleared)
                {
                    if (thisTrain.Train.CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                            String.Format("Checking : Index in route list : {0} = {1}\n",
                            routeIndex, thisRoute[routeIndex].TCSectionIndex));
                    }

                    thisElement = routePart[routeIndex];
                    sectionIndex = thisElement.TCSectionIndex;
                    thisSection = TrackCircuitList[sectionIndex];

                    // check if section is in loop

                    if (sectionsInRoute.Contains(thisSection.Index) ||
                        (routeIndex > startRouteIndex && sectionIndex == thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex))
                    {
                        endAuthority = Train.END_AUTHORITY.LOOP;
                        thisTrain.Train.LoopSection = thisSection.Index;
                        routeAvailable = false;

                        Trace.TraceInformation("Train {0} ({1}) : Looped at {2}", thisTrain.Train.Name, thisTrain.Train.Number, thisSection.Index);

                        if (thisTrain.Train.CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                "Section looped \n");
                        }
                    }

                    // check if section is access to pool

                    else if (thisTrain.Train.CheckPoolAccess(thisSection.Index))
                    {
                        routeAvailable = false;
                        furthestRouteCleared = true;
                    }

                    // check if section is available

                    else if (thisSection.GetSectionStateClearNode(thisTrain, thisElement.Direction, routePart))
                    {
                        lastReserved = thisSection.Index;
                        lastRouteIndex = routeIndex;
                        sectionsInRoute.Add(thisSection.Index);
                        clearedDistanceM += thisSection.Length - offset;
                        if (thisTrain.Train.CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                "Section clear \n");
                        }

                        if (thisSection.CircuitState.OccupiedByOtherTrains(thisTrain))
                        {
                            bool trainIsAhead = false;

                            // section is still ahead
                            if (thisSection.Index != thisPosition.TCSectionIndex)
                            {
                                trainIsAhead = true;
                            }
                            // same section
                            else
                            {
                                trainAhead = thisSection.TestTrainAhead(thisTrain.Train, thisPosition.TCOffset, thisPosition.TCDirection);
                                if (trainAhead.Count > 0 && thisSection.CircuitType == TrackCircuitType.Normal) // do not end path on junction
                                {
                                    trainIsAhead = true;
                                }
                            }

                            if (trainIsAhead)
                            {
                                if (thisTrain.Train.CheckTrain)
                                {
                                    File.AppendAllText(@"C:\temp\checktrain.txt",
                                        String.Format("Train ahead in section {0} : {1}\n",
                                        thisSection.Index, trainAhead.Count));
                                }
                                lastRouteIndex = routeIndex - 1;
                                lastReserved = lastRouteIndex >= 0 ? routePart[lastRouteIndex].TCSectionIndex : -1;
                                routeAvailable = false;
                                clearedDistanceM -= thisSection.Length + offset; // correct length as this section was already added to total length
                            }
                        }

                        if (routeAvailable)
                        {
                            routeIndex++;
                            offset = 0.0f;

                            if (!thisSection.CircuitState.OccupiedByThisTrain(thisTrain) &&
                                thisSection.CircuitState.TrainReserved == null)
                            {
                                thisSection.Reserve(thisTrain, routePart);
                            }

                            if (!furthestRouteCleared && thisSection.EndSignals[(TrackDirection)thisElement.Direction] != null)
                            {
                                Signal endSignal = thisSection.EndSignals[(TrackDirection)thisElement.Direction];
                                // check if signal enabled for other train - if so, keep in node control
                                if (endSignal.EnabledTrain == null || endSignal.EnabledTrain == thisTrain)
                                {
                                    if (routeIndex < routePart.Count)
                                    {
                                        thisTrain.Train.SwitchToSignalControl(thisSection.EndSignals[(TrackDirection)thisElement.Direction]);
                                    }
                                }
                                furthestRouteCleared = true;
                            }

                            if (clearedDistanceM > thisTrain.Train.minCheckDistanceM &&
                                            clearedDistanceM > (thisTrain.Train.AllowedMaxSpeedMpS * thisTrain.Train.maxTimeS))
                            {
                                endAuthority = Train.END_AUTHORITY.MAX_DISTANCE;
                                furthestRouteCleared = true;
                            }
                        }
                    }

                    // section is not available
                    else
                    {
                        lastRouteIndex = routeIndex - 1;
                        lastReserved = lastRouteIndex >= 0 ? routePart[lastRouteIndex].TCSectionIndex : -1;
                        routeAvailable = false;
                    }
                }
            }

            // if not cleared to max distance or looped, determine reason

            if (!furthestRouteCleared && lastRouteIndex > 0 && routePart[lastRouteIndex].TCSectionIndex >= 0 && endAuthority != Train.END_AUTHORITY.LOOP)
            {

                thisElement = routePart[lastRouteIndex];
                sectionIndex = thisElement.TCSectionIndex;
                thisSection = TrackCircuitList[sectionIndex];

                if (thisTrain.Train.CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        String.Format("Last section cleared in route list : {0} = {1}\n",
                        lastRouteIndex, thisRoute[lastRouteIndex].TCSectionIndex));
                }
                // end of track reached

                if (thisSection.CircuitType == TrackCircuitType.EndOfTrack)
                {
                    endAuthority = Train.END_AUTHORITY.END_OF_TRACK;
                    furthestRouteCleared = true;
                    if (thisTrain.Train.CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                            "End of track \n");
                    }
                }

                // end of path reached

                if (!furthestRouteCleared)
                {
                    if (lastRouteIndex > (routePart.Count - 1))
                    {
                        endAuthority = Train.END_AUTHORITY.END_OF_PATH;
                        furthestRouteCleared = true;
                        if (thisTrain.Train.CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                "End of path \n");
                        }
                    }
                }
            }

            // check if next section is switch held against train

            if (!furthestRouteCleared && lastRouteIndex < (routePart.Count - 1))
            {
                Train.TCRouteElement nextElement = routePart[lastRouteIndex + 1];
                sectionIndex = nextElement.TCSectionIndex;
                thisSection = TrackCircuitList[sectionIndex];
                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!thisSection.IsAvailable(thisTrain))
                    {
                        // check if switch is set to required path - if so, do not classify as reserved switch even if it is reserved by another train

                        int jnIndex = routePart.GetRouteIndex(sectionIndex, 0);
                        bool jnAligned = false;
                        if (jnIndex < routePart.Count - 1)
                        {
                            if (routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Ahead, Location.NearEnd].Link ||
                                routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Ahead, Location.FarEnd].Link)
                            {
                                if (routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Reverse, Location.NearEnd].Link ||
                                    routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Reverse, Location.FarEnd].Link)
                                {
                                    jnAligned = true;
                                }
                            }
                            else if (routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Reverse, Location.NearEnd].Link ||
                                routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Reverse, Location.FarEnd].Link)
                            {
                                if (routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Ahead, Location.NearEnd].Link ||
                                    routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[TrackDirection.Ahead, Location.FarEnd].Link)
                                {
                                    jnAligned = true;
                                }
                            }
                        }

                        // switch is not properly set, so it blocks the path
                        if (!jnAligned)
                        {
                            endAuthority = Train.END_AUTHORITY.RESERVED_SWITCH;
                            furthestRouteCleared = true;
                            if (thisTrain.Train.CheckTrain)
                            {
                                File.AppendAllText(@"C:\temp\checktrain.txt",
                                    "Reserved Switch \n");
                            }
                        }
                    }
                }
            }

            // check if next section is occupied by stationary train or train moving in similar direction
            // if so calculate distance to end of train
            // only allowed for NORMAL sections and if not looped

            if (!furthestRouteCleared && lastRouteIndex < (routePart.Count - 1) && endAuthority != Train.END_AUTHORITY.LOOP)
            {
                Train.TCRouteElement nextElement = routePart[lastRouteIndex + 1];
                int reqDirection = nextElement.Direction;
                int revDirection = nextElement.Direction == 0 ? 1 : 0;

                sectionIndex = nextElement.TCSectionIndex;
                thisSection = TrackCircuitList[sectionIndex];

                if (thisSection.CircuitType == TrackCircuitType.Normal &&
                           thisSection.CircuitState.OccupiedByOtherTrains(thisTrain))
                {
                    if (thisSection.CircuitState.OccupiedByOtherTrains(revDirection, false, thisTrain))
                    {
                        endAuthority = Train.END_AUTHORITY.TRAIN_AHEAD;
                        furthestRouteCleared = true;
                        if (thisTrain.Train.CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                "Train Ahead \n");
                        }
                    }
                    // check for train further ahead and determine distance to train
                    Dictionary<Train, float> trainAhead =
                                            thisSection.TestTrainAhead(thisTrain.Train, offset, reqDirection);

                    if (trainAhead.Count > 0)
                    {
                        foreach (KeyValuePair<Train, float> thisTrainAhead in trainAhead)  // there is only one value
                        {
                            endAuthority = Train.END_AUTHORITY.TRAIN_AHEAD;
                            clearedDistanceM += thisTrainAhead.Value;
                            furthestRouteCleared = true;
                            if (thisTrain.Train.CheckTrain)
                            {
                                File.AppendAllText(@"C:\temp\checktrain.txt",
                                    "Train Ahead \n");
                            }
                        }
                    }
                }
                else if (thisSection.GetSectionStateClearNode(thisTrain, thisElement.Direction, routePart))
                {
                    endAuthority = Train.END_AUTHORITY.END_OF_AUTHORITY;
                    furthestRouteCleared = true;
                }
                else if (thisSection.CircuitType == TrackCircuitType.Crossover || thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    // first not-available section is crossover or junction - treat as reserved switch
                    endAuthority = Train.END_AUTHORITY.RESERVED_SWITCH;
                }
            }

            else if (routeIndex >= routePart.Count)
            {
                endAuthority = Train.END_AUTHORITY.END_OF_AUTHORITY;
            }

            // update train details

            thisTrain.Train.EndAuthorityType[thisTrain.TrainRouteDirectionIndex] = endAuthority;
            thisTrain.Train.LastReservedSection[thisTrain.TrainRouteDirectionIndex] = lastReserved;
            thisTrain.Train.DistanceToEndNodeAuthorityM[thisTrain.TrainRouteDirectionIndex] = clearedDistanceM;

            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Returned : \n    State : {0}\n    Dist  : {1}\n    Sect  : {2}\n",
                    endAuthority, FormatStrings.FormatDistance(clearedDistanceM, true), lastReserved));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Break down reserved route
        /// </summary>

        public void BreakDownRoute(int firstSectionIndex, Train.TrainRouted reqTrain)
        {
            if (firstSectionIndex < 0)
                return; // no route to break down

            TrackCircuitSection firstSection = TrackCircuitList[firstSectionIndex];
            Train.TrainRouted thisTrain = firstSection.CircuitState.TrainReserved;

            // if occupied by train - skip actions and proceed to next section

            if (!firstSection.CircuitState.OccupiedByThisTrain(reqTrain))
            {

                // if not reserved - no further route ahead

                if (thisTrain == null)
                {
                    return;
                }

                if (thisTrain != reqTrain)
                {
                    return;   // section reserved for other train - stop action
                }

                // unreserve first section

                firstSection.UnreserveTrain(thisTrain, true);
            }

            // check which direction to go

            TrackCircuitSection nextSection = null;
            TrackDirection nextDirection = TrackDirection.Ahead;

            foreach (TrackDirection direction in EnumExtension.GetValues<TrackDirection>())
            {
                foreach (Location pinLocation in EnumExtension.GetValues<Location>())
                {
                    int trySectionIndex = firstSection.Pins[direction, pinLocation].Link;
                    if (trySectionIndex > 0)
                    {
                        TrackCircuitSection trySection = TrackCircuitList[trySectionIndex];
                        if (trySection.CircuitState.TrainReserved != null && reqTrain != null && trySection.CircuitState.TrainReserved.Train == reqTrain.Train)
                        {
                            nextSection = trySection;
                            nextDirection = firstSection.Pins[direction, pinLocation].Direction;
                        }
                    }
                }
            }

            // run back through all reserved sections

            while (nextSection != null)
            {
                nextSection.UnreserveTrain(reqTrain, true);
                TrackCircuitSection thisSection = nextSection;
                nextSection = null;

                // try to find next section using active links

                TrackCircuitSection trySection = null;

                TrackDirection currentDirection = nextDirection;
                foreach (Location pinLocation in EnumExtension.GetValues<Location>())
                {
                    int trySectionIndex = thisSection.ActivePins[currentDirection, pinLocation].Link;
                    if (trySectionIndex > 0)
                    {
                        trySection = TrackCircuitList[trySectionIndex];
                        if (trySection.CircuitState.TrainReserved != null && reqTrain != null && trySection.CircuitState.TrainReserved.Train == reqTrain.Train)
                        {
                            nextSection = trySection;
                            nextDirection = thisSection.ActivePins[currentDirection, pinLocation].Direction;
                        }
                    }
                }

                // not found, then try possible links
                foreach (Location pinLocation in EnumExtension.GetValues<Location>())
                {
                    int trySectionIndex = thisSection.Pins[currentDirection, pinLocation].Link;
                    if (trySectionIndex > 0)
                    {
                        trySection = TrackCircuitList[trySectionIndex];
                        if (trySection.CircuitState.TrainReserved != null && reqTrain != null && trySection.CircuitState.TrainReserved.Train == reqTrain.Train)
                        {
                            nextSection = trySection;
                            nextDirection = thisSection.Pins[currentDirection, pinLocation].Direction;
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Break down reserved route using route list
        /// </summary>

        public void BreakDownRouteList(Train.TCSubpathRoute reqRoute, int firstRouteIndex, Train.TrainRouted reqTrain)
        {
            for (int iindex = reqRoute.Count - 1; iindex >= 0 && iindex >= firstRouteIndex; iindex--)
            {
                TrackCircuitSection thisSection = TrackCircuitList[reqRoute[iindex].TCSectionIndex];
                if (!thisSection.CircuitState.OccupiedByThisTrain(reqTrain.Train))
                {
                    thisSection.RemoveTrain(reqTrain.Train, true);
                }
                else
                {
                    Signal thisSignal = thisSection.EndSignals[(TrackDirection)reqRoute[iindex].Direction];
                    if (thisSignal != null)
                    {
                        thisSignal.ResetSignal(false);
                    }
                }
            }
        }

        //================================================================================================//
        /// Build temp route for train
        /// <summary>
        /// Used for trains without path (eg stationary constists), manual operation
        /// </summary>

        public Train.TCSubpathRoute BuildTempRoute(Train thisTrain,
                int firstSectionIndex, float firstOffset, TrackDirection firstDirection,
                float routeLength, bool overrideManualSwitchState, bool autoAlign, bool stopAtFacingSignal)
        {
            bool honourManualSwitchState = !overrideManualSwitchState;
            List<int> sectionList = ScanRoute(thisTrain, firstSectionIndex, firstOffset, firstDirection,
                    true, routeLength, honourManualSwitchState, autoAlign, stopAtFacingSignal, false, true, false, false, false, false, false);
            Train.TCSubpathRoute tempRoute = new Train.TCSubpathRoute();
            int lastIndex = -1;

            foreach (int nextSectionIndex in sectionList)
            {
                int curDirection = nextSectionIndex < 0 ? 1 : 0;
                int thisSectionIndex = nextSectionIndex < 0 ? -nextSectionIndex : nextSectionIndex;
                TrackCircuitSection thisSection = TrackCircuitList[thisSectionIndex];

                Train.TCRouteElement thisElement = new Train.TCRouteElement(thisSection, curDirection, this, lastIndex);
                tempRoute.Add(thisElement);
                lastIndex = thisSectionIndex;
            }

            // set pin references for junction sections
            for (int iElement = 0; iElement < tempRoute.Count - 1; iElement++) // do not process last element as next element is required
            {
                Train.TCRouteElement thisElement = tempRoute[iElement];
                TrackCircuitSection thisSection = TrackCircuitList[thisElement.TCSectionIndex];

                if (thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    if (thisElement.OutPin[0] == 1) // facing switch
                    {
                        thisElement.OutPin[1] = thisSection.Pins[TrackDirection.Reverse, Location.NearEnd].Link == tempRoute[iElement + 1].TCSectionIndex ? 0 : 1;
                    }
                }
            }

            return (tempRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Follow default route for train
        /// Use for :
        ///   - build temp list for trains without route (eg stat objects)
        ///   - build list for train under Manual control
        ///   - build list of sections when train slip backward
        ///   - search signal or speedpost ahead or at the rear of the train (either in facing or backward direction)
        ///
        /// Search ends :
        ///   - if required object is found
        ///   - if required length is covered
        ///   - if valid path only is requested and unreserved section is found (variable thisTrain required)
        ///   - end of track
        ///   - looped track
        ///   - re-enter in original route (for manual re-routing)
        ///
        /// Returned is list of sections, with positive no. indicating direction 0 and negative no. indicating direction 1
        /// If signal or speedpost is required, list will contain index of required item (>0 facing direction, <0 backing direction)
        /// </summary>

        public List<int> ScanRoute(Train thisTrain, int firstSectionIndex, float firstOffset, TrackDirection firstDirection, bool forward,
                float routeLength, bool honourManualSwitch, bool autoAlign, bool stopAtFacingSignal, bool reservedOnly, bool returnSections,
                bool searchFacingSignal, bool searchBackwardSignal, bool searchFacingSpeedpost, bool searchBackwardSpeedpost,
                bool isFreight, bool considerSpeedReset = false, bool checkReenterOriginalRoute = false)
        {

            int sectionIndex = firstSectionIndex;

            int lastIndex = -2;   // set to values not encountered for pin links
            int thisIndex = sectionIndex;

            float offset = firstOffset;
            TrackDirection curDirection = firstDirection;
            TrackDirection nextDirection = curDirection;

            TrackCircuitSection thisSection = TrackCircuitList[sectionIndex];

            float coveredLength = firstOffset;
            if (forward || (firstDirection == TrackDirection.Reverse && !forward))
            {
                coveredLength = thisSection.Length - firstOffset;
            }

            bool endOfRoute = false;
            List<int> foundItems = new List<int>();
            List<int> foundObject = new List<int>();

            while (!endOfRoute)
            {

                // check looped

                int routedIndex = curDirection == 0 ? thisIndex : -thisIndex;
                if (foundItems.Contains(thisIndex) || foundItems.Contains(-thisIndex))
                {
                    break;
                }

                // add section
                foundItems.Add(routedIndex);

                // set length, pin index and opp direction

                TrackDirection oppDirection = curDirection.Next();

                TrackDirection outPinDirection = forward ? curDirection : oppDirection;
                TrackDirection inPinDirection = outPinDirection.Next();

                // check all conditions and objects as required

                if (stopAtFacingSignal && thisSection.EndSignals[curDirection] != null)           // stop at facing signal
                {
                    endOfRoute = true;
                }

                // search facing speedpost
                if (searchFacingSpeedpost && thisSection.CircuitItems.TrackCircuitSpeedPosts[curDirection].Count > 0)
                {
                    List<TrackCircuitSignalItem> thisItemList = thisSection.CircuitItems.TrackCircuitSpeedPosts[curDirection];

                    if (forward)
                    {
                        for (int iObject = 0; iObject < thisItemList.Count && !endOfRoute; iObject++)
                        {
                            TrackCircuitSignalItem thisItem = thisItemList[iObject];

                            Signal thisSpeedpost = thisItem.Signal;
                            SpeedInfo speed_info = thisSpeedpost.this_lim_speed(SignalFunction.Speed);

                            if ((isFreight && speed_info.FreightSpeed > 0) || (!isFreight && speed_info.PassengerSpeed > 0))
                            {
                                if (thisItem.SignalLocation > offset)
                                {
                                    foundObject.Add(thisItem.Signal.Index);
                                    endOfRoute = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int iObject = thisItemList.Count - 1; iObject >= 0 && !endOfRoute; iObject--)
                        {
                            TrackCircuitSignalItem thisItem = thisItemList[iObject];

                            Signal thisSpeedpost = thisItem.Signal;
                            SpeedInfo speed_info = thisSpeedpost.this_lim_speed(SignalFunction.Speed);

                            if ((isFreight && speed_info.FreightSpeed > 0) || (!isFreight && speed_info.PassengerSpeed > 0))
                            {
                                if (offset == 0 || thisItem.SignalLocation < offset)
                                {
                                    foundObject.Add(thisItem.Signal.Index);
                                    endOfRoute = true;
                                }
                            }
                        }
                    }
                }

                if (searchFacingSignal && thisSection.EndSignals[curDirection] != null)           // search facing signal
                {
                    foundObject.Add(thisSection.EndSignals[curDirection].Index);
                    endOfRoute = true;
                }


                // search backward speedpost
                if (searchBackwardSpeedpost && thisSection.CircuitItems.TrackCircuitSpeedPosts[oppDirection].Count > 0)
                {
                    List<TrackCircuitSignalItem> thisItemList = thisSection.CircuitItems.TrackCircuitSpeedPosts[oppDirection];

                    if (forward)
                    {
                        for (int iObject = thisItemList.Count - 1; iObject >= 0 && !endOfRoute; iObject--)
                        {
                            TrackCircuitSignalItem thisItem = thisItemList[iObject];

                            Signal thisSpeedpost = thisItem.Signal;
                            SpeedInfo speed_info = thisSpeedpost.this_lim_speed(SignalFunction.Speed);
                            if (considerSpeedReset)
                            {
                                var speed_infoR = thisSpeedpost.this_sig_speed(SignalFunction.Speed);
                                speed_info.Reset = speed_infoR.Reset;
                            }
                            if ((isFreight && speed_info.FreightSpeed > 0) || (!isFreight && speed_info.PassengerSpeed > 0) || speed_info.Reset)
                            {
                                if (thisItem.SignalLocation < thisSection.Length - offset)
                                {
                                    endOfRoute = true;
                                    foundObject.Add(-(thisItem.Signal.Index));
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int iObject = 0; iObject < thisItemList.Count - 1 && !endOfRoute; iObject++)
                        {
                            TrackCircuitSignalItem thisItem = thisItemList[iObject];

                            Signal thisSpeedpost = thisItem.Signal;
                            SpeedInfo speed_info = thisSpeedpost.this_lim_speed(SignalFunction.Speed);

                            if ((isFreight && speed_info.FreightSpeed > 0) || (!isFreight && speed_info.PassengerSpeed > 0))
                            {
                                if (offset == 0 || thisItem.SignalLocation > thisSection.Length - offset)
                                {
                                    endOfRoute = true;
                                    foundObject.Add(-(thisItem.Signal.Index));
                                }
                            }
                        }
                    }
                }

                // move to next section
                // follow active links if set, otherwise default links (=0)

                int nextIndex = -1;
                switch (thisSection.CircuitType)
                {
                    case TrackCircuitType.Crossover:
                        if (thisSection.Pins[inPinDirection, Location.NearEnd].Link == lastIndex)
                        {
                            nextIndex = thisSection.Pins[outPinDirection, Location.NearEnd].Link;
                            nextDirection = thisSection.Pins[outPinDirection, Location.NearEnd].Direction;
                        }
                        else if (thisSection.Pins[inPinDirection, Location.FarEnd].Link == lastIndex)
                        {
                            nextIndex = thisSection.Pins[outPinDirection, Location.FarEnd].Link;
                            nextDirection = thisSection.Pins[outPinDirection, Location.FarEnd].Direction;
                        }
                        break;

                    case TrackCircuitType.Junction:
                        //                        if (checkReenterOriginalRoute && foundItems.Count > 2)
                        if (checkReenterOriginalRoute)
                        {
                            Train.TCSubpathRoute originalSubpath = thisTrain.TCRoute.TCRouteSubpaths[thisTrain.TCRoute.OriginalSubpath];
                            if (outPinDirection == 0)
                            {
                                // loop on original route to check if we are re-entering it
                                for (int routeIndex = 0; routeIndex < originalSubpath.Count; routeIndex++)
                                {
                                    if (thisIndex == originalSubpath[routeIndex].TCSectionIndex)
                                    // nice, we are returning into the original route
                                    {
                                        endOfRoute = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (thisSection.ActivePins[outPinDirection, Location.NearEnd].Link > 0)
                        {
                            nextIndex = thisSection.ActivePins[outPinDirection, Location.NearEnd].Link;
                            nextDirection = thisSection.ActivePins[outPinDirection, Location.NearEnd].Direction;
                        }
                        else if (thisSection.ActivePins[outPinDirection, Location.FarEnd].Link > 0)
                        {
                            nextIndex = thisSection.ActivePins[outPinDirection, Location.FarEnd].Link;
                            nextDirection = thisSection.ActivePins[outPinDirection, Location.FarEnd].Direction;
                        }
                        else if (honourManualSwitch && thisSection.JunctionSetManual >= 0)
                        {
                            nextIndex = thisSection.Pins[outPinDirection, (Location)thisSection.JunctionSetManual].Link;
                            nextDirection = thisSection.Pins[outPinDirection, (Location)thisSection.JunctionSetManual].Direction;
                        }
                        else if (!reservedOnly)
                        {
                            nextIndex = thisSection.Pins[outPinDirection, (Location)thisSection.JunctionLastRoute].Link;
                            nextDirection = thisSection.Pins[outPinDirection, (Location)thisSection.JunctionLastRoute].Direction;
                        }
                        break;

                    case TrackCircuitType.EndOfTrack:
                        break;

                    default:
                        nextIndex = thisSection.Pins[outPinDirection, Location.NearEnd].Link;
                        nextDirection = thisSection.Pins[outPinDirection, Location.NearEnd].Direction;

                        TrackCircuitSection nextSection = TrackCircuitList[nextIndex];

                        // if next section is junction : check if locked against AI and if auto-alignment allowed
                        // switchable end of switch is always pin direction 1
                        if (nextSection.CircuitType == TrackCircuitType.Junction)
                        {
                            TrackDirection nextPinDirection = nextDirection.Next();
                            int nextPinIndex = nextSection.Pins[nextPinDirection, Location.NearEnd].Link == thisIndex ? 0 : 1;
                            if (nextPinDirection == TrackDirection.Reverse && nextSection.JunctionLastRoute != nextPinIndex)
                            {
                                //TODO 20201027 to be verified, nextSection.AILock had never been set, thus removed
                                //if (nextSection.AILock && thisTrain != null && (thisTrain.TrainType == Train.TRAINTYPE.AI
                                //    || thisTrain.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING))
                                //{
                                //    endOfRoute = true;
                                //}

                                if (!autoAlign)
                                {
                                    endOfRoute = true;
                                }
                            }
                        }

                        break;
                }

                if (nextIndex < 0)
                {
                    endOfRoute = true;
                }
                else
                {
                    lastIndex = thisIndex;
                    thisIndex = nextIndex;
                    thisSection = TrackCircuitList[thisIndex];
                    curDirection = forward ? nextDirection : nextDirection.Next();
                    oppDirection = curDirection.Next();

                    if (searchBackwardSignal && thisSection.EndSignals[oppDirection] != null)
                    {
                        endOfRoute = true;
                        foundObject.Add(-(thisSection.EndSignals[oppDirection].Index));
                    }
                }

                if (!endOfRoute)
                {
                    offset = 0.0f;

                    if (thisTrain != null && reservedOnly)
                    {
                        TrackCircuitState thisState = thisSection.CircuitState;

                        if (!thisState.OccupationState.ContainsTrain(thisTrain) &&
                            (thisState.TrainReserved != null && thisState.TrainReserved.Train != thisTrain))
                        {
                            endOfRoute = true;
                        }
                    }
                }

                if (!endOfRoute && routeLength > 0)
                {
                    endOfRoute = (coveredLength > routeLength);
                    coveredLength += thisSection.Length;
                }

            }

            if (returnSections)
            {
                return (foundItems);
            }
            else
            {
                return (foundObject);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process Platforms
        /// </summary>

        private void ProcessPlatforms(Dictionary<int, int> platformList, TrackItem[] TrItems,
                TrackNode[] trackNodes, Dictionary<int, uint> platformSidesList)
        {
            foreach (KeyValuePair<int, int> thisPlatformIndex in platformList)
            {
                int thisPlatformDetailsIndex;
                uint thisPlatformData;

                // get platform item

                int thisIndex = thisPlatformIndex.Key;

                var thisPlatform = TrItems[thisIndex] is PlatformItem ? (PlatformItem)TrItems[thisIndex] : new PlatformItem((SidingItem)TrItems[thisIndex]);

                TrackNode thisNode = trackNodes[thisPlatformIndex.Value];

                // check if entry already created for related entry

                int relatedIndex = (int)thisPlatform.LinkedPlatformItemId;

                PlatformDetails thisDetails;
                Location refIndex;
                bool splitPlatform = false;

                // get related platform details

                if (PlatformXRefList.ContainsKey(relatedIndex))
                {
                    thisPlatformDetailsIndex = PlatformXRefList[relatedIndex];
                    thisDetails = PlatformDetailsList[thisPlatformDetailsIndex];
                    PlatformXRefList.Add(thisIndex, thisPlatformDetailsIndex);
                    refIndex = Location.FarEnd;
                }

                // create new platform details

                else
                {
                    thisDetails = new PlatformDetails(thisIndex);
                    PlatformDetailsList.Add(thisDetails);
                    thisPlatformDetailsIndex = PlatformDetailsList.Count - 1;
                    PlatformXRefList.Add(thisIndex, thisPlatformDetailsIndex);
                    refIndex = Location.NearEnd;
                }

                // set station reference
                if (StationXRefList.ContainsKey(thisPlatform.Station))
                {
                    List<int> XRefList = StationXRefList[thisPlatform.Station];
                    XRefList.Add(thisPlatformDetailsIndex);
                }
                else
                {
                    List<int> XRefList = new List<int>();
                    XRefList.Add(thisPlatformDetailsIndex);
                    StationXRefList.Add(thisPlatform.Station, XRefList);
                }

                // get tracksection

                int TCSectionIndex = -1;
                int TCXRefIndex = -1;

                for (int iXRef = thisNode.TrackCircuitCrossReferences.Count - 1; iXRef >= 0 && TCSectionIndex < 0; iXRef--)
                {
                    if (thisPlatform.SData1 <
                     (thisNode.TrackCircuitCrossReferences[iXRef].OffsetLength[1] + thisNode.TrackCircuitCrossReferences[iXRef].Length))
                    {
                        TCSectionIndex = thisNode.TrackCircuitCrossReferences[iXRef].Index;
                        TCXRefIndex = iXRef;
                    }
                }

                if (TCSectionIndex < 0)
                {
                    Trace.TraceInformation("Cannot locate TCSection for platform {0}", thisIndex);
                    TCSectionIndex = thisNode.TrackCircuitCrossReferences[0].Index;
                    TCXRefIndex = 0;
                }

                // if first entry, set tracksection

                if (refIndex == Location.NearEnd)
                {
                    thisDetails.TCSectionIndex.Add(TCSectionIndex);
                }

                // if second entry, test if equal - if not, build list

                else
                {
                    if (TCSectionIndex != thisDetails.TCSectionIndex[0])
                    {
                        int firstXRef = -1;
                        for (int iXRef = thisNode.TrackCircuitCrossReferences.Count - 1; iXRef >= 0 && firstXRef < 0; iXRef--)
                        {
                            if (thisNode.TrackCircuitCrossReferences[iXRef].Index == thisDetails.TCSectionIndex[0])
                            {
                                firstXRef = iXRef;
                            }
                        }

                        if (firstXRef < 0)  // platform is split by junction !!!
                        {
                            ResolveSplitPlatform(ref thisDetails, TCSectionIndex, thisPlatform, thisNode as TrackVectorNode, TrItems, trackNodes);
                            splitPlatform = true;
                            Trace.TraceInformation("Platform split by junction at " + thisDetails.Name);
                        }
                        else if (TCXRefIndex < firstXRef)
                        {
                            thisDetails.TCSectionIndex.Clear();
                            for (int iXRef = TCXRefIndex; iXRef <= firstXRef; iXRef++)
                            {
                                thisDetails.TCSectionIndex.Add(thisNode.TrackCircuitCrossReferences[iXRef].Index);
                            }
                        }
                        else
                        {
                            thisDetails.TCSectionIndex.Clear();
                            for (int iXRef = firstXRef; iXRef <= TCXRefIndex; iXRef++)
                            {
                                thisDetails.TCSectionIndex.Add(thisNode.TrackCircuitCrossReferences[iXRef].Index);
                            }
                        }
                    }
                }

                // set details (if not split platform)

                if (!splitPlatform)
                {
                    TrackCircuitSection thisSection = TrackCircuitList[TCSectionIndex];

                    thisDetails.PlatformReference[refIndex] = thisIndex;
                    thisDetails.NodeOffset[refIndex] = thisPlatform.SData1;
                    thisDetails.TrackCircuitOffset[refIndex, TrackDirection.Reverse] = thisPlatform.SData1 - thisSection.OffsetLength[Location.FarEnd];
                    thisDetails.TrackCircuitOffset[refIndex.Next(), TrackDirection.Ahead] = thisSection.Length - thisDetails.TrackCircuitOffset[refIndex, TrackDirection.Reverse];
                    if (thisPlatform.Flags1 == "ffff0000" || thisPlatform.Flags1 == "FFFF0000") thisDetails.PlatformFrontUiD = thisIndex;        // used to define 
                }

                if (refIndex == 0)
                {
                    thisDetails.Name = thisPlatform.Station;
                    thisDetails.MinWaitingTime = thisPlatform.PlatformMinWaitingTime;
                    thisDetails.NumPassengersWaiting = (int)thisPlatform.PlatformNumPassengersWaiting;
                }
                else if (!splitPlatform)
                {
                    thisDetails.Length = Math.Abs(thisDetails.NodeOffset[Location.FarEnd] - thisDetails.NodeOffset[Location.NearEnd]);
                }

                if (platformSidesList.TryGetValue(thisIndex, out thisPlatformData))
                {
                    if (((uint)PlatformDataFlag.PlatformLeft & thisPlatformData) != 0)
                        thisDetails.PlatformSide |= PlatformDetails.PlatformSides.Left;
                    if (((uint)PlatformDataFlag.PlatformRight & thisPlatformData) != 0)
                        thisDetails.PlatformSide |= PlatformDetails.PlatformSides.Right;
                }

                // check if direction correct, else swap 0 - 1 entries for offsets etc.

                if (refIndex == Location.FarEnd && thisDetails.NodeOffset[Location.FarEnd] < thisDetails.NodeOffset[Location.NearEnd] && !splitPlatform)
                {
                    float tf;
                    tf = thisDetails.NodeOffset[0];
                    thisDetails.NodeOffset[Location.NearEnd] = thisDetails.NodeOffset[Location.FarEnd];
                    thisDetails.NodeOffset[Location.FarEnd] = tf;

                    foreach (Location location in EnumExtension.GetValues<Location>())
                    {
                        tf = thisDetails.TrackCircuitOffset[location, TrackDirection.Ahead];
                        thisDetails.TrackCircuitOffset[location, TrackDirection.Ahead] = thisDetails.TrackCircuitOffset[location, TrackDirection.Reverse];
                        thisDetails.TrackCircuitOffset[location, TrackDirection.Reverse] = tf;
                    }
                }

                // search for end signals

                thisNode = trackNodes[TrackCircuitList[thisDetails.TCSectionIndex[0]].OriginalIndex];

                if (refIndex == Location.FarEnd)
                {
                    float distToSignal = 0.0f;
                    float offset = thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Ahead];
                    int lastSection = thisDetails.TCSectionIndex[thisDetails.TCSectionIndex.Count - 1];
                    int lastSectionXRef = -1;

                    for (int iXRef = 0; iXRef < thisNode.TrackCircuitCrossReferences.Count; iXRef++)
                    {
                        if (lastSection == thisNode.TrackCircuitCrossReferences[iXRef].Index)
                        {
                            lastSectionXRef = iXRef;
                            break;
                        }
                    }

                    for (int iXRef = lastSectionXRef; iXRef < thisNode.TrackCircuitCrossReferences.Count; iXRef++)
                    {
                        int sectionIndex = thisNode.TrackCircuitCrossReferences[iXRef].Index;
                        TrackCircuitSection thisSection = TrackCircuitList[sectionIndex];

                        distToSignal += thisSection.Length - offset;
                        offset = 0.0f;

                        if (thisSection.EndSignals[TrackDirection.Ahead] != null)
                        {
                            // end signal is always valid in timetable mode
                            if (Simulator.TimetableMode || distToSignal <= 150)
                            {
                                thisDetails.EndSignals[TrackDirection.Ahead] = thisSection.EndSignals[TrackDirection.Ahead].Index;
                                thisDetails.DistanceToSignals[TrackDirection.Ahead] = distToSignal;
                            }
                            // end signal is only valid if it has no fixed route in activity mode
                            else
                            {
                                float? approachControlLimitPositionM = null;
                                if (distToSignal > 150)
                                {
                                    foreach (SignalHead signalHead in thisSection.EndSignals[TrackDirection.Ahead].SignalHeads)
                                    {
                                        if (signalHead.ApproachControlLimitPositionM != null) approachControlLimitPositionM = signalHead.ApproachControlLimitPositionM;
                                    }
                                }
                                if (!thisSection.EndSignals[TrackDirection.Ahead].FixedRoute && !(approachControlLimitPositionM != null && (float)approachControlLimitPositionM < distToSignal + 100))
                                {
                                    thisDetails.EndSignals[TrackDirection.Ahead] = thisSection.EndSignals[TrackDirection.Ahead].Index;
                                    thisDetails.DistanceToSignals[TrackDirection.Ahead] = distToSignal;
                                }
                            }
                            break;
                        }
                    }

                    distToSignal = 0.0f;
                    offset = thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Reverse];
                    int firstSection = thisDetails.TCSectionIndex[0];
                    int firstSectionXRef = lastSectionXRef;

                    if (lastSection != firstSection)
                    {
                        for (int iXRef = 0; iXRef < thisNode.TrackCircuitCrossReferences.Count; iXRef++)
                        {
                            if (firstSection == thisNode.TrackCircuitCrossReferences[iXRef].Index)
                            {
                                firstSectionXRef = iXRef;
                                break;
                            }
                        }
                    }

                    for (int iXRef = firstSectionXRef; iXRef >= 0; iXRef--)
                    {
                        int sectionIndex = thisNode.TrackCircuitCrossReferences[iXRef].Index;
                        TrackCircuitSection thisSection = TrackCircuitList[sectionIndex];

                        distToSignal += thisSection.Length - offset;
                        offset = 0.0f;

                        if (thisSection.EndSignals[TrackDirection.Reverse] != null)
                        {
                            if (Simulator.TimetableMode || distToSignal <= 150)
                            {
                                thisDetails.EndSignals[TrackDirection.Reverse] = thisSection.EndSignals[TrackDirection.Reverse].Index;
                                thisDetails.DistanceToSignals[TrackDirection.Reverse] = distToSignal;
                            }
                            else
                            {
                                float? approachControlLimitPositionM = null;
                                if (distToSignal > 150)
                                {
                                    foreach (SignalHead signalHead in thisSection.EndSignals[TrackDirection.Reverse].SignalHeads)
                                    {
                                        if (signalHead.ApproachControlLimitPositionM != null) approachControlLimitPositionM = signalHead.ApproachControlLimitPositionM;
                                    }
                                }
                                if (!thisSection.EndSignals[TrackDirection.Reverse].FixedRoute && !(approachControlLimitPositionM != null && (float)approachControlLimitPositionM < distToSignal + 100))
                                {
                                    thisDetails.EndSignals[TrackDirection.Reverse] = thisSection.EndSignals[TrackDirection.Reverse].Index;
                                    thisDetails.DistanceToSignals[TrackDirection.Reverse] = distToSignal;
                                }
                            }
                            break;
                        }

                    }
                }

                // set section crossreference


                if (refIndex == Location.FarEnd)
                {
                    foreach (int sectionIndex in thisDetails.TCSectionIndex)
                    {
                        TrackCircuitSection thisSection = TrackCircuitList[sectionIndex];
                        thisSection.PlatformIndices.Add(thisPlatformDetailsIndex);
                    }
                }
            }

            if (Simulator.Activity != null &&
                Simulator.Activity.Activity.PlatformWaitingPassengers != null)

            // Override .tdb NumPassengersWaiting info with .act NumPassengersWaiting info if any available
            {
                int overriddenPlatformDetailsIndex;
                foreach (PlatformData platformData in Simulator.Activity.Activity.PlatformWaitingPassengers)
                {
                    overriddenPlatformDetailsIndex = PlatformDetailsList.FindIndex(platformDetails => (platformDetails.PlatformReference[Location.NearEnd] == platformData.ID) || (platformDetails.PlatformReference[Location.FarEnd] == platformData.ID));
                    if (overriddenPlatformDetailsIndex >= 0) PlatformDetailsList[overriddenPlatformDetailsIndex].NumPassengersWaiting = platformData.PassengerCount;
                    else Trace.TraceWarning("Platform referenced in .act file with TrItemId {0} not present in .tdb file ", platformData.ID);
                }
            }

        }// ProcessPlatforms

        //================================================================================================//
        /// <summary>
        /// Resolve split platforms
        /// </summary>

        public void ResolveSplitPlatform(ref PlatformDetails thisDetails, int secondSectionIndex,
                PlatformItem secondPlatform, TrackVectorNode secondNode,
                    TrackItem[] TrItems, TrackNode[] trackNodes)
        {
            // get all positions related to tile of first platform item

            PlatformItem firstPlatform = (TrItems[thisDetails.PlatformReference[Location.NearEnd]] is PlatformItem) ?
                    (PlatformItem)TrItems[thisDetails.PlatformReference[Location.NearEnd]] :
                    new PlatformItem((SidingItem)TrItems[thisDetails.PlatformReference[Location.NearEnd]]);

            int firstSectionIndex = thisDetails.TCSectionIndex[0];
            TrackCircuitSection thisSection = TrackCircuitList[firstSectionIndex];
            TrackVectorNode firstNode = trackNodes[thisSection.OriginalIndex] as TrackVectorNode;

            // first platform
            int TileX1 = firstPlatform.Location.TileX;
            int TileZ1 = firstPlatform.Location.TileZ;
            float X1 = firstPlatform.Location.Location.X;
            float Z1 = firstPlatform.Location.Location.Z;

            ref readonly WorldLocation location1 = ref firstNode.TrackVectorSections[0].Location;
            // start node position
            int TS1TileX = location1.TileX;
            int TS1TileZ = location1.TileZ;
            float TS1X = location1.Location.X;
            float TS1Z = location1.Location.Z;

            float TS1Xc = TS1X + (TS1TileX - TileX1) * 2048;
            float TS1Zc = TS1Z + (TS1TileZ - TileZ1) * 2048;

            // second platform
            int TileX2 = secondPlatform.Location.TileX;
            int TileZ2 = secondPlatform.Location.TileZ;
            float X2 = secondPlatform.Location.Location.X;
            float Z2 = secondPlatform.Location.Location.Z;

            float X2c = X2 + (TileX2 - TileX1) * 2048;
            float Z2c = Z2 + (TileZ2 - TileZ1) * 2048;

            ref readonly WorldLocation location2 = ref secondNode.TrackVectorSections[0].Location;
            int TS2TileX = location2.TileX;
            int TS2TileZ = location2.TileZ;
            float TS2X = location2.Location.X;
            float TS2Z = location2.Location.Z;

            float TS2Xc = TS2X + (TS2TileX - TileX1) * 2048;
            float TS2Zc = TS2Z + (TS2TileZ - TileZ1) * 2048;

            // determine if 2nd platform is towards end or begin of tracknode - use largest delta for check

            float dXplatform = X2c - X1;
            float dXnode = TS1Xc - X1;
            float dZplatform = Z2c - Z1;
            float dZnode = TS1Zc - Z1;

            float dplatform = Math.Abs(dXplatform) > Math.Abs(dZplatform) ? dXplatform : dZplatform;
            float dnode = Math.Abs(dXplatform) > Math.Abs(dXplatform) ? dXnode : dZnode;  // use same delta direction!

            // if towards begin : build list of sections from start

            List<int> PlSections1 = new List<int>();
            bool reqSectionFound = false;
            float totalLength1 = 0;
            int direction1 = 0;

            if (Math.Sign(dplatform) == Math.Sign(dnode))
            {
                for (int iXRef = firstNode.TrackCircuitCrossReferences.Count - 1; iXRef >= 0 && !reqSectionFound; iXRef--)
                {
                    int thisIndex = firstNode.TrackCircuitCrossReferences[iXRef].Index;
                    PlSections1.Add(thisIndex);
                    totalLength1 += TrackCircuitList[thisIndex].Length;
                    reqSectionFound = (thisIndex == firstSectionIndex);
                }
                totalLength1 -= thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Ahead];  // correct for offset
            }
            else
            {
                for (int iXRef = 0; iXRef < firstNode.TrackCircuitCrossReferences.Count && !reqSectionFound; iXRef++)
                {
                    int thisIndex = firstNode.TrackCircuitCrossReferences[iXRef].Index;
                    PlSections1.Add(thisIndex);
                    totalLength1 += TrackCircuitList[thisIndex].Length;
                    reqSectionFound = (thisIndex == firstSectionIndex);
                    direction1 = 1;
                }
                totalLength1 -= thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Reverse];  // correct for offset
            }

            // determine if 1st platform is towards end or begin of tracknode - use largest delta for check

            dXplatform = X1 - X2c;
            dXnode = TS2Xc - X2c;
            dZplatform = Z1 - Z2c;
            dZnode = TS2Zc - Z2c;

            dplatform = Math.Abs(dXplatform) > Math.Abs(dZplatform) ? dXplatform : dZplatform;
            dnode = Math.Abs(dXplatform) > Math.Abs(dXplatform) ? dXnode : dZnode;  // use same delta direction!

            // if towards begin : build list of sections from start

            List<int> PlSections2 = new List<int>();
            reqSectionFound = false;
            float totalLength2 = 0;
            int direction2 = 0;

            if (Math.Sign(dplatform) == Math.Sign(dnode))
            {
                for (int iXRef = secondNode.TrackCircuitCrossReferences.Count - 1; iXRef >= 0 && !reqSectionFound; iXRef--)
                {
                    int thisIndex = secondNode.TrackCircuitCrossReferences[iXRef].Index;
                    PlSections2.Add(thisIndex);
                    totalLength2 += TrackCircuitList[thisIndex].Length;
                    reqSectionFound = (thisIndex == secondSectionIndex);
                }
                totalLength2 -= (TrackCircuitList[secondSectionIndex].Length - secondPlatform.SData1);
            }
            else
            {
                for (int iXRef = 0; iXRef < secondNode.TrackCircuitCrossReferences.Count && !reqSectionFound; iXRef++)
                {
                    int thisIndex = secondNode.TrackCircuitCrossReferences[iXRef].Index;
                    PlSections2.Add(thisIndex);
                    totalLength2 += TrackCircuitList[thisIndex].Length;
                    reqSectionFound = (thisIndex == secondSectionIndex);
                    direction2 = 1;
                }
                totalLength2 -= secondPlatform.SData1; // correct for offset
            }

            // use largest part

            thisDetails.TCSectionIndex.Clear();

            if (totalLength1 > totalLength2)
            {
                foreach (int thisIndex in PlSections1)
                {
                    thisDetails.TCSectionIndex.Add(thisIndex);
                }

                thisDetails.Length = totalLength1;

                if (direction1 == 0)
                {
                    thisDetails.NodeOffset[Location.NearEnd] = 0.0f;
                    thisDetails.NodeOffset[Location.FarEnd] = firstPlatform.SData1;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] = TrackCircuitList[PlSections1[PlSections1.Count - 1]].Length - totalLength1;
                    for (int iSection = 0; iSection < PlSections1.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] += TrackCircuitList[PlSections1[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Reverse] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Ahead] = TrackCircuitList[PlSections1[0]].Length;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Reverse] = firstPlatform.SData1;
                    for (int iSection = 0; iSection < PlSections1.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] -= TrackCircuitList[PlSections1[iSection]].Length;
                    }
                }
                else
                {
                    thisDetails.NodeOffset[Location.NearEnd] = firstPlatform.SData1;
                    thisDetails.NodeOffset[Location.FarEnd] = thisDetails.NodeOffset[Location.NearEnd] + totalLength1;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Reverse] = TrackCircuitList[PlSections1[0]].Length - totalLength1;
                    for (int iSection = 1; iSection < PlSections1.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] += TrackCircuitList[PlSections1[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Ahead] = totalLength1;
                    for (int iSection = 1; iSection < PlSections1.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] -= TrackCircuitList[PlSections1[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Reverse] = TrackCircuitList[PlSections1[PlSections1.Count - 1]].Length;
                }
            }
            else
            {
                foreach (int thisIndex in PlSections2)
                {
                    thisDetails.TCSectionIndex.Add(thisIndex);
                }

                thisDetails.Length = totalLength2;

                if (direction2 == 0)
                {
                    thisDetails.NodeOffset[Location.NearEnd] = 0.0f;
                    thisDetails.NodeOffset[Location.FarEnd] = secondPlatform.SData1;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] = TrackCircuitList[PlSections2.Count - 1].Length - totalLength2;
                    for (int iSection = 0; iSection < PlSections2.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] += TrackCircuitList[PlSections2[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Reverse] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Ahead] = TrackCircuitList[PlSections2[0]].Length;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Reverse] = secondPlatform.SData1;
                    for (int iSection = 0; iSection < PlSections2.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] -= TrackCircuitList[PlSections2[iSection]].Length;
                    }
                }
                else
                {
                    thisDetails.NodeOffset[Location.NearEnd] = secondPlatform.SData1;
                    thisDetails.NodeOffset[Location.FarEnd] = thisDetails.NodeOffset[0] + totalLength2;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Reverse] = TrackCircuitList[PlSections2[0]].Length - totalLength2;
                    for (int iSection = 1; iSection < PlSections2.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] += TrackCircuitList[PlSections2[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Ahead] = totalLength2;
                    for (int iSection = 1; iSection < PlSections2.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, TrackDirection.Ahead] -= TrackCircuitList[PlSections2[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, TrackDirection.Reverse] = TrackCircuitList[PlSections2[PlSections2.Count - 1]].Length;
                }
            }
        }


        //================================================================================================//
        /// <summary>
        /// Remove all deadlock path references for specified train
        /// </summary>

        public void RemoveDeadlockPathReferences(int trainnumber)
        {
            foreach (KeyValuePair<int, DeadlockInfo> deadlockElement in DeadlockInfoList)
            {
                DeadlockInfo deadlockInfo = deadlockElement.Value;
                if (deadlockInfo.TrainSubpathIndex.ContainsKey(trainnumber))
                {
                    Dictionary<int, int> subpathRef = deadlockInfo.TrainSubpathIndex[trainnumber];
                    foreach (KeyValuePair<int, int> pathRef in subpathRef)
                    {
                        int routeIndex = pathRef.Value;
                        List<int> pathReferences = deadlockInfo.TrainReferences[routeIndex];
                        foreach (int pathReference in pathReferences)
                        {
                            deadlockInfo.AvailablePathList[pathReference].AllowedTrains.Remove(trainnumber);
                        }
                        deadlockInfo.TrainReferences.Remove(routeIndex);
                        deadlockInfo.TrainOwnPath.Remove(routeIndex);
                        deadlockInfo.TrainLengthFit.Remove(routeIndex);
                    }
                    deadlockInfo.TrainSubpathIndex.Remove(trainnumber);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reallocate all deadlock path references for specified train when train forms new train
        /// </summary>

        public void ReallocateDeadlockPathReferences(int oldnumber, int newnumber)
        {
            foreach (KeyValuePair<int, DeadlockInfo> deadlockElement in DeadlockInfoList)
            {
                DeadlockInfo deadlockInfo = deadlockElement.Value;
                if (deadlockInfo.TrainSubpathIndex.ContainsKey(oldnumber))
                {
                    Dictionary<int, int> subpathRef = deadlockInfo.TrainSubpathIndex[oldnumber];
                    foreach (KeyValuePair<int, int> pathRef in subpathRef)
                    {
                        int routeIndex = pathRef.Value;
                        List<int> pathReferences = deadlockInfo.TrainReferences[routeIndex];
                        foreach (int pathReference in pathReferences)
                        {
                            deadlockInfo.AvailablePathList[pathReference].AllowedTrains.Remove(oldnumber);
                            deadlockInfo.AvailablePathList[pathReference].AllowedTrains.Add(newnumber);
                        }
                    }
                    deadlockInfo.TrainSubpathIndex.Add(newnumber, subpathRef);
                    deadlockInfo.TrainSubpathIndex.Remove(oldnumber);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// ProcessTunnels
        /// Process tunnel sections and add info to TrackCircuitSections
        /// </summary>

        public void ProcessTunnels()
        {
            // loop through tracknodes
            foreach (TrackNode thisNode in trackDB.TrackNodes)
            {
                if (thisNode is TrackVectorNode tvn)
                {
                    bool inTunnel = false;
                    List<float[]> tunnelInfo = new List<float[]>();
                    List<int> tunnelPaths = new List<int>();
                    float[] lastTunnel = null;
                    float totalLength = 0f;
                    int numPaths = -1;

                    // loop through all sections in node
                    foreach (TrackVectorSection thisSection in tvn.TrackVectorSections)
                    {
                        if (!tsectiondat.TrackSections.ContainsKey(thisSection.SectionIndex))
                        {
                            continue;  // missing track section
                        }

                        float thisLength = 0f;
                        TrackSection TS = tsectiondat.TrackSections[thisSection.SectionIndex];

                        // determine length
                        if (TS.Curved)
                        {
                            thisLength = MathHelper.ToRadians(Math.Abs(TS.Angle)) * TS.Radius;
                        }
                        else
                        {
                            thisLength = TS.Length;

                        }

                        // check tunnel shape

                        bool tunnelShape = false;
                        int shapePaths = 0;

                        if (tsectiondat.TrackShapes.ContainsKey(thisSection.ShapeIndex))
                        {
                            TrackShape thisShape = tsectiondat.TrackShapes[thisSection.ShapeIndex];
                            tunnelShape = thisShape.TunnelShape;
                            shapePaths = Convert.ToInt32(thisShape.PathsNumber);
                        }

                        if (tunnelShape)
                        {
                            numPaths = numPaths < 0 ? shapePaths : Math.Min(numPaths, shapePaths);
                            if (inTunnel)
                            {
                                lastTunnel[1] += thisLength;
                            }
                            else
                            {
                                lastTunnel = new float[2];
                                lastTunnel[0] = totalLength;
                                lastTunnel[1] = thisLength;
                                inTunnel = true;
                            }
                        }
                        else if (inTunnel)
                        {
                            tunnelInfo.Add(lastTunnel);
                            tunnelPaths.Add(numPaths);
                            inTunnel = false;
                            numPaths = -1;
                        }
                        totalLength += thisLength;
                    }

                    // add last tunnel item
                    if (inTunnel)
                    {
                        tunnelInfo.Add(lastTunnel);
                        tunnelPaths.Add(numPaths);
                    }

                    // add tunnel info to TrackCircuitSections

                    if (tunnelInfo.Count > 0)
                    {
                        bool TCSInTunnel = false;
                        float[] tunnelData = tunnelInfo[0];
                        float processedLength = 0;

                        for (int iXRef = thisNode.TrackCircuitCrossReferences.Count - 1; iXRef >= 0; iXRef--)
                        {
                            TrackCircuitSectionCrossReference TCSXRef = thisNode.TrackCircuitCrossReferences[iXRef];
                            // forward direction
                            float TCSStartOffset = TCSXRef.OffsetLength[1];
                            float TCSLength = TCSXRef.Length;
                            TrackCircuitSection thisTCS = TrackCircuitList[TCSXRef.Index];
                            float startOffset;

                            // if tunnel starts in TCS
                            while (tunnelData != null && tunnelData[0] <= (TCSStartOffset + TCSLength))
                            {
                                float tunnelStart = 0;
                                float sectionTunnelStart;

                                // if in tunnel, set start in tunnel and check end
                                if (TCSInTunnel)
                                {
                                    sectionTunnelStart = -1;
                                    startOffset = processedLength;
                                }
                                else
                                // else start new tunnel
                                {
                                    sectionTunnelStart = tunnelData[0] - TCSStartOffset;
                                    tunnelStart = sectionTunnelStart;
                                    startOffset = -1;
                                }

                                if ((TCSStartOffset + TCSLength) >= (tunnelData[0] + tunnelData[1]))  // tunnel end is in this section
                                {
                                    TCSInTunnel = false;

                                    processedLength = 0;

                                    thisTCS.AddTunnelData(new TunnelInfoData(tunnelPaths[0], sectionTunnelStart, tunnelStart + tunnelData[1] - processedLength, tunnelData[1] - processedLength, tunnelData[1], thisTCS.Length, startOffset));

                                    if (tunnelInfo.Count >= 2)
                                    {
                                        tunnelInfo.RemoveAt(0);
                                        tunnelData = tunnelInfo[0];
                                        tunnelPaths.RemoveAt(0);
                                    }
                                    else
                                    {
                                        tunnelData = null;
                                        break;  // no more tunnels to process
                                    }
                                }
                                else
                                {
                                    TCSInTunnel = true;

                                    processedLength += (TCSLength - tunnelStart);

                                    thisTCS.AddTunnelData(new TunnelInfoData(tunnelPaths[0], sectionTunnelStart, -1, TCSLength - tunnelStart, tunnelData[1], thisTCS.Length, startOffset));
                                    break;  // cannot add more tunnels to section
                                }
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// ProcessTroughs
        /// Process trough sections and add info to TrackCircuitSections
        /// </summary>

        public void ProcessTroughs()
        {
            // loop through tracknodes
            foreach (TrackNode thisNode in trackDB.TrackNodes)
            {
                if (thisNode is TrackVectorNode tvn)
                {
                    bool overTrough = false;
                    List<float[]> troughInfo = new List<float[]>();
                    List<int> troughPaths = new List<int>();
                    float[] lastTrough = null;
                    float totalLength = 0f;
                    int numPaths = -1;

                    // loop through all sections in node
                    foreach (TrackVectorSection thisSection in tvn.TrackVectorSections)
                    {
                        if (!tsectiondat.TrackSections.ContainsKey(thisSection.SectionIndex))
                        {
                            continue;  // missing track section
                        }

                        float thisLength = 0f;
                        TrackSection TS = tsectiondat.TrackSections[thisSection.SectionIndex];

                        // determine length
                        if (TS.Curved)
                        {
                            thisLength = MathHelper.ToRadians(Math.Abs(TS.Angle)) * TS.Radius;
                        }
                        else
                        {
                            thisLength = TS.Length;

                        }

                        // check trough shape

                        bool troughShape = false;
                        int shapePaths = 0;

                        if (tsectiondat.TrackShapes.ContainsKey(thisSection.ShapeIndex))
                        {
                            TrackShape thisShape = tsectiondat.TrackShapes[thisSection.ShapeIndex];
                            if (thisShape.FileName != null)
                            {
                                troughShape = thisShape.FileName.EndsWith("Wtr.s") || thisShape.FileName.EndsWith("wtr.s");
                                shapePaths = Convert.ToInt32(thisShape.PathsNumber);
                            }
                        }

                        if (troughShape)
                        {
                            numPaths = numPaths < 0 ? shapePaths : Math.Min(numPaths, shapePaths);
                            if (overTrough)
                            {
                                lastTrough[1] += thisLength;
                            }
                            else
                            {
                                lastTrough = new float[2];
                                lastTrough[0] = totalLength;
                                lastTrough[1] = thisLength;
                                overTrough = true;
                            }
                        }
                        else if (overTrough)
                        {
                            troughInfo.Add(lastTrough);
                            troughPaths.Add(numPaths);
                            overTrough = false;
                            numPaths = -1;
                        }
                        totalLength += thisLength;
                    }

                    // add last tunnel item
                    if (overTrough)
                    {
                        troughInfo.Add(lastTrough);
                        troughPaths.Add(numPaths);
                    }

                    // add tunnel info to TrackCircuitSections

                    if (troughInfo.Count > 0)
                    {
                        bool TCSOverTrough = false;
                        float[] troughData = troughInfo[0];
                        float processedLength = 0;

                        for (int iXRef = thisNode.TrackCircuitCrossReferences.Count - 1; iXRef >= 0; iXRef--)
                        {
                            TrackCircuitSectionCrossReference TCSXRef = thisNode.TrackCircuitCrossReferences[iXRef];
                            // forward direction
                            float TCSStartOffset = TCSXRef.OffsetLength[1];
                            float TCSLength = TCSXRef.Length;
                            TrackCircuitSection thisTCS = TrackCircuitList[TCSXRef.Index];

                            // if trough starts in TCS
                            while (troughData != null && troughData[0] <= (TCSStartOffset + TCSLength))
                            {
                                float troughStart = 0;
                                float sectionTroughStart;
                                float startOffset;

                                // if in trough, set start in trough and check end
                                if (TCSOverTrough)
                                {
                                    sectionTroughStart = -1;
                                    startOffset = processedLength;
                                }
                                else
                                // else start new trough
                                {
                                    sectionTroughStart = troughData[0] - TCSStartOffset;
                                    troughStart = sectionTroughStart;
                                    startOffset = -1;
                                }

                                if ((TCSStartOffset + TCSLength) >= (troughData[0] + troughData[1]))  // trough end is in this section
                                {
                                    TCSOverTrough = false;
                                    processedLength = 0;

                                    thisTCS.AddTroughData(new TroughInfoData(sectionTroughStart, troughStart + troughData[1] - processedLength, troughData[1] - processedLength, troughData[1], thisTCS.Length, startOffset));

                                    if (troughInfo.Count >= 2)
                                    {
                                        troughInfo.RemoveAt(0);
                                        troughData = troughInfo[0];
                                        troughPaths.RemoveAt(0);
                                    }
                                    else
                                    {
                                        troughData = null;
                                        break;  // no more troughs to process
                                    }
                                }
                                else
                                {
                                    TCSOverTrough = true;

                                    processedLength += (TCSLength - troughStart);

                                    thisTCS.AddTroughData(new TroughInfoData(sectionTroughStart, -1, TCSLength - troughStart, troughData[1], thisTCS.Length, startOffset));
                                    break;  // cannot add more troughs to section
                                }
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Find Train
        /// Find train in list using number, to restore reference after restore
        /// </summary>

        public static Train FindTrain(int number, List<Train> trains)
        {
            foreach (Train thisTrain in trains)
            {
                if (thisTrain.Number == number)
                {
                    return (thisTrain);
                }
            }

            return (null);
        }

        //================================================================================================//
        /// <summary>
        /// Request set switch
        /// Manual request to set switch, either from train or direct from node
        /// </summary>

        public static bool RequestSetSwitch(Train thisTrain, Direction direction)
        {
            if (thisTrain.ControlMode == Train.TRAIN_CONTROL.MANUAL)
            {
                return (thisTrain.ProcessRequestManualSetSwitch(direction));
            }
            else if (thisTrain.ControlMode == Train.TRAIN_CONTROL.EXPLORER)
            {
                return (thisTrain.ProcessRequestExplorerSetSwitch(direction));
            }
            return (false);
        }

        public bool RequestSetSwitch(TrackNode switchNode)
        {
            return RequestSetSwitch(switchNode.TrackCircuitCrossReferences[0].Index);
        }

        public bool RequestSetSwitch(int trackCircuitIndex)
        {
            TrackCircuitSection switchSection = TrackCircuitList[trackCircuitIndex];
            Train thisTrain = switchSection.CircuitState.TrainReserved == null ? null : switchSection.CircuitState.TrainReserved.Train;
            bool switchReserved = (switchSection.CircuitState.SignalReserved >= 0 || switchSection.CircuitState.TrainClaimed.Count > 0);
            bool switchSet = false;

            // set physical state

            if (switchReserved)
            {
                switchSet = false;
            }

            else if (!switchSection.CircuitState.Occupied() && thisTrain == null)
            {
                switchSection.JunctionSetManual = switchSection.JunctionLastRoute == 0 ? 1 : 0;
                setSwitch(switchSection.OriginalIndex, switchSection.JunctionSetManual, switchSection);
                switchSet = true;
            }

            // if switch reserved by manual train then notify train

            else if (thisTrain != null && thisTrain.ControlMode == Train.TRAIN_CONTROL.MANUAL)
            {
                switchSection.JunctionSetManual = switchSection.JunctionLastRoute == 0 ? 1 : 0;
                switchSet = thisTrain.ProcessRequestManualSetSwitch(switchSection.Index);
            }
            else if (thisTrain != null && thisTrain.ControlMode == Train.TRAIN_CONTROL.EXPLORER)
            {
                switchSection.JunctionSetManual = switchSection.JunctionLastRoute == 0 ? 1 : 0;
                switchSet = thisTrain.ProcessRequestExplorerSetSwitch(switchSection.Index);
            }

            return (switchSet);
        }

        //only used by MP to manually set a switch to a desired position
        public bool RequestSetSwitch(TrackJunctionNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            bool switchReserved = (switchSection.CircuitState.SignalReserved >= 0 || switchSection.CircuitState.TrainClaimed.Count > 0);
            bool switchSet = false;

            // It must be possible to force a switch also in its present state, not only in the opposite state
            if (!MPManager.IsServer())
                if (switchReserved) return (false);
            //this should not be enforced in MP, as a train may need to be allowed to go out of the station from the side line

            if (!switchSection.CircuitState.Occupied())
            {
                switchSection.JunctionSetManual = desiredState;
                (trackDB.TrackNodes[switchSection.OriginalIndex] as TrackJunctionNode).SelectedRoute = switchSection.JunctionSetManual;
                switchSection.JunctionLastRoute = switchSection.JunctionSetManual;
                switchSet = true;

                if (!Simulator.TimetableMode) switchSection.CircuitState.Forced = true;

                foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
                {
                    Signal thisSignal = SignalObjects[thisSignalIndex];
                    thisSignal.Update();
                }

                var temptrains = Simulator.Trains.ToArray();

                foreach (var t in temptrains)
                {
                    if (t.TrainType != Train.TRAINTYPE.STATIC)
                    {
                        try
                        {
                            if (t.ControlMode != Train.TRAIN_CONTROL.AUTO_NODE && t.ControlMode != Train.TRAIN_CONTROL.AUTO_SIGNAL)
                                t.ProcessRequestExplorerSetSwitch(switchSection.Index);
                            else
                                t.ProcessRequestAutoSetSwitch(switchSection.Index);
                        }

                        catch
                        {
                        }
                    }
                }
            }
            return (switchSet);
        }

        //================================================================================================//

    }// class Signals

}
