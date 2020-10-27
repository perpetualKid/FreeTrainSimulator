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
using System.Text;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Threading;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.MultiPlayer;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Signalling
{


    //================================================================================================//
    //================================================================================================//
    /// <summary>
    /// Class Signals
    /// </summary>
    public class Signals
    {

        //================================================================================================//
        // local data
        //================================================================================================//

        internal readonly Simulator Simulator;

        public TrackDB trackDB;
        private TrackSectionsFile tsectiondat;
        private TrackDatabaseFile tdbfile;

        private Signal[] signalObjects;
        private List<SignalWorldInfo> SignalWorldList = new List<SignalWorldInfo>();
        private Dictionary<uint, SignalReferenceInfo> SignalRefList;
        private Dictionary<uint, Signal> SignalHeadList;
        public static SIGSCRfile scrfile;
        public int ORTSSignalTypeCount { get; private set; }
        public IList<string> ORTSSignalTypes;

        public int noSignals;
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

        public Signals(Simulator simulator, SignalConfigurationFile sigcfg, CancellationToken cancellation)
        {
            Simulator = simulator;

#if DEBUG_REPORTS
            File.Delete(@"C:\temp\printproc.txt");
#endif

            SignalRefList = new Dictionary<uint, SignalReferenceInfo>();
            SignalHeadList = new Dictionary<uint, Signal>();
            Dictionary<int, int> platformList = new Dictionary<int, int>();

            ORTSSignalTypeCount = OrSignalTypes.Instance.FunctionTypes.Count;
            ORTSSignalTypes = OrSignalTypes.Instance.FunctionTypes;

            trackDB = simulator.TDB.TrackDB;
            tsectiondat = simulator.TSectionDat;
            tdbfile = Simulator.TDB;

            // read SIGSCR files

            Trace.Write(" SIGSCR ");
            scrfile = new SIGSCRfile(new SignalScripts(sigcfg.ScriptPath, sigcfg.ScriptFiles, sigcfg.SignalTypes));

            // build list of signal world file information

            BuildSignalWorld(simulator, sigcfg, cancellation);

            // build list of signals in TDB file

            BuildSignalList(trackDB.TrackItems, trackDB.TrackNodes, tsectiondat, tdbfile, platformList, MilepostList);

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

            if (SignalObjects != null)
            {
                foreach (Signal thisSignal in SignalObjects)
                {
                    if (thisSignal != null)
                    {
                        if (thisSignal.isSignalNormal())
                        {
                            if (thisSignal.TCNextTC < 0)
                            {
                                Trace.TraceInformation("Signal " + thisSignal.thisRef +
                                    " ; TC : " + thisSignal.TCReference +
                                    " ; NextTC : " + thisSignal.TCNextTC +
                                    " ; TN : " + thisSignal.trackNode + 
                                    " ; TDB (0) : " + thisSignal.SignalHeads[0].TDBIndex);
                            }

                            if (thisSignal.TCReference < 0) // signal is not on any track - remove it!
                            {
                                Trace.TraceInformation("Signal removed " + thisSignal.thisRef +
                                    " ; TC : " + thisSignal.TCReference +
                                    " ; NextTC : " + thisSignal.TCNextTC +
                                    " ; TN : " + thisSignal.trackNode +
                                    " ; TDB (0) : " + thisSignal.SignalHeads[0].TDBIndex);
                                SignalObjects[thisSignal.thisRef] = null;
                            }
                        }
                    }
                }
            }

            DeadlockInfoList = new Dictionary<int, DeadlockInfo>();
            deadlockIndex = 1;
            DeadlockReference = new Dictionary<int, int>();
        }

        //================================================================================================//
        /// <summary>
        /// Overlay constructor for restore after saved game
        /// </summary>

        public Signals(Simulator simulator, SignalConfigurationFile sigcfg, BinaryReader inf, CancellationToken cancellation)
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
                        outf.Write(thisSignal.thisRef);
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
        /// Gets an array of all the SignalObjects.
        /// </summary>

        public Signal[] SignalObjects
        {
            get
            {
                return signalObjects;
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
                    else  if (worldObject is PlatformObject platformObject)
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

                int totalSignal = signalObjects.Length - 1;

                int updatestep = (totalSignal / 20) + 1;
                if (preUpdate)
                {
                    updatestep = totalSignal;
                }

                for (int icount = updatecount; icount < Math.Min(totalSignal, updatecount + updatestep); icount++)
                {
                    Signal signal = signalObjects[icount];
                    if (signal != null && !signal.noupdate) // to cater for orphans, and skip signals which do not require updates
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

            noSignals = 0;
            if (TrItems == null)
                return;                // No track Objects in route.
            foreach (TrackItem trItem in TrItems)
            {
                if (trItem != null)
                {
                    if (trItem is SignalItem)
                    {
                        noSignals++;
                    }
                    else if (trItem is SpeedPostItem)
                    {
                        SpeedPostItem Speedpost = (SpeedPostItem)trItem;
                        if (Speedpost.IsLimit)
                        {
                            noSignals++;
                        }
                    }
                }
            }

            // set general items and create sections
            if (noSignals > 0)
            {
                signalObjects = new Signal[noSignals];
                Signal.signalObjects = signalObjects;
            }

            Signal.trackNodes = trackNodes;
            Signal.trItems = TrItems;

            for (int i = 1; i < trackNodes.Length; i++)
            {
                ScanSection(TrItems, trackNodes, i, tsectiondat, tdbfile, platformList, milepostList);
            }

            //  Only continue if one or more signals in route.

            if (noSignals > 0)
            {
                // using world cross-reference list, merge heads to single signal

                MergeHeads();

                // rebuild list - clear out null elements

                int firstfree = -1;
                for (int iSignal = 0; iSignal < SignalObjects.Length; iSignal++)
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
                    firstfree = SignalObjects.Length - 1;

                // restore all links and indices

                for (var iSignal = 0; iSignal < SignalObjects.Length; iSignal++)
                {
                    if (SignalObjects[iSignal] != null)
                    {
                        var thisObject = SignalObjects[iSignal];
                        thisObject.thisRef = iSignal;

                        foreach (var thisHead in thisObject.SignalHeads)
                        {
                            thisHead.ResetMain(thisObject);
                            var trackItem = TrItems[thisHead.TDBIndex];
                            var sigItem = trackItem as SignalItem;
                            var speedItem = trackItem as SpeedPostItem;
                            if (sigItem != null)
                            {
                                sigItem.SignalObject = thisObject.thisRef;
                            }
                            else if (speedItem != null)
                            {
                                speedItem.SignalObject = thisObject.thisRef;
                            }
                        }
                    }
                }

                foundSignals = firstfree;

            }
            else
            {
                signalObjects = new Signal[0];
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

            for (int isignal = 0; isignal < signalObjects.Length - 1; isignal++)
            {
                Signal singleSignal = signalObjects[isignal];
                if (singleSignal != null && singleSignal.isSignal && singleSignal.WorldObject?.Backfacing.Count > 0)
                {

                    //
                    // create new signal - copy of existing signal
                    // use Backfacing flags and reset head indication
                    //

                    Signal newSignal = new Signal(singleSignal);

                    newSignal.thisRef = newindex;
                    newSignal.signalRef = this;
                    newSignal.trRefIndex = 0;

                    newSignal.WorldObject.UpdateFlags(singleSignal.WorldObject.FlagsSetBackfacing);

                    for (int iindex = 0; iindex < newSignal.WorldObject.HeadsSet.Length; iindex++)
                    {
                        newSignal.WorldObject.HeadsSet[iindex] = false;
                    }

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
                            if (singleSignal.direction != thisItemOld.Direction)
                            {
                                singleSignal.direction = (int)thisItemOld.Direction;
                                singleSignal.tdbtraveller.ReverseDirection();                           // reverse //
                            }
                        }

                        SignalItem thisItemNew = TrItems[newSignal.SignalHeads[0].TDBIndex] as SignalItem;
                        if (newSignal.direction != thisItemNew.Direction)
                        {
                            newSignal.direction = (int)thisItemNew.Direction;
                            newSignal.tdbtraveller.ReverseDirection();                           // reverse //
                        }

                        //
                        // set correct trRefIndex for this signal, and set cross-reference for all backfacing trRef items
                        //

                        TrackVectorNode tvn = TrackNodes[newSignal.trackNode] as TrackVectorNode;
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
                                            sigItem.SignalObject = newSignal.thisRef;
                                            newSignal.trRefIndex = i;

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
                        tvn = TrackNodes[newSignal.trackNode] as TrackVectorNode;
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
                                            sigItem.SignalObject = singleSignal.thisRef;
                                            singleSignal.trRefIndex = i;

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
                            signalObjects[isignal] = null;
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
                signalObjects[newindex] = newSignal;
                newindex++;
            }

            foundSignals = newindex;
        }

        //================================================================================================//
        /// <summary>
        /// ScanSection : This method checks a section in the TDB for signals or speedposts
        /// </summary>

        private void ScanSection(TrackItem[] TrItems, TrackNode[] trackNodes, int index,
                               TrackSectionsFile tsectiondat, TrackDatabaseFile tdbfile, Dictionary<int, int> platformList, List <Milepost> milepostList)
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

                                lastSignal = AddSpeed(index, i, speedItem, TDBRef, tsectiondat, tdbfile, ORTSSignalTypeCount);
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
                                if (MainSignal.trackNode != AddSignal.trackNode)
                                {
                                    Trace.TraceWarning("Signal head {0} in different track node than signal head {1} of same signal", MainSignal.trItem, thisReference.Key);
                                    MainSignal = null;
                                    break;
                                }
                                foreach (SignalHead thisHead in AddSignal.SignalHeads)
                                {
                                    MainSignal.SignalHeads.Add(thisHead);
                                    SignalObjects[AddSignal.thisRef] = null;
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

            signalObjects[foundSignals] = new Signal(ORTSSignalTypeCount);
            signalObjects[foundSignals].isSignal = true;
            signalObjects[foundSignals].isSpeedSignal = false;
            signalObjects[foundSignals].direction = (int)sigItem.Direction;
            signalObjects[foundSignals].trackNode = trackNode;
            signalObjects[foundSignals].trRefIndex = nodeIndx;
            signalObjects[foundSignals].AddHead(nodeIndx, TDBRef, sigItem);
            signalObjects[foundSignals].thisRef = foundSignals;
            signalObjects[foundSignals].signalRef = this;

            if (!(tdbfile.TrackDB.TrackNodes[trackNode] is TrackVectorNode tvn))
            {
                validSignal = false;
                Trace.TraceInformation("Reference to invalid track node {0} for Signal {1}\n", trackNode, TDBRef);
            }
            else
            {
                signalObjects[foundSignals].tdbtraveller =
                new Traveller(tsectiondat, tdbfile.TrackDB.TrackNodes, tvn, sigItem.Location, (Traveller.TravellerDirection)(1 - sigItem.Direction));
            }

            signalObjects[foundSignals].WorldObject = null;

            if (SignalHeadList.ContainsKey((uint)TDBRef))
            {
                validSignal = false;
                Trace.TraceInformation("Invalid double TDBRef {0} in node {1}\n", TDBRef, trackNode);
            }

            if (!validSignal)
            {
                signalObjects[foundSignals] = null;  // reset signal, do not increase signal count
            }
            else
            {
                SignalHeadList.Add((uint)TDBRef, signalObjects[foundSignals]);
                foundSignals++;
            }

            return foundSignals - 1;
        } // AddSignal


        //================================================================================================//
        /// <summary>
        /// This method adds a new Speedpost to the list
        /// </summary>

        private int AddSpeed(int trackNode, int nodeIndx, SpeedPostItem speedItem, int TDBRef, TrackSectionsFile tsectiondat, TrackDatabaseFile tdbfile, int ORTSSignalTypeCount)
        {
            signalObjects[foundSignals] = new Signal(ORTSSignalTypeCount);
            signalObjects[foundSignals].isSignal = false;
            signalObjects[foundSignals].isSpeedSignal = false;
            signalObjects[foundSignals].direction = 0;                  // preset - direction not yet known //
            signalObjects[foundSignals].trackNode = trackNode;
            signalObjects[foundSignals].trRefIndex = nodeIndx;
            signalObjects[foundSignals].AddHead(nodeIndx, TDBRef, speedItem);
            signalObjects[foundSignals].thisRef = foundSignals;
            signalObjects[foundSignals].signalRef = this;

            signalObjects[foundSignals].tdbtraveller =
            new Traveller(tsectiondat, tdbfile.TrackDB.TrackNodes, tdbfile.TrackDB.TrackNodes[trackNode] as TrackVectorNode, speedItem.Location, (Traveller.TravellerDirection)signalObjects[foundSignals].direction);

            double delta_angle = signalObjects[foundSignals].tdbtraveller.RotY - ((Math.PI / 2) - speedItem.Angle);
            float delta_float = MathHelper.WrapAngle((float)delta_angle);
            if (Math.Abs(delta_float) < (Math.PI / 2))
            {
                signalObjects[foundSignals].direction = signalObjects[foundSignals].tdbtraveller.Direction == 0 ? 1 : 0;
            }
            else
            {
                signalObjects[foundSignals].direction = (int)signalObjects[foundSignals].tdbtraveller.Direction;
                signalObjects[foundSignals].tdbtraveller.ReverseDirection();
            }

#if DEBUG_PRINT
            File.AppendAllText(@"C:\temp\speedpost.txt",
				String.Format("\nPlaced : at : {0} {1}:{2} {3}; angle - track : {4}:{5}; delta : {6}; dir : {7}\n",
				speedItem.TileX, speedItem.TileZ, speedItem.X, speedItem.Z,
				speedItem.Angle, signalObjects[foundSignals].tdbtraveller.RotY,
				delta_angle,
				signalObjects[foundSignals].direction));
#endif

            signalObjects[foundSignals].WorldObject = null;
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

        private void AddCFG(SignalConfigurationFile sigCFG)
        {
            foreach (Signal signal in signalObjects)
            {
                if (signal != null)
                {
                    if (signal.isSignal)
                    {
                        signal.SetSignalType(sigCFG);
                    }
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

            foreach (Signal signal in signalObjects)
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
            foreach (var signal in signalObjects)
                if (signal != null)
                    foreach (var head in signal.SignalHeads)
                        if ((Signal.trackNodes[signal.trackNode] as TrackVectorNode).TrackItemIndices[head.TrackItemIndex] == (int)trItem)
                            return new KeyValuePair<Signal, SignalHead>(signal, head);
            return null;
        }//FindByTrItem

        //================================================================================================//
        /// <summary>
        /// Count number of normal signal heads
        /// </summary>

        public void SetNumSignalHeads()
        {
            foreach (Signal thisSignal in signalObjects)
            {
                if (thisSignal != null)
                {
                    foreach (SignalHead thisHead in thisSignal.SignalHeads)
                    {
                        if (thisHead.SignalFunction == SignalFunction.Normal)
                        {
                            thisSignal.SignalNumNormalHeads++;
                        }
                    }
                }
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
            Heading actDirection = (Heading)thisElement.Direction;
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
                            else if (thisSpeedpost.Signal.isSpeedSignal)
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

                    int setSection = thisSection.ActivePins[thisElement.OutPin[0], thisElement.OutPin[1]].Link;
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
                        actDirection = (Heading)thisElement.Direction;
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
                    thisPosition.TCOffset, thisPosition.TCDirection,
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
                else if (thisTrain != null && foundSignal.enabledTrain != thisTrain)
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

            for (int iNode = 0; iNode < TrackCircuitList.Count; iNode++)
            {
                setSignalCrossReference(iNode);
            }

            //
            // Set default next signal and fixed route information
            //

            for (int iSignal = 0; signalObjects != null && iSignal < signalObjects.Length; iSignal++)
            {
                Signal thisSignal = signalObjects[iSignal];
                if (thisSignal != null)
                {
                    thisSignal.setSignalDefaultNextSignal();
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
                    if (thisSignal.direction == 1)
                    {
                        signalDistance = thisCircuit.Length - signalDistance;
                    }

                    for (int fntype = 0; fntype < ORTSSignalTypeCount; fntype++)
                    {
                        if (thisSignal.isORTSSignalType(fntype))
                        {
                            TrackCircuitSignalItem thisTCItem =
                                    new TrackCircuitSignalItem(thisSignal, signalDistance);

                            Heading directionList = thisSignal.direction == 0 ? Heading.Reverse : Heading.Ahead;
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
                    newLastDistance[thisSignal.direction] = signalDistance;
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
                        if (thisSpeedpost.direction == 1)
                        {
                            speedpostDistance = thisCircuit.Length - speedpostDistance;
                        }

                        if (speedpostDistance == lastDistance[thisSpeedpost.direction]) // if at same position as last item
                        {
                            speedpostDistance = speedpostDistance + 0.001f;  // shift 1 mm so it will be found
                        }

                        TrackCircuitSignalItem thisTCItem =
                                new TrackCircuitSignalItem(thisSpeedpost, speedpostDistance);

                        Heading directionList = thisSpeedpost.direction == 0 ? Heading.Reverse : Heading.Ahead;
                        TrackCircuitSignalList thisSignalList = thisCircuit.CircuitItems.TrackCircuitSpeedPosts[directionList];

                        if (directionList == 0)
                        {
                            thisSignalList.Insert(0, thisTCItem);
                        }
                        else
                        {
                            thisSignalList.Add(thisTCItem);
                        }

                        newLastDistance[thisSpeedpost.direction] = speedpostDistance;
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
                         thisSection.CircuitItems.TrackCircuitSignals[0] [(int)SignalFunction.Normal];

                while (sectionSignals.Count > 0)
                {
                    TrackCircuitSignalItem thisSignal = sectionSignals[0];
                    sectionSignals.RemoveAt(0);

                    newIndex = nextNode;
                    nextNode++;

                    TrackCircuitSection.SplitSection(thisIndex, newIndex, thisSection.Length - thisSignal.SignalLocation);
                    TrackCircuitSection newSection = TrackCircuitList[newIndex];
                    newSection.EndSignals[Heading.Ahead] = thisSignal.Signal;
                    thisSection = TrackCircuitList[thisIndex];
                    addIndex.Add(newIndex);

                    // restore list (link is lost as item is replaced)
                    sectionSignals = thisSection.CircuitItems.TrackCircuitSignals[0] [(int)SignalFunction.Normal];
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

                        List<TrackCircuitSignalItem> sectionSignals = thisSection.CircuitItems.TrackCircuitSignals[Heading.Reverse] [(int)SignalFunction.Normal];

                        if (sectionSignals.Count > 0)
                        {
                            TrackCircuitSignalItem thisSignal = sectionSignals[0];
                            sectionSignals.RemoveAt(0);

                            newIndex = nextNode;
                            nextNode++;

                            TrackCircuitSection.SplitSection(thisIndex, newIndex, thisSignal.SignalLocation);
                            TrackCircuitSection newSection = TrackCircuitList[newIndex];
                            newSection.EndSignals[Heading.Ahead] = null;
                            thisSection = TrackCircuitList[thisIndex];
                            thisSection.EndSignals[Heading.Reverse] = thisSignal.Signal;

                            // restore list (link is lost as item is replaced)
                            sectionSignals = thisSection.CircuitItems.TrackCircuitSignals[Heading.Reverse] [(int)SignalFunction.Normal];
                        }
                    }
                    thisIndex = thisSection.CircuitItems.TrackCircuitSignals[Heading.Reverse] [(int)SignalFunction.Normal].Count > 0 ? thisIndex : newIndex;
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
                sectionIndex = section.Pins[1, 0].Link;

                if (sectionIndex > 0)
                {
                    section = TrackCircuitList[sectionIndex];
                    if (section.CircuitType == TrackCircuitType.Crossover)
                    {
                        if (section.Pins[0, 0].Link == prevSection)
                        {
                            sectionIndex = section.Pins[1, 0].Link;
                        }
                        else
                        {
                            sectionIndex = section.Pins[1, 1].Link;
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

            for (int iDirection = 0; iDirection <= 1; iDirection++)
            {
                for (int iPin = 0; iPin <= 1; iPin++)
                {
                    int linkedNode = thisSection.Pins[iDirection, iPin].Link;
                    int linkedDirection = thisSection.Pins[iDirection, iPin].Direction == 0 ? 1 : 0;

                    if (linkedNode > 0)
                    {
                        TrackCircuitSection linkedSection = TrackCircuitList[linkedNode];

                        bool linkfound = false;
                        bool doublelink = false;
                        int doublenode = -1;

                        for (int linkedPin = 0; linkedPin <= 1; linkedPin++)
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
                            Trace.TraceWarning("Ignored invalid track node {0} pin [{1},{2}] link to track node {3}", thisNode, iDirection, iPin, linkedNode);
                            int endNode = nextNode;
                            nextNode++;
                            TrackCircuitSection.InsertEndNode(thisNode, iDirection, iPin, endNode);
                        }

                        if (doublelink)
                        {
                            Trace.TraceWarning("Ignored invalid track node {0} pin [{1},{2}] link to track node {3}; already linked to track node {4}", thisNode, iDirection, iPin, linkedNode, doublenode);
                            int endNode = nextNode;
                            nextNode++;
                            TrackCircuitSection.InsertEndNode(thisNode, iDirection, iPin, endNode);
                        }
                    }
                    else if (linkedNode == 0)
                    {
                        Trace.TraceWarning("Ignored invalid track node {0} pin [{1},{2}] link to track node {3}", thisNode, iDirection, iPin, linkedNode);
                        int endNode = nextNode;
                        nextNode++;
                        TrackCircuitSection.InsertEndNode(thisNode, iDirection, iPin, endNode);
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

            for (int iDirection = 0; iDirection <= 1; iDirection++)
            {
                for (int iPin = 0; iPin <= 1; iPin++)
                {
                    if (thisSection.Pins[iDirection, iPin].Link > 0)
                    {
                        TrackCircuitSection nextSection = null;

                        if (thisSection.CircuitType == TrackCircuitType.Junction)
                        {
                            int nextIndex = thisSection.Pins[iDirection, iPin].Link;
                            nextSection = TrackCircuitList[nextIndex];

                            if (thisSection.Pins[iDirection, 1].Link > 0)    // Junction end
                            {
                                thisSection.ActivePins[iDirection, iPin] = thisSection.Pins[iDirection, iPin].FromLink(-1);
                            }
                            else
                            {
                                thisSection.ActivePins[iDirection, iPin] = thisSection.Pins[iDirection, iPin];
                            }
                        }
                        else if (thisSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            int nextIndex = thisSection.Pins[iDirection, iPin].Link;
                            nextSection = TrackCircuitList[nextIndex];

                            thisSection.ActivePins[iDirection, iPin] = thisSection.Pins[iDirection, iPin].FromLink(-1);
                        }
                        else
                        {
                            int nextIndex = thisSection.Pins[iDirection, iPin].Link;
                            nextSection = TrackCircuitList[nextIndex];

                            thisSection.ActivePins[iDirection, iPin] = thisSection.Pins[iDirection, iPin];
                        }


                        if (nextSection != null && nextSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            thisSection.ActivePins[iDirection, iPin] = thisSection.ActivePins[iDirection, iPin].FromLink(-1);
                        }
                        else if (nextSection != null && nextSection.CircuitType == TrackCircuitType.Junction)
                        {
                            int nextDirection = thisSection.Pins[iDirection, iPin].Direction == 0 ? 1 : 0;
                            //                          int nextDirection = thisSection.Pins[iDirection, iPin].Direction;
                            if (nextSection.Pins[nextDirection, 1].Link > 0)
                            {
                                thisSection.ActivePins[iDirection, iPin] = thisSection.ActivePins[iDirection, iPin].FromLink(-1);
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
                for (int iPin = 0; iPin <= 1; iPin++)
                {
                    int prevIndex = thisSection.Pins[0, iPin].Link;
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
        /// Set trackcircuit cross reference for signal items and speedposts
        /// </summary>

        private void setSignalCrossReference(int thisNode)
        {

            TrackCircuitSection thisSection = TrackCircuitList[thisNode];

            // process end signals

            foreach (Heading heading in EnumExtension.GetValues<Heading>())
            {
                Signal thisSignal = thisSection.EndSignals[heading];
                if (thisSignal != null)
                {
                    thisSignal.TCReference = thisNode;
                    thisSignal.TCOffset = thisSection.Length;
                    thisSignal.TCDirection = (int)heading;

                    //                  int pinIndex = iDirection == 0 ? 1 : 0;
                    int pinIndex = (int)heading;
                    thisSignal.TCNextTC = thisSection.Pins[pinIndex, 0].Link;
                    thisSignal.TCNextDirection = thisSection.Pins[pinIndex, 0].Direction;
                }
            }

            // process other signals - only set info if not already set

            foreach (Heading heading in EnumExtension.GetValues<Heading>())
            {
                for (int fntype = 0; fntype < ORTSSignalTypeCount; fntype++)
                {
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[heading][fntype];
                    foreach (TrackCircuitSignalItem thisItem in thisList)
                    {
                        Signal thisSignal = thisItem.Signal;

                        if (thisSignal.TCReference <= 0)
                        {
                            thisSignal.TCReference = thisNode;
                            thisSignal.TCOffset = thisItem.SignalLocation;
                            thisSignal.TCDirection = (int)heading;
                        }
                    }
                }
            }


            // process speedposts

            foreach (Heading heading in EnumExtension.GetValues<Heading>())
            {
                TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSpeedPosts[heading];
                foreach (TrackCircuitSignalItem thisItem in thisList)
                {
                    Signal thisSignal = thisItem.Signal;

                    if (thisSignal.TCReference <= 0)
                    {
                        thisSignal.TCReference = thisNode;
                        thisSignal.TCOffset = thisItem.SignalLocation;
                        thisSignal.TCDirection = (int)heading;
                    }
                }
            }


            // process mileposts

            foreach (TrackCircuitMilepost trackCircuitMilepost in thisSection.CircuitItems.TrackCircuitMileposts)
            {
                if (trackCircuitMilepost.Milepost.TrackCircuitReference <= 0)
                {
                    trackCircuitMilepost.Milepost.SetCircuit(thisNode, trackCircuitMilepost.MilepostLocation[Location.NearEnd]);
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
                    thisTrain.Train.PresentPosition[1].TCDirection, thisTrain.Train.Length, true, true, false);

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

                            if (!furthestRouteCleared && thisSection.EndSignals[(Heading)thisElement.Direction] != null)
                            {
                                Signal endSignal = thisSection.EndSignals[(Heading)thisElement.Direction];
                                // check if signal enabled for other train - if so, keep in node control
                                if (endSignal.enabledTrain == null || endSignal.enabledTrain == thisTrain)
                                {
                                    if (routeIndex < routePart.Count)
                                    {
                                        thisTrain.Train.SwitchToSignalControl(thisSection.EndSignals[(Heading)thisElement.Direction]);
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

            if (!furthestRouteCleared && lastRouteIndex > 0 && routePart[lastRouteIndex].TCSectionIndex >= 0  && endAuthority != Train.END_AUTHORITY.LOOP)
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
                            if (routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[0, 0].Link || routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[0, 1].Link)
                            {
                                if (routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[1, 0].Link || routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[1, 1].Link)
                                {
                                    jnAligned = true;
                                }
                            }
                            else if (routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[1, 0].Link || routePart[jnIndex + 1].TCSectionIndex == thisSection.ActivePins[1, 1].Link)
                            {
                                if (routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[0, 0].Link || routePart[jnIndex - 1].TCSectionIndex == thisSection.ActivePins[0, 1].Link)
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
            int nextDirection = 0;

            for (int iPinLink = 0; iPinLink <= 1; iPinLink++)
            {
                for (int iPinIndex = 0; iPinIndex <= 1; iPinIndex++)
                {
                    int trySectionIndex = firstSection.Pins[iPinLink, iPinIndex].Link;
                    if (trySectionIndex > 0)
                    {
                        TrackCircuitSection trySection = TrackCircuitList[trySectionIndex];
                        if (trySection.CircuitState.TrainReserved != null && reqTrain != null && trySection.CircuitState.TrainReserved.Train == reqTrain.Train)
                        {
                            nextSection = trySection;
                            nextDirection = firstSection.Pins[iPinLink, iPinIndex].Direction;
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

                int iPinLink = nextDirection;
                for (int iPinIndex = 0; iPinIndex <= 1; iPinIndex++)
                {
                    int trySectionIndex = thisSection.ActivePins[iPinLink, iPinIndex].Link;
                    if (trySectionIndex > 0)
                    {
                        trySection = TrackCircuitList[trySectionIndex];
                        if (trySection.CircuitState.TrainReserved != null && reqTrain != null && trySection.CircuitState.TrainReserved.Train == reqTrain.Train)
                        {
                            nextSection = trySection;
                            nextDirection = thisSection.ActivePins[iPinLink, iPinIndex].Direction;
                        }
                    }
                }

                // not found, then try possible links

                for (int iPinIndex = 0; iPinIndex <= 1; iPinIndex++)
                {
                    int trySectionIndex = thisSection.Pins[iPinLink, iPinIndex].Link;
                    if (trySectionIndex > 0)
                    {
                        trySection = TrackCircuitList[trySectionIndex];
                        if (trySection.CircuitState.TrainReserved != null && reqTrain != null && trySection.CircuitState.TrainReserved.Train == reqTrain.Train)
                        {
                            nextSection = trySection;
                            nextDirection = thisSection.Pins[iPinLink, iPinIndex].Direction;
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
                    Signal thisSignal = thisSection.EndSignals[(Heading)reqRoute[iindex].Direction];
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
                int firstSectionIndex, float firstOffset, int firstDirection,
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
                        thisElement.OutPin[1] = thisSection.Pins[1, 0].Link == tempRoute[iElement + 1].TCSectionIndex ? 0 : 1;
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

        public List<int> ScanRoute(Train thisTrain, int firstSectionIndex, float firstOffset, int firstDirection, bool forward,
                float routeLength, bool honourManualSwitch, bool autoAlign, bool stopAtFacingSignal, bool reservedOnly, bool returnSections,
                bool searchFacingSignal, bool searchBackwardSignal, bool searchFacingSpeedpost, bool searchBackwardSpeedpost,
                bool isFreight, bool considerSpeedReset = false, bool checkReenterOriginalRoute = false)
        {

            int sectionIndex = firstSectionIndex;

            int lastIndex = -2;   // set to values not encountered for pin links
            int thisIndex = sectionIndex;

            float offset = firstOffset;
            Heading curDirection = (Heading)firstDirection;
            int nextDirection = (int)curDirection;

            TrackCircuitSection thisSection = TrackCircuitList[sectionIndex];

            float coveredLength = firstOffset;
            if (forward || (firstDirection == 1 && !forward))
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

                Heading oppDirection = curDirection.Next();

                int outPinIndex = forward ? (int)curDirection : (int)oppDirection;
                int inPinIndex = outPinIndex == 0 ? 1 : 0;

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
                                    foundObject.Add(thisItem.Signal.thisRef);
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
                                    foundObject.Add(thisItem.Signal.thisRef);
                                    endOfRoute = true;
                                }
                            }
                        }
                    }
                }

                if (searchFacingSignal && thisSection.EndSignals[curDirection] != null)           // search facing signal
                {
                    foundObject.Add(thisSection.EndSignals[curDirection].thisRef);
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
                                    foundObject.Add(-(thisItem.Signal.thisRef));
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
                                    foundObject.Add(-(thisItem.Signal.thisRef));
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
                        if (thisSection.Pins[inPinIndex, 0].Link == lastIndex)
                        {
                            nextIndex = thisSection.Pins[outPinIndex, 0].Link;
                            nextDirection = thisSection.Pins[outPinIndex, 0].Direction;
                        }
                        else if (thisSection.Pins[inPinIndex, 1].Link == lastIndex)
                        {
                            nextIndex = thisSection.Pins[outPinIndex, 1].Link;
                            nextDirection = thisSection.Pins[outPinIndex, 1].Direction;
                        }
                        break;

                    case TrackCircuitType.Junction:
//                        if (checkReenterOriginalRoute && foundItems.Count > 2)
                        if (checkReenterOriginalRoute)
                        {
                            Train.TCSubpathRoute originalSubpath = thisTrain.TCRoute.TCRouteSubpaths[thisTrain.TCRoute.OriginalSubpath];
                            if (outPinIndex == 0)
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

                        if (thisSection.ActivePins[outPinIndex, 0].Link > 0)
                        {
                            nextIndex = thisSection.ActivePins[outPinIndex, 0].Link;
                            nextDirection = thisSection.ActivePins[outPinIndex, 0].Direction;
                        }
                        else if (thisSection.ActivePins[outPinIndex, 1].Link > 0)
                        {
                            nextIndex = thisSection.ActivePins[outPinIndex, 1].Link;
                            nextDirection = thisSection.ActivePins[outPinIndex, 1].Direction;
                        }
                        else if (honourManualSwitch && thisSection.JunctionSetManual >= 0)
                        {
                            nextIndex = thisSection.Pins[outPinIndex, thisSection.JunctionSetManual].Link;
                            nextDirection = thisSection.Pins[outPinIndex, thisSection.JunctionSetManual].Direction;
                        }
                        else if (!reservedOnly)
                        {
                            nextIndex = thisSection.Pins[outPinIndex, thisSection.JunctionLastRoute].Link;
                            nextDirection = thisSection.Pins[outPinIndex, thisSection.JunctionLastRoute].Direction;
                        }
                        break;

                    case TrackCircuitType.EndOfTrack:
                        break;

                    default:
                        nextIndex = thisSection.Pins[outPinIndex, 0].Link;
                        nextDirection = thisSection.Pins[outPinIndex, 0].Direction;

                        TrackCircuitSection nextSection = TrackCircuitList[nextIndex];

                        // if next section is junction : check if locked against AI and if auto-alignment allowed
                        // switchable end of switch is always pin direction 1
                        if (nextSection.CircuitType == TrackCircuitType.Junction)
                        {
                            int nextPinDirection = nextDirection == 0 ? 1 : 0;
                            int nextPinIndex = nextSection.Pins[(nextDirection == 0 ? 1 : 0), 0].Link == thisIndex ? 0 : 1;
                            if (nextPinDirection == 1 && nextSection.JunctionLastRoute != nextPinIndex)
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
                    curDirection = forward ? (Heading)nextDirection : nextDirection == 0 ? Heading.Reverse : Heading.Ahead;
                    oppDirection = curDirection.Next();

                    if (searchBackwardSignal && thisSection.EndSignals[oppDirection] != null)
                    {
                        endOfRoute = true;
                        foundObject.Add(-(thisSection.EndSignals[oppDirection].thisRef));
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
                    thisDetails.TrackCircuitOffset[refIndex, Heading.Reverse] = thisPlatform.SData1 - thisSection.OffsetLength[Location.FarEnd];
                    thisDetails.TrackCircuitOffset[refIndex.Next(), Heading.Ahead] = thisSection.Length - thisDetails.TrackCircuitOffset[refIndex, Heading.Reverse];
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
                        tf = thisDetails.TrackCircuitOffset[location, Heading.Ahead];
                        thisDetails.TrackCircuitOffset[location, Heading.Ahead] = thisDetails.TrackCircuitOffset[location, Heading.Reverse];
                        thisDetails.TrackCircuitOffset[location, Heading.Reverse] = tf;
                    }
                }

                // search for end signals

                thisNode = trackNodes[TrackCircuitList[thisDetails.TCSectionIndex[0]].OriginalIndex];

                if (refIndex == Location.FarEnd)
                {
                    float distToSignal = 0.0f;
                    float offset = thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Ahead];
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

                        if (thisSection.EndSignals[Heading.Ahead] != null)
                        {
                            // end signal is always valid in timetable mode
                            if (Simulator.TimetableMode || distToSignal <= 150)
                            {
                                thisDetails.EndSignals[Heading.Ahead] = thisSection.EndSignals[Heading.Ahead].thisRef;
                                thisDetails.DistanceToSignals[Heading.Ahead] = distToSignal;
                            }
                            // end signal is only valid if it has no fixed route in activity mode
                            else
                            {
                                float? approachControlLimitPositionM = null;
                                if (distToSignal > 150)
                                {
                                    foreach (SignalHead signalHead in thisSection.EndSignals[Heading.Ahead].SignalHeads)
                                    {
                                        if (signalHead.ApproachControlLimitPositionM != null) approachControlLimitPositionM = signalHead.ApproachControlLimitPositionM;
                                    }
                                }
                                if (!thisSection.EndSignals[Heading.Ahead].hasFixedRoute && !(approachControlLimitPositionM != null && (float)approachControlLimitPositionM < distToSignal + 100))
                                {
                                    thisDetails.EndSignals[Heading.Ahead] = thisSection.EndSignals[Heading.Ahead].thisRef;
                                    thisDetails.DistanceToSignals[Heading.Ahead] = distToSignal;
                                }
                            }
                            break;
                        }
                    }

                    distToSignal = 0.0f;
                    offset = thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Reverse];
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

                        if (thisSection.EndSignals[Heading.Reverse] != null)
                        {
                            if (Simulator.TimetableMode || distToSignal <= 150)
                            {
                                thisDetails.EndSignals[Heading.Reverse] = thisSection.EndSignals[Heading.Reverse].thisRef;
                                thisDetails.DistanceToSignals[Heading.Reverse] = distToSignal;
                            }
                            else
                            {
                                float? approachControlLimitPositionM = null;
                                if (distToSignal > 150)
                                {
                                    foreach (SignalHead signalHead in thisSection.EndSignals[Heading.Reverse].SignalHeads)
                                    {
                                        if (signalHead.ApproachControlLimitPositionM != null) approachControlLimitPositionM = signalHead.ApproachControlLimitPositionM;
                                    }
                                }
                                if (!thisSection.EndSignals[Heading.Reverse].hasFixedRoute && !(approachControlLimitPositionM != null && (float)approachControlLimitPositionM < distToSignal + 100))
                                {
                                    thisDetails.EndSignals[Heading.Reverse] = thisSection.EndSignals[Heading.Reverse].thisRef;
                                    thisDetails.DistanceToSignals[Heading.Reverse] = distToSignal;
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
                totalLength1 -= thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Ahead];  // correct for offset
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
                totalLength1 -= thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Reverse];  // correct for offset
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
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] = TrackCircuitList[PlSections1[PlSections1.Count - 1]].Length - totalLength1;
                    for (int iSection = 0; iSection < PlSections1.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] += TrackCircuitList[PlSections1[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Reverse] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Ahead] = TrackCircuitList[PlSections1[0]].Length;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Reverse] = firstPlatform.SData1;
                    for (int iSection = 0; iSection < PlSections1.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] -= TrackCircuitList[PlSections1[iSection]].Length;
                    }
                }
                else
                {
                    thisDetails.NodeOffset[Location.NearEnd] = firstPlatform.SData1;
                    thisDetails.NodeOffset[Location.FarEnd] = thisDetails.NodeOffset[Location.NearEnd] + totalLength1;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Reverse] = TrackCircuitList[PlSections1[0]].Length - totalLength1;
                    for (int iSection = 1; iSection < PlSections1.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] += TrackCircuitList[PlSections1[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Ahead] = totalLength1;
                    for (int iSection = 1; iSection < PlSections1.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] -= TrackCircuitList[PlSections1[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Reverse] = TrackCircuitList[PlSections1[PlSections1.Count - 1]].Length;
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
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] = TrackCircuitList[PlSections2.Count - 1].Length - totalLength2;
                    for (int iSection = 0; iSection < PlSections2.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] += TrackCircuitList[PlSections2[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Reverse] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Ahead] = TrackCircuitList[PlSections2[0]].Length;
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Reverse] = secondPlatform.SData1;
                    for (int iSection = 0; iSection < PlSections2.Count - 2; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] -= TrackCircuitList[PlSections2[iSection]].Length;
                    }
                }
                else
                {
                    thisDetails.NodeOffset[Location.NearEnd] = secondPlatform.SData1;
                    thisDetails.NodeOffset[Location.FarEnd] = thisDetails.NodeOffset[0] + totalLength2;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] = 0.0f;
                    thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Reverse] = TrackCircuitList[PlSections2[0]].Length - totalLength2;
                    for (int iSection = 1; iSection < PlSections2.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] += TrackCircuitList[PlSections2[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Ahead] = totalLength2;
                    for (int iSection = 1; iSection < PlSections2.Count - 1; iSection++)
                    {
                        thisDetails.TrackCircuitOffset[Location.NearEnd, Heading.Ahead] -= TrackCircuitList[PlSections2[iSection]].Length;
                    }
                    thisDetails.TrackCircuitOffset[Location.FarEnd, Heading.Reverse] = TrackCircuitList[PlSections2[PlSections2.Count - 1]].Length;
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
                if (thisNode is TrackVectorNode tvn )
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

    //================================================================================================//
    /// <summary>
    ///
    ///  class SignalObject
    ///
    /// </summary>
    //================================================================================================//

    public class Signal
    {

        public enum InternalBlockstate
        {
            Reserved,                   // all sections reserved for requiring train       //
            Reservable,                 // all secetions clear and reservable for train    //
            OccupiedSameDirection,      // occupied by train moving in same direction      //
            ReservedOther,              // reserved for other train                        //
            ForcedWait,                 // train is forced to wait for other train         //
            OccupiedOppositeDirection,  // occupied by train moving in opposite direction  //
            Open,                       // sections are claimed and not accesible          //
            Blocked,                    // switch locked against train                     //
        }

        public enum Permission
        {
            Granted,
            Requested,
            Denied,
        }

        public enum HoldState                // signal is locked in hold
        {
            None,                            // signal is clear
            StationStop,                     // because of station stop
            ManualLock,                      // because of manual lock. 
            ManualPass,                      // Sometime you want to set a light green, especially in MP
            ManualApproach,                  // Sometime to set approach, in MP again
            //PLEASE DO NOT CHANGE THE ORDER OF THESE ENUMS
        }

        public Signals signalRef;               // reference to overlaying Signal class
        public static Signal[] signalObjects;
        public static TrackNode[] trackNodes;
        public static TrackItem[] trItems;
        public SignalWorldInfo WorldObject;   // Signal World Object information

        public int trackNode;                   // Track node which contains this signal
        public int trRefIndex;                  // Index to TrItemRef within Track Node 

        public int TCReference = -1;            // Reference to TrackCircuit (index)
        public float TCOffset;                  // Position within TrackCircuit
        public int TCDirection;                 // Direction within TrackCircuit
        public int TCNextTC = -1;               // Index of next TrackCircuit (NORMAL signals only)
        public int TCNextDirection;             // Direction of next TrackCircuit 
        public int? nextSwitchIndex = null;     // index of first switch in path

        public List<int> JunctionsPassed = new List<int>();  // Junctions which are passed checking next signal //

        public int thisRef;                     // This signal's reference.
        public int direction;                   // Direction facing on track

        public bool isSignal = true;            // if signal, false if speedpost //
        public bool isSpeedSignal = true;       // if signal of type SPEED, false if fixed speedpost or actual signal
        public List<SignalHead> SignalHeads = new List<SignalHead>();

        public int SignalNumClearAhead_MSTS = -2;    // Overall maximum SignalNumClearAhead over all heads (MSTS calculation)
        public int SignalNumClearAhead_ORTS = -2;    // Overall maximum SignalNumClearAhead over all heads (ORTS calculation)
        public int SignalNumClearAheadActive = -2;   // Active SignalNumClearAhead (for ORST calculation only, as set by script)
        public int SignalNumNormalHeads;             // no. of normal signal heads in signal
        public int ReqNumClearAhead;                 // Passed on value for SignalNumClearAhead

        public int draw_state;                  // actual signal state
        public Dictionary<int, int> localStorage = new Dictionary<int, int>();  // list to store local script variables
        public bool noupdate = false;                // set if signal does not required updates (fixed signals)

        public Train.TrainRouted enabledTrain;  // full train structure for which signal is enabled

        private InternalBlockstate internalBlockState = InternalBlockstate.Open;    // internal blockstate
        public Permission hasPermission = Permission.Denied;  // Permission to pass red signal
        public HoldState holdState = HoldState.None;

        public List<int> sigfound = new List<int>();  // active next signal - used for signals with NORMAL heads only
        public int reqNormalSignal = -1;              // ref of normal signal requesting route clearing (only used for signals without NORMAL heads)
        private List<int> defaultNextSignal = new List<int>();  // default next signal
        public Traveller tdbtraveller;          // TDB traveller to determine distance between objects

        public Train.TCSubpathRoute signalRoute = new Train.TCSubpathRoute();  // train route from signal
        public int trainRouteDirectionIndex;    // direction index in train route array (usually 0, value 1 valid for Manual only)
        public int thisTrainRouteIndex;        // index of section after signal in train route list

        private Train.TCSubpathRoute fixedRoute = new Train.TCSubpathRoute();     // fixed route from signal
        public bool hasFixedRoute;              // signal has fixed route
        private bool fullRoute;                 // required route is full route to next signal or end-of-track
        private bool AllowPartRoute = false;    // signal is always allowed to clear unto partial route
        private bool propagated;                // route request propagated to next signal
        private bool isPropagated;              // route request for this signal was propagated from previous signal
        public bool ForcePropagation = false;   // Force propagation (used in case of signals at very short distance)

        public bool ApproachControlCleared;     // set in case signal has cleared on approach control
        public bool ApproachControlSet;         // set in case approach control is active
        public bool ClaimLocked;                // claim is locked in case of approach control
        public bool ForcePropOnApproachControl; // force propagation if signal is held on close control
        public double TimingTriggerValue;        // used timing trigger if time trigger is required, hold trigger time

        public bool StationHold = false;        // Set if signal must be held at station - processed by signal script
        protected List<KeyValuePair<int, int>> LockedTrains;

        public bool enabled
        {
            get
            {
                if (MPManager.IsMultiPlayer() && MPManager.PreferGreen == true) return true;
                return (enabledTrain != null);
            }
        }

        public SignalBlockState blockState
        {
            get
            {
                SignalBlockState lstate = SignalBlockState.Jn_Obstructed;
                switch (internalBlockState)
                {
                    case InternalBlockstate.Reserved:
                    case InternalBlockstate.Reservable:
                        lstate = SignalBlockState.Clear;
                        break;
                    case InternalBlockstate.OccupiedSameDirection:
                        lstate = SignalBlockState.Occupied;
                        break;
                    default:
                        lstate = SignalBlockState.Jn_Obstructed;
                        break;
                }

                return (lstate);
            }
        }

        public int trItem
        {
            get
            {
                return (trackNodes[trackNode] as TrackVectorNode).TrackItemIndices[trRefIndex];
            }
        }

        public int revDir                //  Needed because signal faces train!
        {
            get
            {
                return direction == 0 ? 1 : 0;
            }
        }

        //================================================================================================//
        /// <summary>
        ///  Constructor for empty item
        /// </summary>

        public Signal(int ORTSSignalTypes)
        {
            LockedTrains = new List<KeyValuePair<int, int>>();
            sigfound = new List<int>();
            defaultNextSignal = new List<int>();

            for (int ifntype = 0; ifntype < ORTSSignalTypes; ifntype++)
            {
                sigfound.Add(-1);
                defaultNextSignal.Add(-1);
            }
        }

        //================================================================================================//
        /// <summary>
        ///  Constructor for Copy 
        /// </summary>

        public Signal(Signal copy)
        {
            signalRef = copy.signalRef;
            WorldObject = new SignalWorldInfo(copy.WorldObject);

            trackNode = copy.trackNode;
            LockedTrains = new List<KeyValuePair<int, int>>();
            foreach (var lockInfo in copy.LockedTrains)
            {
                KeyValuePair<int, int> oneLock = new KeyValuePair<int, int>(lockInfo.Key, lockInfo.Value);
                LockedTrains.Add(oneLock);
            }

            TCReference = copy.TCReference;
            TCOffset = copy.TCOffset;
            TCDirection = copy.TCDirection;
            TCNextTC = copy.TCNextTC;
            TCNextDirection = copy.TCNextDirection;

            direction = copy.direction;
            isSignal = copy.isSignal;
            SignalNumClearAhead_MSTS = copy.SignalNumClearAhead_MSTS;
            SignalNumClearAhead_ORTS = copy.SignalNumClearAhead_ORTS;
            SignalNumClearAheadActive = copy.SignalNumClearAheadActive;
            SignalNumNormalHeads = copy.SignalNumNormalHeads;

            draw_state = copy.draw_state;
            internalBlockState = copy.internalBlockState;
            hasPermission = copy.hasPermission;

            tdbtraveller = new Traveller(copy.tdbtraveller);

            sigfound = new List<int>(copy.sigfound);
            defaultNextSignal = new List<int>(copy.defaultNextSignal);
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for restore
        /// IMPORTANT : enabled train is restore temporarily as Trains are restored later as Signals
        /// Full restore of train link follows in RestoreTrains
        /// </summary>

        public void Restore(Simulator simulator, BinaryReader inf)
        {
            int trainNumber = inf.ReadInt32();

            int sigfoundLength = inf.ReadInt32();
            for (int iSig = 0; iSig < sigfoundLength; iSig++)
            {
                sigfound[iSig] = inf.ReadInt32();
            }

            bool validRoute = inf.ReadBoolean();

            if (validRoute)
            {
                signalRoute = new Train.TCSubpathRoute(inf);
            }

            thisTrainRouteIndex = inf.ReadInt32();
            holdState = (HoldState)inf.ReadInt32();

            int totalJnPassed = inf.ReadInt32();

            for (int iJn = 0; iJn < totalJnPassed; iJn++)
            {
                int thisJunction = inf.ReadInt32();
                JunctionsPassed.Add(thisJunction);
                signalRef.TrackCircuitList[thisJunction].SignalsPassingRoutes.Add(thisRef);
            }

            fullRoute = inf.ReadBoolean();
            AllowPartRoute = inf.ReadBoolean();
            propagated = inf.ReadBoolean();
            isPropagated = inf.ReadBoolean();
            ForcePropagation = false; // preset (not stored)
            SignalNumClearAheadActive = inf.ReadInt32();
            ReqNumClearAhead = inf.ReadInt32();
            StationHold = inf.ReadBoolean();
            ApproachControlCleared = inf.ReadBoolean();
            ApproachControlSet = inf.ReadBoolean();
            ClaimLocked = inf.ReadBoolean();
            ForcePropOnApproachControl = inf.ReadBoolean();
            hasPermission = (Permission)inf.ReadInt32();

            // set dummy train, route direction index will be set later on restore of train

            enabledTrain = null;

            if (trainNumber >= 0)
            {
                Train thisTrain = new Train(simulator, trainNumber);
                Train.TrainRouted thisTrainRouted = new Train.TrainRouted(thisTrain, 0);
                enabledTrain = thisTrainRouted;
            }
            //  Retrieve lock table
            LockedTrains = new List<KeyValuePair<int, int>>();
            int cntLock = inf.ReadInt32();
            for (int cnt = 0; cnt < cntLock; cnt++)
            {
                KeyValuePair<int, int> lockInfo = new KeyValuePair<int, int>(inf.ReadInt32(), inf.ReadInt32());
                LockedTrains.Add(lockInfo);

            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Train Reference
        /// </summary>

        public void RestoreTrains(List<Train> trains)
        {
            if (enabledTrain != null)
            {
                int number = enabledTrain.Train.Number;

                Train foundTrain = Signals.FindTrain(number, trains);

                // check if this signal is next signal forward for this train

                if (foundTrain != null && foundTrain.NextSignalObject[0] != null && this.thisRef == foundTrain.NextSignalObject[0].thisRef)
                {
                    enabledTrain = foundTrain.routedForward;
                    foundTrain.NextSignalObject[0] = this;
                }

                // check if this signal is next signal backward for this train

                else if (foundTrain != null && foundTrain.NextSignalObject[1] != null && this.thisRef == foundTrain.NextSignalObject[1].thisRef)
                {
                    enabledTrain = foundTrain.routedBackward;
                    foundTrain.NextSignalObject[1] = this;
                }
                else
                {
                    // check if this section is reserved for this train

                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[TCReference];
                    if (thisSection.CircuitState.TrainReserved != null && thisSection.CircuitState.TrainReserved.Train.Number == number)
                    {
                        enabledTrain = thisSection.CircuitState.TrainReserved;
                    }
                    else
                    {
                        enabledTrain = null; // reset - train not found
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore Signal Aspect based on train information
        /// Process non-propagated signals only, others are updated through propagation
        /// </summary>

        public void RestoreAspect()
        {
            if (enabledTrain != null && !isPropagated)
            {
                if (isSignalNormal())
                {
                    checkRouteState(false, signalRoute, enabledTrain);
                    propagateRequest();
                    StateUpdate();
                }
                else
                {
                    getBlockState_notRouted();
                    StateUpdate();
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Save
        /// </summary>

        public void Save(BinaryWriter outf)
        {
            if (enabledTrain == null)
            {
                outf.Write(-1);
            }
            else
            {
                outf.Write(enabledTrain.Train.Number);
            }

            outf.Write(sigfound.Count);
            foreach (int thisSig in sigfound)
            {
                outf.Write(thisSig);
            }

            if (signalRoute == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                signalRoute.Save(outf);
            }

            outf.Write(thisTrainRouteIndex);
            outf.Write((int)holdState);

            outf.Write(JunctionsPassed.Count);
            if (JunctionsPassed.Count > 0)
            {
                foreach (int thisJunction in JunctionsPassed)
                {
                    outf.Write(thisJunction);
                }
            }

            outf.Write(fullRoute);
            outf.Write(AllowPartRoute);
            outf.Write(propagated);
            outf.Write(isPropagated);
            outf.Write(SignalNumClearAheadActive);
            outf.Write(ReqNumClearAhead);
            outf.Write(StationHold);
            outf.Write(ApproachControlCleared);
            outf.Write(ApproachControlSet);
            outf.Write(ClaimLocked);
            outf.Write(ForcePropOnApproachControl);
            outf.Write((int)hasPermission);
            outf.Write(LockedTrains.Count);
            for (int cnt = 0; cnt < LockedTrains.Count; cnt++)
            {
                outf.Write(LockedTrains[cnt].Key);
                outf.Write(LockedTrains[cnt].Value);
            }

        }

        //================================================================================================//
        /// <summary>
        /// return blockstate
        /// </summary>

        public SignalBlockState block_state()
        {
            return (blockState);
        }

        //================================================================================================//
        /// <summary>
        /// return station hold state
        /// </summary>

        public bool isStationHold()
        {
            return (StationHold);
        }

        //================================================================================================//
        /// <summary>
        /// setSignalDefaultNextSignal : set default next signal based on non-Junction tracks ahead
        /// this routine also sets fixed routes for signals which do not lead onto junction or crossover
        /// </summary>

        public void setSignalDefaultNextSignal()
        {
            int thisTC = TCReference;
            float position = TCOffset;
            Heading direction = (Heading)TCDirection;
            bool setFixedRoute = false;

            // for normal signals : start at next TC

            if (TCNextTC > 0)
            {
                thisTC = TCNextTC;
                direction = (Heading)TCNextDirection;
                position = 0.0f;
                setFixedRoute = true;
            }

            bool completedFixedRoute = !setFixedRoute;

            // get trackcircuit

            TrackCircuitSection thisSection = null;
            if (thisTC > 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];
            }

            // set default

            for (int fntype = 0; fntype < defaultNextSignal.Count; fntype++)
            {
                defaultNextSignal[fntype] = -1;
            }

            // loop through valid sections

            while (thisSection != null && thisSection.CircuitType == TrackCircuitType.Normal)
            {

                if (!completedFixedRoute)
                {
                    fixedRoute.Add(new Train.TCRouteElement(thisSection.Index, (int)direction));
                }

                // normal signal

                if (defaultNextSignal[(int)SignalFunction.Normal] < 0)
                {
                    if (thisSection.EndSignals[direction] != null)
                    {
                        defaultNextSignal[(int)SignalFunction.Normal] = thisSection.EndSignals[direction].thisRef;
                        completedFixedRoute = true;
                    }
                }

                // other signals

                for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
                {
                    if (fntype != (int)SignalFunction.Normal)
                    {
                        TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                        bool signalFound = defaultNextSignal[fntype] >= 0;
                        for (int iItem = 0; iItem < thisList.Count && !signalFound; iItem++)
                        {
                            TrackCircuitSignalItem thisItem = thisList[iItem];
                            if (thisItem.Signal.thisRef != thisRef && (thisSection.Index != thisTC || thisItem.SignalLocation > position))
                            {
                                defaultNextSignal[fntype] = thisItem.Signal.thisRef;
                                signalFound = true;
                            }
                        }
                    }
                }

                int pinIndex = (int)direction;
                direction = (Heading)thisSection.Pins[pinIndex, 0].Direction;
                thisSection = signalRef.TrackCircuitList[thisSection.Pins[pinIndex, 0].Link];
            }

            // copy default as valid items

            for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
            {
                sigfound[fntype] = defaultNextSignal[fntype];
            }

            // Allow use of fixed route if ended on END_OF_TRACK

            if (thisSection != null && thisSection.CircuitType == TrackCircuitType.EndOfTrack)
            {
                completedFixedRoute = true;
            }

            // if valid next normal signal found, signal has fixed route

            if (setFixedRoute && completedFixedRoute)
            {
                hasFixedRoute = true;
                fullRoute = true;
            }
            else
            {
                hasFixedRoute = false;
                fixedRoute.Clear();
            }
        }

        //================================================================================================//
        /// <summary>
        /// isSignalNormal : Returns true if at least one signal head is type normal.
        /// </summary>

        public bool isSignalNormal()
        {
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == SignalFunction.Normal)
                    return true;
            }
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// isORTSSignalType : Returns true if at least one signal head is of required type
        /// </summary>

        public bool isORTSSignalType(int reqSIGFN)
        {
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (reqSIGFN == sigHead.OrtsSignalFunctionIndex)
                    return true;
            }
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_mr : returns most restrictive state of next signal of required type
        /// </summary>

        public SignalAspectState next_sig_mr(int fn_type)
        {
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                return signalObjects[nextSignal].this_sig_mr(fn_type);
            }
            else
            {
                return SignalAspectState.Stop;
            }
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_lr : returns least restrictive state of next signal of required type
        /// </summary>

        public SignalAspectState next_sig_lr(int fn_type)
        {
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                return signalObjects[nextSignal].this_sig_lr(fn_type);
            }
            else
            {
                return SignalAspectState.Stop;
            }
        }

        //================================================================================================//
        /// <summary>
        /// next_nsig_lr : returns least restrictive state of next signal of required type of the nth signal ahead
        /// </summary>

        public SignalAspectState next_nsig_lr(int fn_type, int nsignal)
        {
            int foundsignal = 0;
            SignalAspectState foundAspect = SignalAspectState.Clear_2;
            Signal nextSignalObject = this;

            while (foundsignal < nsignal && foundAspect != SignalAspectState.Stop)
            {
                // use sigfound
                int nextSignal = nextSignalObject.sigfound[fn_type];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = SONextSignal(fn_type);
                    nextSignalObject.sigfound[fn_type] = nextSignal;
                }

                // signal found : get state
                if (nextSignal >= 0)
                {
                    foundsignal++;

                    nextSignalObject = signalObjects[nextSignal];
                    foundAspect = nextSignalObject.this_sig_lr(fn_type);

                    // reached required signal or state is stop : return
                    if (foundsignal >= nsignal || foundAspect == SignalAspectState.Stop)
                    {
                        return (foundAspect);
                    }
                }

                // signal not found : return stop
                else
                {
                    return SignalAspectState.Stop;
                }
            }
            return (SignalAspectState.Stop); // emergency exit - loop should normally have exited on return
        }

        //================================================================================================//
        /// <summary>
        /// opp_sig_mr
        /// </summary>

	/// normal version
        public SignalAspectState opp_sig_mr(int fn_type)
        {
            int signalFound = SONextSignalOpp(fn_type);
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_mr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_mr

	/// debug version
        public SignalAspectState opp_sig_mr(int fn_type, ref Signal foundSignal)
        {
            int signalFound = SONextSignalOpp(fn_type);
            foundSignal = signalFound >= 0 ? signalObjects[signalFound] : null;
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_mr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_mr

        //================================================================================================//
        /// <summary>
        /// opp_sig_lr
        /// </summary>

	/// normal version
        public SignalAspectState opp_sig_lr(int fn_type)
        {
            int signalFound = SONextSignalOpp(fn_type);
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_lr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_lr

	/// debug version
        public SignalAspectState opp_sig_lr(int fn_type, ref Signal foundSignal)
        {
            int signalFound = SONextSignalOpp(fn_type);
            foundSignal = signalFound >= 0 ? signalObjects[signalFound] : null;
            return (signalFound >= 0 ? signalObjects[signalFound].this_sig_lr(fn_type) : SignalAspectState.Stop);
        }//opp_sig_lr

        //================================================================================================//
        /// <summary>
        /// this_sig_mr : Returns the most restrictive state of this signal's heads of required type
        /// </summary>

        /// <summary>
        /// standard version without state return
        /// </summary>
        public SignalAspectState this_sig_mr(int fn_type)
        {
            bool sigfound = false;
            return (this_sig_mr(fn_type, ref sigfound));
        }

        /// <summary>
        /// standard version without state return using MSTS type parameter
        /// </summary>
        public SignalAspectState this_sig_mr(SignalFunction msfn_type)
        {
            bool sigfound = false;
            return (this_sig_mr((int)msfn_type, ref sigfound));
        }

        /// <summary>
        /// additional version with state return
        /// </summary>
        public SignalAspectState this_sig_mr(int fn_type, ref bool sigfound)
        {
            SignalAspectState sigAsp = SignalAspectState.Unknown;
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == fn_type && sigHead.SignalIndicationState < sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                }
            }
            if (sigAsp == SignalAspectState.Unknown)
            {
                sigfound = false;
                return SignalAspectState.Stop;
            }
            else
            {
                sigfound = true;
                return sigAsp;
            }
        }

        /// <summary>
        /// additional version using valid route heads only
        /// </summary>
        internal SignalAspectState MRSignalOnRoute(int signalType)
        {
            SignalAspectState sigAsp = SignalAspectState.Unknown;

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == signalType && sigHead.VerifyRouteSet() == 1 && sigHead.SignalIndicationState < sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                }
            }

            return sigAsp == SignalAspectState.Unknown ? SignalAspectState.Stop : sigAsp;
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_lr : Returns the least restrictive state of this signal's heads of required type
        /// </summary>

        /// <summary>
        /// standard version without state return
        /// </summary>
        public SignalAspectState this_sig_lr(int fn_type)
        {
            bool sigfound = false;
            return (this_sig_lr(fn_type, ref sigfound));
        }

        /// <summary>
        /// standard version without state return using MSTS type parameter
        /// </summary>
        public SignalAspectState this_sig_lr(SignalFunction msfn_type)
        {
            bool sigfound = false;
            return (this_sig_lr((int)msfn_type, ref sigfound));
        }

        /// <summary>
        /// additional version with state return
        /// </summary>
        public SignalAspectState this_sig_lr(int fn_type, ref bool sigfound)
        {
            SignalAspectState sigAsp = SignalAspectState.Stop;
            bool sigAspSet = false;
            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.OrtsSignalFunctionIndex == fn_type && sigHead.SignalIndicationState >= sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                    sigAspSet = true;
                }
            }

            sigfound = sigAspSet;

            if (sigAspSet)
            {
                return sigAsp;
            }
            else if (fn_type == (int)SignalFunction.Normal)
            {
                return SignalAspectState.Clear_2;
            }
            else
            {
                return SignalAspectState.Stop;
            }
        }//this_sig_lr

        //================================================================================================//
        /// <summary>
        /// this_sig_speed : Returns the speed related to the least restrictive aspect (for normal signal)
        /// </summary>

        public SpeedInfo this_sig_speed(SignalFunction fn_type)
        {
            var sigAsp = SignalAspectState.Stop;
            var set_speed = new SpeedInfo(null);

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == fn_type && sigHead.SignalIndicationState >= sigAsp)
                {
                    set_speed = sigHead.SpeedInfoSet[sigHead.SignalIndicationState];
                }
            }
            return set_speed;
        }//this_sig_speed

        //================================================================================================//
        /// <summary>
        /// next_sig_id : returns ident of next signal of required type
        /// </summary>

        public int next_sig_id(int fn_type)
        {
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }

            if (nextSignal >= 0)
            {
                if (fn_type != (int)SignalFunction.Normal)
                {
                    Signal foundSignalObject = signalRef.SignalObjects[nextSignal];
                    if (isSignalNormal())
                    {
                        foundSignalObject.reqNormalSignal = thisRef;
                    }
                    else
                    {
                        foundSignalObject.reqNormalSignal = reqNormalSignal;
                    }
                }

                return (nextSignal);
            }
            else
            {
                return (-1);
            }
        }

        //================================================================================================//
        /// <summary>
        /// next_nsig_id : returns ident of next signal of required type
        /// </summary>

        public int next_nsig_id(int fn_type, int nsignal)
        {
            int nextSignal = thisRef;
            int foundsignal = 0;
            Signal nextSignalObject = this;

            while (foundsignal < nsignal && nextSignal >= 0)
            {
                // use sigfound
                nextSignal = nextSignalObject.sigfound[fn_type];

                // sigfound not set, try direct search
                if (nextSignal < 0)
                {
                    nextSignal = nextSignalObject.SONextSignal(fn_type);
                    nextSignalObject.sigfound[fn_type] = nextSignal;
                }

                // signal found
                if (nextSignal >= 0)
                {
                    foundsignal++;
                    nextSignalObject = signalObjects[nextSignal];
                }
            }

            if (nextSignal >= 0 && foundsignal > 0)
            {
                return (nextSignal);
            }
            else
            {
                return (-1);
            }
        }

        //================================================================================================//
        /// <summary>
        /// opp_sig_id : returns ident of next opposite signal of required type
        /// </summary>

        public int opp_sig_id(int fn_type)
        {
            return (SONextSignalOpp(fn_type));
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_noSpeedReduction : Returns the setting if speed must be reduced on RESTRICTED or STOP_AND_PROCEED
        /// returns TRUE if speed reduction must be suppressed
        /// </summary>

        public bool this_sig_noSpeedReduction(SignalFunction fn_type)
        {
            var sigAsp = SignalAspectState.Stop;
            bool setNoReduction = false;

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == fn_type && sigHead.SignalIndicationState >= sigAsp)
                {
                    sigAsp = sigHead.SignalIndicationState;
                    if (sigAsp <= SignalAspectState.Restricting && sigHead.SpeedInfoSet?[sigAsp] != null)
                    {
                        setNoReduction = sigHead.SpeedInfoSet[sigAsp].LimitedSpeedReduction == 1;
                    }
                    else
                    {
                        setNoReduction = false;
                    }
                }
            }
            return setNoReduction;
        }//this_sig_noSpeedReduction

        //================================================================================================//
        /// <summary>
        /// isRestrictedSpeedPost : Returns TRUE if it is a restricted (temp) speedpost
        /// </summary>

        public int SpeedPostType()
        {
            var sigAsp = SignalAspectState.Clear_2;
            int speedPostType = 0; // default = standard speedpost

            SignalHead sigHead = SignalHeads.First();

            if (sigHead.SpeedInfoSet?[sigAsp] != null)
            {
                speedPostType = sigHead.SpeedInfoSet[sigAsp].LimitedSpeedReduction;

            }
            return speedPostType;

        }//isRestrictedSpeedPost

        //================================================================================================//
        /// <summary>
        /// this_lim_speed : Returns the lowest allowed speed (for speedpost and speed signal)
        /// </summary>

        public SpeedInfo this_lim_speed(SignalFunction fn_type)
        {
            var set_speed = new SpeedInfo(9E9f, 9E9f, false, false, 0);

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == fn_type)
                {
                    SpeedInfo this_speed = sigHead.SpeedInfoSet[sigHead.SignalIndicationState];
                    if (this_speed != null)
                    {
                        if (this_speed.PassengerSpeed > 0 && this_speed.PassengerSpeed < set_speed.PassengerSpeed)
                        {
                            set_speed.PassengerSpeed = this_speed.PassengerSpeed;
                            set_speed.Flag = false;
                            set_speed.Reset = false;
                            if (!isSignal) set_speed.LimitedSpeedReduction = this_speed.LimitedSpeedReduction;
                        }

                        if (this_speed.FreightSpeed > 0 && this_speed.FreightSpeed < set_speed.FreightSpeed)
                        {
                            set_speed.FreightSpeed = this_speed.FreightSpeed;
                            set_speed.Flag = false;
                            set_speed.Reset = false;
                            if (!isSignal) set_speed.LimitedSpeedReduction = this_speed.LimitedSpeedReduction;
                        }
                    }

                }
            }

            if (set_speed.PassengerSpeed > 1E9f)
                set_speed.PassengerSpeed = -1;
            if (set_speed.FreightSpeed > 1E9f)
                set_speed.FreightSpeed = -1;

            return set_speed;
        }//this_lim_speed

        //================================================================================================//
        /// <summary>
        /// store_lvar : store local variable
        /// </summary>

        public void store_lvar(int index, int value)
        {
            if (localStorage.ContainsKey(index))
            {
                localStorage.Remove(index);
            }
            localStorage.Add(index, value);
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_lvar : retrieve variable from this signal
        /// </summary>

        public int this_sig_lvar(int index)
        {
            if (localStorage.ContainsKey(index))
            {
                return (localStorage[index]);
            }
            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_lvar : retrieve variable from next signal
        /// </summary>

        public int next_sig_lvar(int fn_type, int index)
        {
            int nextSignal = sigfound[fn_type];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal(fn_type);
                sigfound[fn_type] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = signalObjects[nextSignal];
                if (nextSignalObject.localStorage.ContainsKey(index))
                {
                    return (nextSignalObject.localStorage[index]);
                }
            }

            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// next_sig_hasnormalsubtype : check if next signal has normal head with required subtype
        /// </summary>

        public int next_sig_hasnormalsubtype(int reqSubtype)
        {
            int nextSignal = sigfound[(int)SignalFunction.Normal];
            if (nextSignal < 0)
            {
                nextSignal = SONextSignal((int)SignalFunction.Normal);
                sigfound[(int)SignalFunction.Normal] = nextSignal;
            }
            if (nextSignal >= 0)
            {
                Signal nextSignalObject = signalObjects[nextSignal];
                return (nextSignalObject.this_sig_hasnormalsubtype(reqSubtype));
            }

            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// this_sig_hasnormalsubtype : check if this signal has normal head with required subtype
        /// </summary>

        public int this_sig_hasnormalsubtype(int reqSubtype)
        {
            foreach (SignalHead thisHead in SignalHeads)
            {
                if (thisHead.SignalFunction == SignalFunction.Normal && thisHead.OrtsNormalSubtypeIndex == reqSubtype)
                {
                    return (1);
                }
            }
            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// switchstand : link signal with next switch and set aspect according to switch state
        /// </summary>

        public int switchstand(int aspect1, int aspect2)
        {
            // if switch index not yet set, find first switch in path
            if (!nextSwitchIndex.HasValue)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[TCReference];
                int sectionDirection = TCDirection;

                bool switchFound = false;

                while (!switchFound)
                {
                    int pinIndex = sectionDirection;

                    if (thisSection.CircuitType == TrackCircuitType.Junction)
                    {
                        if (thisSection.Pins[pinIndex, 1].Link >= 0) // facing point
                        {
                            switchFound = true;
                            nextSwitchIndex = thisSection.Index;
                            if (thisSection.LinkedSignals == null)
                            {
                                thisSection.LinkedSignals = new List<int>();
                                thisSection.LinkedSignals.Add(thisRef);
                            }
                            else if (!thisSection.LinkedSignals.Contains(thisRef))
                            {
                                thisSection.LinkedSignals.Add(thisRef);
                            }
                        }

                    }

                    sectionDirection = thisSection.Pins[pinIndex, 0].Direction;

                    if (thisSection.CircuitType != TrackCircuitType.EndOfTrack && thisSection.Pins[pinIndex, 0].Link >= 0)
                    {
                        thisSection = signalRef.TrackCircuitList[thisSection.Pins[pinIndex, 0].Link];
                    }
                    else
                    {
                        break;
                    }
                }

                if (!switchFound)
                {
                    nextSwitchIndex = -1;
                }
            }

            if (nextSwitchIndex >= 0)
            {
                TrackCircuitSection switchSection = signalRef.TrackCircuitList[nextSwitchIndex.Value];
                return (switchSection.JunctionLastRoute == 0 ? aspect1 : aspect2);
            }

            return (aspect1);
        }

        //================================================================================================//
        /// <summary>
        /// route_set : check if required route is set
        /// </summary>

        public bool route_set(int req_mainnode, uint req_jnnode)
        {
            bool routeset = false;
            bool retry = false;

            // if signal is enabled for a train, check if required section is in train route path

            if (enabledTrain != null && !MPManager.IsMultiPlayer())
            {
                Train.TCSubpathRoute RoutePart = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex];

                TrackNode thisNode = signalRef.trackDB.TrackNodes[req_mainnode];
                if (RoutePart != null)
                {
                    for (int iSection = 0; iSection <= thisNode.TrackCircuitCrossReferences.Count - 1 && !routeset; iSection++)
                    {
                        int sectionIndex = thisNode.TrackCircuitCrossReferences[iSection].Index;

                        for (int iElement = 0; iElement < RoutePart.Count && !routeset; iElement++)
                        {
                            routeset = (sectionIndex == RoutePart[iElement].TCSectionIndex && signalRef.TrackCircuitList[sectionIndex].CircuitType == TrackCircuitType.Normal);
                        }
                    }
                }

                // if not found in trainroute, try signalroute

                if (!routeset && signalRoute != null)
                {
                    for (int iElement = 0; iElement <= signalRoute.Count - 1 && !routeset; iElement++)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[signalRoute[iElement].TCSectionIndex];
                        routeset = (thisSection.OriginalIndex == req_mainnode && thisSection.CircuitType == TrackCircuitType.Normal);
                    }
                }
                retry = !routeset;
            }


            // not enabled, follow set route but only if not normal signal (normal signal will not clear if not enabled)
            // also, for normal enabled signals - try and follow pins (required node may be beyond present route)

            if (retry || !isSignalNormal() || MPManager.IsMultiPlayer())
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[TCReference];
                int curDirection = TCDirection;
                int newDirection = 0;
                int sectionIndex = -1;
                bool passedTrackJn = false;

                List<int> passedSections = new List<int>();
                passedSections.Add(thisSection.Index);

                routeset = (req_mainnode == thisSection.OriginalIndex);
                while (!routeset && thisSection != null)
                {
                    if (thisSection.ActivePins[curDirection, 0].Link >= 0)
                    {
                        newDirection = thisSection.ActivePins[curDirection, 0].Direction;
                        sectionIndex = thisSection.ActivePins[curDirection, 0].Link;
                    }
                    else
                    {
                        newDirection = thisSection.ActivePins[curDirection, 1].Direction;
                        sectionIndex = thisSection.ActivePins[curDirection, 1].Link;
                    }

                    // if Junction, if active pins not set use selected route
                    if (sectionIndex < 0 && thisSection.CircuitType == TrackCircuitType.Junction)
                    {
                        // check if this is required junction
                        if (Convert.ToUInt32(thisSection.Index) == req_jnnode)
                        {
                            passedTrackJn = true;
                        }
                        // break if passed required junction
                        else if (passedTrackJn)
                        {
                            break;
                        }

                        if (thisSection.ActivePins[1, 0].Link == -1 && thisSection.ActivePins[1, 1].Link == -1)
                        {
                            int selectedDirection = (signalRef.trackDB.TrackNodes[thisSection.OriginalIndex] as TrackJunctionNode).SelectedRoute;
                            newDirection = thisSection.Pins[1, selectedDirection].Direction;
                            sectionIndex = thisSection.Pins[1, selectedDirection].Link;
                        }
                    }

                    // if NORMAL, if active pins not set use default pins
                    if (sectionIndex < 0 && thisSection.CircuitType == TrackCircuitType.Normal)
                    {
                        newDirection = thisSection.Pins[curDirection, 0].Direction;
                        sectionIndex = thisSection.Pins[curDirection, 0].Link;
                    }

                    // check for loop
                    if (passedSections.Contains(sectionIndex))
                    {
                        thisSection = null;  // route is looped - exit
                    }

                    // next section
                    else if (sectionIndex >= 0)
                    {
                        passedSections.Add(sectionIndex);
                        thisSection = signalRef.TrackCircuitList[sectionIndex];
                        curDirection = newDirection;
                        routeset = (req_mainnode == thisSection.OriginalIndex && thisSection.CircuitType == TrackCircuitType.Normal);
                    }

                    // no next section
                    else
                    {
                        thisSection = null;
                    }
                }
            }

            return (routeset);
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal of specified type along set sections - not for NORMAL signals
        /// </summary>

        public int SONextSignal(int fntype)
        {
            int thisTC = TCReference;
            Heading direction = (Heading)TCDirection;
            int signalFound = -1;
            TrackCircuitSection thisSection = null;
            bool sectionSet = false;

            // maximise fntype to length of available type list
            int reqtype = Math.Min(fntype, signalRef.ORTSSignalTypeCount);

            // if searching for SPEED signal : check if enabled and use train to find next speedpost
            if (reqtype == (int)SignalFunction.Speed)
            {
                if (enabledTrain != null)
                {
                    signalFound = SONextSignalSpeed(TCReference);
                }
                else
                {
                    return (-1);
                }
            }

            // for normal signals

            else if (reqtype == (int)SignalFunction.Normal)
            {
                if (isSignalNormal())        // if this signal is normal : cannot be done using this route (set through sigfound variable)
                    return (-1);
                signalFound = SONextSignalNormal(TCReference);   // other types of signals (sigfound not used)
            }

        // for other signals : move to next TC (signal would have been default if within same section)

            else
            {
                thisSection = signalRef.TrackCircuitList[thisTC];
                sectionSet = enabledTrain == null ? false : thisSection.IsSet(enabledTrain, false);

                if (sectionSet)
                {
                    int pinIndex = (int)direction;
                    thisTC = thisSection.ActivePins[pinIndex, 0].Link;
                    direction = (Heading)thisSection.ActivePins[pinIndex, 0].Direction;
                }
            }

            // loop through valid sections

            while (sectionSet && thisTC > 0 && signalFound < 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!JunctionsPassed.Contains(thisTC))
                        JunctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(thisRef))
                        thisSection.SignalsPassingRoutes.Add(thisRef);
                }

                // check if required type of signal is along this section

                TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][reqtype];
                if (thisList.Count > 0)
                {
                    signalFound = thisList[0].Signal.thisRef;
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    int pinIndex = (int)direction;
                    sectionSet = thisSection.IsSet(enabledTrain, false);
                    if (sectionSet)
                    {
                        thisTC = thisSection.ActivePins[pinIndex, 0].Link;
                        direction = (Heading)thisSection.ActivePins[pinIndex, 0].Direction;
                        if (thisTC == -1)
                        {
                            thisTC = thisSection.ActivePins[pinIndex, 1].Link;
                            direction = (Heading)thisSection.ActivePins[pinIndex, 1].Direction;
                        }
                    }
                }
            }

            // if signal not found following switches use signal route
            if (signalFound < 0 && signalRoute != null && signalRoute.Count > 0)
            {
                for (int iSection = 0; iSection <= (signalRoute.Count - 1) && signalFound < 0; iSection++)
                {
                    thisSection = signalRef.TrackCircuitList[signalRoute[iSection].TCSectionIndex];
                    direction = (Heading)signalRoute[iSection].Direction;
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.thisRef;
                    }
                }
            }

            // if signal not found, use route from requesting normal signal
            if (signalFound < 0 && reqNormalSignal >= 0)
            {
                Signal refSignal = signalRef.SignalObjects[reqNormalSignal];
                if (refSignal.signalRoute != null && refSignal.signalRoute.Count > 0)
                {
                    int nextSectionIndex = refSignal.signalRoute.GetRouteIndex(TCReference, 0);

                    if (nextSectionIndex >= 0)
                    {
                        for (int iSection = nextSectionIndex+1; iSection <= (refSignal.signalRoute.Count - 1) && signalFound < 0; iSection++)
                        {
                            thisSection = signalRef.TrackCircuitList[refSignal.signalRoute[iSection].TCSectionIndex];
                            direction = (Heading)refSignal.signalRoute[iSection].Direction;
                            TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                            if (thisList.Count > 0)
                            {
                                signalFound = thisList[0].Signal.thisRef;
                            }
                        }
                    }
                }
            }

            return (signalFound);
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal of specified type along set sections - for SPEED signals only
        /// </summary>

        private int SONextSignalSpeed(int thisTC)
        {
            int routeListIndex = enabledTrain.Train.ValidRoute[0].GetRouteIndex(TCReference, enabledTrain.Train.PresentPosition[0].RouteListIndex);

            // signal not in train's route
            if (routeListIndex < 0)
            {
                return (-1);
            }

            // find next speed object
            TrackCircuitSignalItem foundItem = signalRef.Find_Next_Object_InRoute(enabledTrain.Train.ValidRoute[0], routeListIndex, TCOffset, -1, SignalFunction.Speed, enabledTrain);
            if (foundItem.SignalState == SignalItemFindState.Item)
            {
                return (foundItem.Signal.thisRef);
            }
            else
            {
                return (-1);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal of specified type along set sections - NORMAL signals ONLY
        /// </summary>

        private int SONextSignalNormal(int thisTC)
        {
            Heading direction = (Heading)TCDirection;
            int signalFound = -1;
            TrackCircuitSection thisSection = null;

            int pinIndex = (int)direction;

            if (thisTC < 0)
            {
                thisTC = TCReference;
                thisSection = signalRef.TrackCircuitList[thisTC];
                pinIndex = (int)direction;
                thisTC = thisSection.ActivePins[pinIndex, 0].Link;
                direction = (Heading)thisSection.ActivePins[pinIndex, 0].Direction;
            }

            // loop through valid sections

            while (thisTC > 0 && signalFound < 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!JunctionsPassed.Contains(thisTC))
                        JunctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(thisRef))
                        thisSection.SignalsPassingRoutes.Add(thisRef);
                }

                // check if normal signal is along this section

                if (thisSection.EndSignals[direction] != null)
                {
                    signalFound = thisSection.EndSignals[direction].thisRef;
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    pinIndex = (int)direction;
                    thisTC = thisSection.ActivePins[pinIndex, 0].Link;
                    direction = (Heading)thisSection.ActivePins[pinIndex, 0].Direction;
                    if (thisTC == -1)
                    {
                        thisTC = thisSection.ActivePins[pinIndex, 1].Link;
                        direction = (Heading)thisSection.ActivePins[pinIndex, 1].Direction;
                    }

                    // if no active link but signal has route allocated, use train route to find next section

                    if (thisTC == -1 && signalRoute != null)
                    {
                        int thisIndex = signalRoute.GetRouteIndex(thisSection.Index, 0);
                        if (thisIndex >= 0 && thisIndex <= signalRoute.Count - 2)
                        {
                            thisTC = signalRoute[thisIndex + 1].TCSectionIndex;
                            direction = (Heading)signalRoute[thisIndex + 1].Direction;
                        }
                    }
                }
            }

            return (signalFound);
        }

        //================================================================================================//
        /// <summary>
        /// Find next signal in opp direction
        /// </summary>

        public int SONextSignalOpp(int fntype)
        {
            int thisTC = TCReference;
            Heading direction = TCDirection == 0 ? Heading.Reverse : Heading.Ahead;    // reverse direction
            int signalFound = -1;

            TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisTC];
            bool sectionSet = enabledTrain == null ? false : thisSection.IsSet(enabledTrain, false);

            // loop through valid sections

            while (sectionSet && thisTC > 0 && signalFound < 0)
            {
                thisSection = signalRef.TrackCircuitList[thisTC];

                if (thisSection.CircuitType == TrackCircuitType.Junction ||
                    thisSection.CircuitType == TrackCircuitType.Crossover)
                {
                    if (!JunctionsPassed.Contains(thisTC))
                        JunctionsPassed.Add(thisTC);  // set reference to junction section
                    if (!thisSection.SignalsPassingRoutes.Contains(thisRef))
                        thisSection.SignalsPassingRoutes.Add(thisRef);
                }

                // check if required type of signal is along this section

                if (fntype == (int) SignalFunction.Normal)
                {
                    signalFound = thisSection.EndSignals[direction] != null ? thisSection.EndSignals[direction].thisRef : -1;
                }
                else
                {
                    TrackCircuitSignalList thisList = thisSection.CircuitItems.TrackCircuitSignals[direction][fntype];
                    if (thisList.Count > 0)
                    {
                        signalFound = thisList[0].Signal.thisRef;
                    }
                }

                // get next section if active link is set

                if (signalFound < 0)
                {
                    int pinIndex = (int)direction;
                    sectionSet = thisSection.IsSet(enabledTrain, false);
                    if (sectionSet)
                    {
                        thisTC = thisSection.ActivePins[pinIndex, 0].Link;
                        direction = (Heading)thisSection.ActivePins[pinIndex, 0].Direction;
                        if (thisTC == -1)
                        {
                            thisTC = thisSection.ActivePins[pinIndex, 1].Link;
                            direction = (Heading)thisSection.ActivePins[pinIndex, 1].Direction;
                        }
                    }
                }
            }

            return (signalFound);
        }

        //================================================================================================//
        /// <summary>
        /// Perform route check and state update
        /// </summary>

        public void Update()
        {
            // perform route update for normal signals if enabled

            if (isSignalNormal())
            {
                // if in hold, set to most restrictive for each head

                if (holdState != HoldState.None)
                {
                    foreach (SignalHead sigHead in SignalHeads)
                    {
                        if (holdState == HoldState.ManualLock || holdState == HoldState.StationStop) sigHead.SetMostRestrictiveAspect();
                    }
                    return;
                }

                // if enabled - perform full update and propagate if not yet done

                if (enabledTrain != null)
                {
                    // if internal state is not reserved (route fully claimed), perform route check

                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        checkRouteState(isPropagated, signalRoute, enabledTrain);
                    }

                    // propagate request

                    if (!isPropagated)
                    {
                        propagateRequest();
                    }

                    StateUpdate();

                    // propagate request if not yet done

                    if (!propagated && enabledTrain != null)
                    {
                        propagateRequest();
                    }
                }

        // fixed route - check route and update

                else if (hasFixedRoute)
                {
                    // if internal state is not reserved (route fully claimed), perform route check

                    if (internalBlockState != InternalBlockstate.Reserved)
                    {
                        checkRouteState(true, fixedRoute, null);
                    }

                    StateUpdate();

                }

        // no route - perform update only

                else
                {
                    StateUpdate();
                }

            }

        // check blockstate for other signals

            else
            {
                getBlockState_notRouted();
                StateUpdate();
            }
        }

        //================================================================================================//
        /// <summary>
        /// fully reset signal as train has passed
        /// </summary>

        public void resetSignalEnabled()
        {
            // reset train information

            enabledTrain = null;
            trainRouteDirectionIndex = 0;
            signalRoute.Clear();
            fullRoute = hasFixedRoute;
            thisTrainRouteIndex = -1;

            isPropagated = false;
            propagated = false;
            ForcePropagation = false;
            ApproachControlCleared = false;
            ApproachControlSet = false;
            ClaimLocked = false;
            ForcePropOnApproachControl = false;

            // reset block state to most restrictive

            internalBlockState = InternalBlockstate.Blocked;

            // reset next signal information to default

            for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
            {
                sigfound[fntype] = defaultNextSignal[fntype];
            }

            foreach (int thisSectionIndex in JunctionsPassed)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];
                thisSection.SignalsPassingRoutes.Remove(thisRef);
            }

            // reset permission //

            hasPermission = Permission.Denied;

            StateUpdate();
        }

        //================================================================================================//
        /// <summary>
        /// Perform the update for each head on this signal to determine state of signal.
        /// </summary>

        public void StateUpdate()
        {
            // reset approach control (must be explicitly reset as test in script may be conditional)
            ApproachControlSet = false;

            // update all normal heads first

            if (MPManager.IsMultiPlayer())
            {
                if (MPManager.IsClient()) return; //client won't handle signal update

                //if there were hold manually, will not update
                if (holdState == HoldState.ManualApproach || holdState == HoldState.ManualLock || holdState == HoldState.ManualPass) return;
            }

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction == SignalFunction.Normal)
                    sigHead.Update();
            }

            // next, update all other heads

            foreach (SignalHead sigHead in SignalHeads)
            {
                if (sigHead.SignalFunction != SignalFunction.Normal)
                    sigHead.Update();
            }

        } // Update

        //================================================================================================//
        /// <summary>
        /// Returns the distance from the TDBtraveller to this signal. 
        /// </summary>

        public float DistanceTo(Traveller tdbTraveller)
        {
            int trItem = (trackNodes[trackNode] as TrackVectorNode).TrackItemIndices[trRefIndex];
            return tdbTraveller.DistanceTo(trItems[trItem].Location);
        }//DistanceTo

        //================================================================================================//
        /// <summary>
        /// Returns the distance from this object to the next object
        /// </summary>

        public float ObjectDistance(Signal nextObject)
        {
            int nextTrItem = (trackNodes[nextObject.trackNode] as TrackVectorNode).TrackItemIndices[nextObject.trRefIndex];
            return this.tdbtraveller.DistanceTo(trItems[nextTrItem].Location);
        }//ObjectDistance

        //================================================================================================//
        /// <summary>
        /// Check whether signal head is for this signal.
        /// </summary>

        public bool isSignalHead(SignalItem signalItem)
        {
            // Tritem for this signal
            SignalItem thisSignalItem = (SignalItem)trItems[this.trItem];
            // Same Tile
            if (signalItem.Location.TileX == thisSignalItem.Location.TileX && signalItem.Location.TileZ == thisSignalItem.Location.TileZ)
            {
                // Same position
                if ((Math.Abs(signalItem.Location.Location.X - thisSignalItem.Location.Location.X) < 0.01) &&
                    (Math.Abs(signalItem.Location.Location.Y - thisSignalItem.Location.Location.Y) < 0.01) &&
                    (Math.Abs(signalItem.Location.Location.Z - thisSignalItem.Location.Location.Z) < 0.01))
                {
                    return true;
                }
            }
            return false;
        }//isSignalHead

        //================================================================================================//
        /// <summary>
        /// Adds a head to this signal (for signam).
        /// </summary>

        public void AddHead(int trItem, int TDBRef, SignalItem sigItem)
        {
            // create SignalHead
            SignalHead head = new SignalHead(this, trItem, TDBRef, sigItem);

            // set junction link
            if (head.TrackJunctionNode != 0)
            {
                if (head.JunctionPath == 0)
                {
                    head.JunctionMainNode =
                       trackNodes[head.TrackJunctionNode].TrackPins[trackNodes[head.TrackJunctionNode].InPins].Link;
                }
                else
                {
                    head.JunctionMainNode =
                       trackNodes[head.TrackJunctionNode].TrackPins[trackNodes[head.TrackJunctionNode].InPins + 1].Link;
                }
            }
            SignalHeads.Add(head);

        }//AddHead (signal)

        //================================================================================================//
        /// <summary>
        /// Adds a head to this signal (for speedpost).
        /// </summary>

        public void AddHead(int trItem, int TDBRef, SpeedPostItem speedItem)
        {
            // create SignalHead
            SignalHead head = new SignalHead(this, trItem, TDBRef, speedItem);
            SignalHeads.Add(head);

        }//AddHead (speedpost)

        //================================================================================================//
        /// <summary>
        /// Sets the signal type from the sigcfg file for each signal head.
        /// </summary>

        public void SetSignalType(SignalConfigurationFile sigCFG)
        {
            foreach (SignalHead sigHead in SignalHeads)
            {
                sigHead.SetSignalType(trItems, sigCFG);
            }
        }//SetSignalType

        //================================================================================================//
        /// <summary>
        /// Gets the display aspect for the track monitor.
        /// </summary>

        public TrackMonitorSignalAspect TranslateTMAspect(SignalAspectState SigState)
        {
            switch (SigState)
            {
                case SignalAspectState.Stop:
                    if (hasPermission == Permission.Granted)
                        return TrackMonitorSignalAspect.Permission;
                    else
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
        /// request to clear signal in explorer mode
        /// </summary>

        public Train.TCSubpathRoute requestClearSignalExplorer(Train.TCSubpathRoute thisRoute,
            Train.TrainRouted thisTrain, bool propagated, int signalNumClearAhead)
        {
            // build output route from input route
            Train.TCSubpathRoute newRoute = new Train.TCSubpathRoute(thisRoute);

            // if signal has fixed route, use that else build route
            if (fixedRoute != null && fixedRoute.Count > 0)
            {
                signalRoute = new Train.TCSubpathRoute(fixedRoute);
            }

            // build route from signal, upto next signal or max distance, take into account manual switch settings
            else
            {
                List<int> nextRoute = signalRef.ScanRoute(thisTrain.Train, TCNextTC, 0.0f, TCNextDirection, true, -1, true, true, true, false,
                true, false, false, false, false, thisTrain.Train.IsFreight);

                signalRoute = new Train.TCSubpathRoute();

                foreach (int sectionIndex in nextRoute)
                {
                    Train.TCRouteElement thisElement = new Train.TCRouteElement(Math.Abs(sectionIndex), sectionIndex >= 0 ? 0 : 1);
                    signalRoute.Add(thisElement);
                }
            }

            // set full route if route ends with signal
            TrackCircuitSection lastSection = signalRef.TrackCircuitList[signalRoute[signalRoute.Count - 1].TCSectionIndex];
            Heading lastDirection = (Heading)signalRoute[signalRoute.Count - 1].Direction;

            if (lastSection.EndSignals[lastDirection] != null)
            {
                fullRoute = true;
                sigfound[(int)SignalFunction.Normal] = lastSection.EndSignals[lastDirection].thisRef;
            }

            // try and clear signal

            enabledTrain = thisTrain;
            checkRouteState(propagated, signalRoute, thisTrain);

            // extend route if block is clear or permission is granted, even if signal is not cleared (signal state may depend on next signal)
            bool extendRoute = false;
            if (this_sig_lr(SignalFunction.Normal) > SignalAspectState.Stop) extendRoute = true;
            if (internalBlockState <= InternalBlockstate.Reservable) extendRoute = true;

            // if signal is cleared or permission is granted, extend route with signal route

            if (extendRoute || hasPermission == Permission.Granted)
            {
                foreach (Train.TCRouteElement thisElement in signalRoute)
                {
                    newRoute.Add(thisElement);
                }
            }

            // if signal is cleared, propagate request if required
            if (extendRoute && fullRoute)
            {
                isPropagated = propagated;
                int ReqNumClearAhead = 0;

                if (SignalNumClearAhead_MSTS > -2)
                {
                    ReqNumClearAhead = propagated ?
                        signalNumClearAhead - SignalNumNormalHeads : SignalNumClearAhead_MSTS - SignalNumNormalHeads;
                }
                else
                {
                    if (SignalNumClearAheadActive == -1)
                    {
                        ReqNumClearAhead = propagated ? signalNumClearAhead : 1;
                    }
                    else if (SignalNumClearAheadActive == 0)
                    {
                        ReqNumClearAhead = 0;
                    }
                    else
                    {
                        ReqNumClearAhead = isPropagated ? signalNumClearAhead - 1 : SignalNumClearAheadActive - 1;
                    }
                }


                if (ReqNumClearAhead > 0)
                {
                    int nextSignalIndex = sigfound[(int)SignalFunction.Normal];
                    if (nextSignalIndex >= 0)
                    {
                        Signal nextSignal = signalObjects[nextSignalIndex];
                        newRoute = nextSignal.requestClearSignalExplorer(newRoute, thisTrain, true, ReqNumClearAhead);
                    }
                }
            }

            return (newRoute);
        }
        //================================================================================================//
        /// <summary>
        /// request to clear signal
        /// </summary>

        public bool requestClearSignal(Train.TCSubpathRoute RoutePart, Train.TrainRouted thisTrain,
                        int clearNextSignals, bool requestIsPropagated, Signal lastSignal)
        {

#if DEBUG_REPORTS
			File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Request for clear signal from train {0} at section {1} for signal {2}\n",
				thisTrain.Train.Number,
				thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex,
				thisRef));
#endif
            if (thisTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Request for clear signal from train {0} at section {1} for signal {2}\n",
                    thisTrain.Train.Number,
                    thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex,
                    thisRef));
            }

            // set general variables
            int foundFirstSection = -1;
            int foundLastSection = -1;
            Signal nextSignal = null;

            isPropagated = requestIsPropagated;
            propagated = false;   // always pass on request

            // check if signal not yet enabled - if it is, give warning and quit

            // check if signal not yet enabled - if it is, give warning, reset signal and set both trains to node control, and quit

            if (enabledTrain != null && enabledTrain != thisTrain)
            {
                Trace.TraceWarning("Request to clear signal {0} from train {1}, signal already enabled for train {2}",
                                       thisRef, thisTrain.Train.Name, enabledTrain.Train.Name);
                Train.TrainRouted otherTrain = enabledTrain;
                ResetSignal(true);
                int routeListIndex = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].RouteListIndex;
                signalRef.BreakDownRouteList(thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex], routeListIndex, thisTrain);
                routeListIndex = otherTrain.Train.PresentPosition[otherTrain.TrainRouteDirectionIndex].RouteListIndex;
                signalRef.BreakDownRouteList(otherTrain.Train.ValidRoute[otherTrain.TrainRouteDirectionIndex], routeListIndex, otherTrain);

                thisTrain.Train.SwitchToNodeControl(thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].TCSectionIndex);
                if (otherTrain.Train.ControlMode != Train.TRAIN_CONTROL.EXPLORER && !otherTrain.Train.IsPathless) otherTrain.Train.SwitchToNodeControl(otherTrain.Train.PresentPosition[otherTrain.TrainRouteDirectionIndex].TCSectionIndex);
                return false;
            }
            if (thisTrain.Train.TCRoute != null && HasLockForTrain(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath))
            {
                return false;
            }
            if (enabledTrain != thisTrain) // new allocation - reset next signals
            {
                for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
                {
                    sigfound[fntype] = defaultNextSignal[fntype];
                }
            }
            enabledTrain = thisTrain;

            // find section in route part which follows signal

            signalRoute.Clear();

            int firstIndex = -1;
            if (lastSignal != null)
            {
                firstIndex = lastSignal.thisTrainRouteIndex;
            }
            if (firstIndex < 0)
            {
                firstIndex = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex].RouteListIndex;
            }

            if (firstIndex >= 0)
            {
                for (int iNode = firstIndex;
                         iNode < RoutePart.Count && foundFirstSection < 0;
                         iNode++)
                {
                    Train.TCRouteElement thisElement = RoutePart[iNode];
                    if (thisElement.TCSectionIndex == TCNextTC)
                    {
                        foundFirstSection = iNode;
                        thisTrainRouteIndex = iNode;
                    }
                }
            }

            if (foundFirstSection < 0)
            {
                enabledTrain = null;

                // if signal on holding list, set hold state
                if (thisTrain.Train.HoldingSignals.Contains(thisRef) && holdState == HoldState.None)
                {
                    holdState = HoldState.StationStop;
                }
                return false;
            }

            // copy sections upto next normal signal
            // check for loop

            List<int> sectionsInRoute = new List<int>();

            for (int iNode = foundFirstSection; iNode < RoutePart.Count && foundLastSection < 0; iNode++)
            {
                Train.TCRouteElement thisElement = RoutePart[iNode];
                if (sectionsInRoute.Contains(thisElement.TCSectionIndex))
                {
                    foundLastSection = iNode;  // loop
                }
                else
                {
                    signalRoute.Add(thisElement);
                    sectionsInRoute.Add(thisElement.TCSectionIndex);

                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];

                    // exit if section is pool access section (signal will clear on new route on next try)
                    // reset train details to force new signal clear request
                    // check also creates new full train route
                    // applies to timetable mode only
                    if (thisTrain.Train.CheckPoolAccess(thisSection.Index))
                    {
                        enabledTrain = null;
                        signalRoute.Clear();

                        if (thisTrain.Train.CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                String.Format("Reset signal for pool access : {0} \n", thisRef));
                        }

                        return false;
                    }

                    // check if section has end signal - if so is last section
                    if (thisSection.EndSignals[(Heading)thisElement.Direction] != null)
                    {
                        foundLastSection = iNode;
                        nextSignal = thisSection.EndSignals[(Heading)thisElement.Direction];
                    }
                }
            }

            // check if signal has route, is enabled, request is by enabled train and train is not occupying sections in signal route

            if (enabledTrain != null && enabledTrain == thisTrain && signalRoute != null && signalRoute.Count > 0)
            {
                foreach (Train.TCRouteElement routeElement in signalRoute)
                {
                    TrackCircuitSection routeSection = signalRef.TrackCircuitList[routeElement.TCSectionIndex];
                    if (routeSection.CircuitState.OccupiedByThisTrain(thisTrain))
                    {
                        return false;  // train has passed signal - clear request is invalid
                    }
                }
            }

            // check if end of track reached

            Train.TCRouteElement lastSignalElement = signalRoute[signalRoute.Count - 1];
            TrackCircuitSection lastSignalSection = signalRef.TrackCircuitList[lastSignalElement.TCSectionIndex];

            fullRoute = true;

            // if end of signal route is not a signal or end-of-track it is not a full route

            if (nextSignal == null && lastSignalSection.CircuitType != TrackCircuitType.EndOfTrack)
            {
                fullRoute = false;
            }

            // if next signal is found and relevant, set reference

            if (nextSignal != null)
            {
                sigfound[(int)SignalFunction.Normal] = nextSignal.thisRef;
            }
            else
            {
                sigfound[(int)SignalFunction.Normal] = -1;
            }

            // set number of signals to clear ahead

            if (SignalNumClearAhead_MSTS > -2)
            {
                ReqNumClearAhead = clearNextSignals > 0 ?
                    clearNextSignals - SignalNumNormalHeads : SignalNumClearAhead_MSTS - SignalNumNormalHeads;
            }
            else
            {
                if (SignalNumClearAheadActive == -1)
                {
                    ReqNumClearAhead = clearNextSignals > 0 ? clearNextSignals : 1;
                }
                else if (SignalNumClearAheadActive == 0)
                {
                    ReqNumClearAhead = 0;
                }
                else
                {
                    ReqNumClearAhead = clearNextSignals > 0 ? clearNextSignals - 1 : SignalNumClearAheadActive - 1;
                }
            }

            // perform route check

            checkRouteState(isPropagated, signalRoute, thisTrain);

            // propagate request

            if (!isPropagated && enabledTrain != null)
            {
                propagateRequest();
            }
            if (thisTrain != null && thisTrain.Train is AITrain && Math.Abs(thisTrain.Train.SpeedMpS) <= Simulator.MaxStoppedMpS)
            {
                WorldLocation location = this.tdbtraveller.WorldLocation;
                ((AITrain)thisTrain.Train).AuxActionsContain.CheckGenActions(this.GetType(), location, 0f, 0f, this.tdbtraveller.TrackNodeIndex);
            }

            return (this_sig_mr(SignalFunction.Normal) != SignalAspectState.Stop);
        }

        //================================================================================================//
        /// <summary>
        /// check and update Route State
        /// </summary>

        public void checkRouteState(bool isPropagated, Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain, bool sound = true)
        {
            // check if signal must be hold
            bool signalHold = (holdState != HoldState.None);
            if (enabledTrain != null && enabledTrain.Train.HoldingSignals.Contains(thisRef) && holdState < HoldState.ManualLock)
            {
                holdState = HoldState.StationStop;
                signalHold = true;
            }
            else if (holdState == HoldState.StationStop)
            {
                if (enabledTrain == null || !enabledTrain.Train.HoldingSignals.Contains(thisRef))
                {
                    holdState = HoldState.None;
                    signalHold = false;
                }
            }

            // check if signal has route, is enabled, request is by enabled train and train is not occupying sections in signal route

            if (enabledTrain != null && enabledTrain == thisTrain && signalRoute != null && signalRoute.Count > 0)
            {
                var forcedRouteElementIndex = -1;
                foreach (Train.TCRouteElement routeElement in signalRoute)
                {
                    TrackCircuitSection routeSection = signalRef.TrackCircuitList[routeElement.TCSectionIndex];
                    if (routeSection.CircuitState.OccupiedByThisTrain(thisTrain))
                    {
                        return;  // train has passed signal - clear request is invalid
                    }
                    if (routeSection.CircuitState.Forced)
                    {
                        // route must be recomputed after switch moved by dispatcher
                        forcedRouteElementIndex = signalRoute.IndexOf(routeElement);
                        break;
                    }
                }
                if (forcedRouteElementIndex >= 0)
                {
                    int forcedTCSectionIndex = signalRoute[forcedRouteElementIndex].TCSectionIndex;
                    TrackCircuitSection forcedTrackSection = signalRef.TrackCircuitList[forcedTCSectionIndex];
                    int forcedRouteSectionIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(forcedTCSectionIndex, 0);
                    thisTrain.Train.ReRouteTrain(forcedRouteSectionIndex, forcedTCSectionIndex);
                    if (thisTrain.Train.TrainType == Train.TRAINTYPE.AI || thisTrain.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
                        (thisTrain.Train as AITrain).ResetActions(true);
                    forcedTrackSection.CircuitState.Forced = false;
                }
            }

            // test if propagate state still correct - if next signal for enabled train is this signal, it is not propagated

            if (enabledTrain != null && enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] != null &&
                enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef == thisRef)
            {
                isPropagated = false;
            }

            // test clearance for full route section

            if (!signalHold)
            {
                if (fullRoute)
                {
                    bool newroute = getBlockState(thisRoute, thisTrain, !sound);
                    if (newroute)
                        thisRoute = this.signalRoute;
                }

                // test clearance for sections in route only if first signal ahead of train or if clearance unto partial route is allowed

                else if (enabledTrain != null && (!isPropagated || AllowPartRoute) && thisRoute.Count > 0)
                {
                    getPartBlockState(thisRoute);
                }

                // test clearance for sections in route if signal is second signal ahead of train, first signal route is clear but first signal is still showing STOP
                // case for double-hold signals

                else if (enabledTrain != null && isPropagated)
                {
                    Signal firstSignal = enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex];
                    if (firstSignal != null &&
                        firstSignal.sigfound[(int)SignalFunction.Normal] == thisRef &&
                        firstSignal.internalBlockState <= InternalBlockstate.Reservable &&
                        firstSignal.this_sig_lr(SignalFunction.Normal) == SignalAspectState.Stop)
                    {
                        getPartBlockState(thisRoute);
                    }
                }
            }

            // else consider route blocked

            else
            {
                internalBlockState = InternalBlockstate.Blocked;
            }

            // derive signal state

            StateUpdate();
            SignalAspectState signalState = this_sig_lr(SignalFunction.Normal);

            float lengthReserved = 0.0f;

            // check for permission

            if (internalBlockState == InternalBlockstate.OccupiedSameDirection && hasPermission == Permission.Requested && !isPropagated)
            {
                hasPermission = Permission.Granted;
                if (sound) signalRef.Simulator.SoundNotify = TrainEvent.PermissionGranted;
            }
            else
            {
                if (enabledTrain != null && enabledTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL &&
                    internalBlockState <= InternalBlockstate.OccupiedSameDirection && hasPermission == Permission.Requested)
                {
                    signalRef.Simulator.SoundNotify = TrainEvent.PermissionGranted;
                }
                else if (hasPermission == Permission.Requested)
                {
                    if (sound) signalRef.Simulator.SoundNotify = TrainEvent.PermissionDenied;
                }

                if (enabledTrain != null && enabledTrain.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL && signalState == SignalAspectState.Stop &&
                internalBlockState <= InternalBlockstate.OccupiedSameDirection && hasPermission == Permission.Requested)
                {
                    hasPermission = Permission.Granted;
                }
                else if (hasPermission == Permission.Requested)
                {
                    hasPermission = Permission.Denied;
                }
            }

            // reserve full section if allowed, do not set reserved if signal is held on approach control

            if (enabledTrain != null)
            {
                if (internalBlockState == InternalBlockstate.Reservable && !ApproachControlSet)
                {
                    internalBlockState = InternalBlockstate.Reserved; // preset all sections are reserved

                    foreach (Train.TCRouteElement thisElement in thisRoute)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.CircuitState.TrainReserved != null || thisSection.CircuitState.OccupationState.Count > 0)
                        {
                            if (thisSection.CircuitState.TrainReserved != thisTrain)
                            {
                                internalBlockState = InternalBlockstate.Reservable; // not all sections are reserved // 
                                break;
                            }
                        }
                        thisSection.Reserve(enabledTrain, thisRoute);
                        enabledTrain.Train.LastReservedSection[enabledTrain.TrainRouteDirectionIndex] = thisElement.TCSectionIndex;
                        lengthReserved += thisSection.Length;
                    }

                    enabledTrain.Train.ClaimState = false;
                }

            // reserve partial sections if signal clears on occupied track or permission is granted

                else if ((signalState > SignalAspectState.Stop || hasPermission == Permission.Granted) &&
                         (internalBlockState != InternalBlockstate.Reserved && internalBlockState < InternalBlockstate.ReservedOther))
                {

                    // reserve upto available section

                    int lastSectionIndex = 0;
                    bool reservable = true;

                    for (int iSection = 0; iSection < thisRoute.Count && reservable; iSection++)
                    {
                        Train.TCRouteElement thisElement = thisRoute[iSection];
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];

                        if (thisSection.IsAvailable(enabledTrain))
                        {
                            if (thisSection.CircuitState.TrainReserved == null)
                            {
                                thisSection.Reserve(enabledTrain, thisRoute);
                            }
                            enabledTrain.Train.LastReservedSection[enabledTrain.TrainRouteDirectionIndex] = thisElement.TCSectionIndex;
                            lastSectionIndex = iSection;
                            lengthReserved += thisSection.Length;
                        }
                        else
                        {
                            reservable = false;
                        }
                    }

                    // set pre-reserved or reserved for all other sections

                    for (int iSection = lastSectionIndex++; iSection < thisRoute.Count && reservable; iSection++)
                    {
                        Train.TCRouteElement thisElement = thisRoute[iSection];
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];

                        if (thisSection.IsAvailable(enabledTrain) && thisSection.CircuitState.TrainReserved == null)
                        {
                            thisSection.Reserve(enabledTrain, thisRoute);
                        }
                        else if (thisSection.CircuitState.OccupiedByOtherTrains(enabledTrain))
                        {
                            thisSection.PreReserve(enabledTrain);
                        }
                        else if (thisSection.CircuitState.TrainReserved == null || thisSection.CircuitState.TrainReserved.Train != enabledTrain.Train)
                        {
                            thisSection.PreReserve(enabledTrain);
                        }
                        else
                        {
                            reservable = false;
                        }
                    }
                    enabledTrain.Train.ClaimState = false;
                }

            // if claim allowed - reserve free sections and claim all other if first signal ahead of train

                else if (enabledTrain.Train.ClaimState && internalBlockState != InternalBlockstate.Reserved &&
                         enabledTrain.Train.NextSignalObject[0] != null && enabledTrain.Train.NextSignalObject[0].thisRef == thisRef)
                {
                    foreach (Train.TCRouteElement thisElement in thisRoute)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.DeadlockReference > 0) // do not claim into deadlock area as path may not have been resolved
                        {
                            break;
                        }

                        if (thisSection.CircuitState.TrainReserved == null || (thisSection.CircuitState.TrainReserved.Train != enabledTrain.Train))
                        {
                            // deadlock has been set since signal request was issued - reject claim, break and reset claimstate
                            if (thisSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number))
                            {
                                thisTrain.Train.ClaimState = false;
                                break;
                            }

                            // claim only if signal claim is not locked (in case of approach control)
                            if (!ClaimLocked)
                            {
                                thisSection.Claim(enabledTrain);
                            }
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// propagate clearance request
        /// </summary>

        private void propagateRequest()
        {
            // no. of next signals to clear : as passed on -1 if signal has normal clear ahead
            // if passed on < 0, use this signals num to clear

            // sections not available
            bool validPropagationRequest = true;
            if (internalBlockState > InternalBlockstate.Reservable)
            {
                validPropagationRequest = false;
            }

            // sections not reserved and no forced propagation
            if (!ForcePropagation && !ForcePropOnApproachControl && internalBlockState > InternalBlockstate.Reserved)
            {
                validPropagationRequest = false;
            }

            // route is not fully available so do not propagate
            if (!validPropagationRequest)
            {
                return;
            }

            Signal nextSignal = null;
            if (sigfound[(int)SignalFunction.Normal] >= 0)
            {
                nextSignal = signalObjects[sigfound[(int)SignalFunction.Normal]];
            }

            Train.TCSubpathRoute RoutePart;
            if (enabledTrain != null)
            {
                RoutePart = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex];   // if known which route to use
            }
            else
            {
                RoutePart = signalRoute; // else use signal route
            }

            bool propagateState = true;  // normal propagate state

            // update ReqNumClearAhead if signal is not propagated (only when SignamNumClearAheadActive has other than default value)

            if (!isPropagated)
            {
                // set number of signals to clear ahead

                if (SignalNumClearAhead_MSTS <= -2 && SignalNumClearAheadActive != SignalNumClearAhead_ORTS)
                {
                    if (SignalNumClearAheadActive == 0)
                    {
                        ReqNumClearAhead = 0;
                    }
                    else if (SignalNumClearAheadActive > 0)
                    {
                        ReqNumClearAhead = SignalNumClearAheadActive - 1;
                    }
                    else if (SignalNumClearAheadActive < 0)
                    {
                        ReqNumClearAhead = 1;
                    }
                }
            }

            bool validBlockState = internalBlockState <= InternalBlockstate.Reserved;

            // for approach control, use reservable state instead of reserved state (sections are not reserved on approach control)
            // also on forced propagation, use reservable state instead of reserved state
            if (ApproachControlSet && ForcePropOnApproachControl)
            {
                validBlockState = internalBlockState <= InternalBlockstate.Reservable;
            }

            // if section is clear but signal remains at stop - dual signal situation - do not treat as propagate
            if (validBlockState && this_sig_lr(SignalFunction.Normal) == SignalAspectState.Stop && isSignalNormal())
            {
                propagateState = false;
            }

            if ((ReqNumClearAhead > 0 || ForcePropagation) && nextSignal != null && validBlockState && (!ApproachControlSet || ForcePropOnApproachControl))
            {
                nextSignal.requestClearSignal(RoutePart, enabledTrain, ReqNumClearAhead, propagateState, this);
                propagated = true;
                ForcePropagation = false;
            }

            // check if next signal is cleared by default (state != stop and enabled == false) - if so, set train as enabled train but only if train's route covers signal route

            if (nextSignal != null && nextSignal.this_sig_lr(SignalFunction.Normal) >= SignalAspectState.Approach_1 && nextSignal.hasFixedRoute && !nextSignal.enabled && enabledTrain != null)
            {
                int firstSectionIndex = nextSignal.fixedRoute.First().TCSectionIndex;
                int lastSectionIndex = nextSignal.fixedRoute.Last().TCSectionIndex;
                int firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                int lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                {
                    nextSignal.requestClearSignal(nextSignal.fixedRoute, enabledTrain, 0, true, null);

                    int furtherSignalIndex = nextSignal.sigfound[(int)SignalFunction.Normal];
                    int furtherSignalsToClear = ReqNumClearAhead - 1;

                    while (furtherSignalIndex >= 0)
                    {
                        Signal furtherSignal = signalRef.SignalObjects[furtherSignalIndex];
                        if (furtherSignal.this_sig_lr(SignalFunction.Normal) >= SignalAspectState.Approach_1 && !furtherSignal.enabled && furtherSignal.hasFixedRoute)
                        {
                            firstSectionIndex = furtherSignal.fixedRoute.First().TCSectionIndex;
                            lastSectionIndex = furtherSignal.fixedRoute.Last().TCSectionIndex;
                            firstSectionRouteIndex = RoutePart.GetRouteIndex(firstSectionIndex, 0);
                            lastSectionRouteIndex = RoutePart.GetRouteIndex(lastSectionIndex, 0);

                            if (firstSectionRouteIndex >= 0 && lastSectionRouteIndex >= 0)
                            {
                                furtherSignal.requestClearSignal(furtherSignal.fixedRoute, enabledTrain, 0, true, null);

                                furtherSignal.isPropagated = true;
                                furtherSignalsToClear = furtherSignalsToClear > 0 ? furtherSignalsToClear - 1 : 0;
                                furtherSignal.ReqNumClearAhead = furtherSignalsToClear;
                                furtherSignalIndex = furtherSignal.sigfound[(int)SignalFunction.Normal];
                            }
                            else
                            {
                                furtherSignalIndex = -1;
                            }
                        }
                        else
                        {
                            furtherSignalIndex = -1;
                        }
                    }
                }
            }

        } //propagateRequest

        //================================================================================================//
        /// <summary>
        /// get block state - not routed
        /// Check blockstate for normal signal which is not enabled
        /// Check blockstate for other types of signals
        /// <summary>

        private void getBlockState_notRouted()
        {

            InternalBlockstate localBlockState = InternalBlockstate.Reserved; // preset to lowest option

            // check fixed route for normal signals

            if (isSignalNormal() && hasFixedRoute)
            {
                foreach (Train.TCRouteElement thisElement in fixedRoute)
                {
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                    if (thisSection.CircuitState.Occupied())
                    {
                        localBlockState = InternalBlockstate.OccupiedSameDirection;
                    }
                }
            }

        // otherwise follow sections upto first non-set switch or next signal
            else
            {
                int thisTC = TCReference;
                Heading direction = (Heading)TCDirection;
                int nextTC = -1;

                // for normal signals : start at next TC

                if (TCNextTC > 0)
                {
                    thisTC = TCNextTC;
                    direction = (Heading)TCNextDirection;
                }

                // get trackcircuit

                TrackCircuitSection thisSection = null;
                if (thisTC > 0)
                {
                    thisSection = signalRef.TrackCircuitList[thisTC];
                }

                // loop through valid sections

                while (thisSection != null)
                {

                    // set blockstate

                    if (thisSection.CircuitState.Occupied())
                    {
                        if (thisSection.Index == TCReference)  // for section where signal is placed, check if train is ahead
                        {
                            Dictionary<Train, float> trainAhead =
                                                    thisSection.TestTrainAhead(null, TCOffset, TCDirection);
                            if (trainAhead.Count > 0)
                                localBlockState = InternalBlockstate.OccupiedSameDirection;
                        }
                        else
                        {
                            localBlockState = InternalBlockstate.OccupiedSameDirection;
                        }
                    }

                    // if section has signal at end stop check

                    if (thisSection.EndSignals[direction] != null)
                    {
                        thisSection = null;
                    }

        // get next section if active link is set

                    else
                    {
                        //                     int pinIndex = direction == 0 ? 1 : 0;
                        int pinIndex = (int)direction;
                        nextTC = thisSection.ActivePins[pinIndex, 0].Link;
                        direction = (Heading)thisSection.ActivePins[pinIndex, 0].Direction;
                        if (nextTC == -1)
                        {
                            nextTC = thisSection.ActivePins[pinIndex, 1].Link;
                            direction = (Heading)thisSection.ActivePins[pinIndex, 1].Direction;
                        }

                        // set state to blocked if ending at unset or unaligned switch

                        if (nextTC >= 0)
                        {
                            thisSection = signalRef.TrackCircuitList[nextTC];
                        }
                        else
                        {
                            thisSection = null;
                            localBlockState = InternalBlockstate.Blocked;
                        }
                    }
                }
            }

            internalBlockState = localBlockState;
        }

        //================================================================================================//
        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// </summary>

        private bool getBlockState(Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain, bool AIPermissionRequest)
        {
            if (signalRef.UseLocationPassingPaths)
            {
                return (getBlockState_locationBased(thisRoute, thisTrain, AIPermissionRequest));
            }
            else
            {
                return (getBlockState_pathBased(thisRoute, thisTrain));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// based on path-based deadlock processing
        /// </summary>

        private bool getBlockState_pathBased(Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain)
        {
            bool returnvalue = false;

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list

            Train.TCRouteElement lastElement = null;

            foreach (Train.TCRouteElement thisElement in thisRoute)
            {
                lastElement = thisElement;
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                int direction = thisElement.Direction;
                blockstate = thisSection.GetSectionState(enabledTrain, direction, blockstate, thisRoute, thisRef);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //

                // if alternative path from section available but train already waiting for deadlock, set blocked
                if (thisElement.StartAlternativePath != null)
                {
                    TrackCircuitSection endSection = signalRef.TrackCircuitList[thisElement.StartAlternativePath[1]];
                    if (endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                    {
                        blockstate = InternalBlockstate.Blocked;
                        lastElement = thisElement;
                        break;
                    }
                }
            }

            // check if alternative route available

            int lastElementIndex = thisRoute.GetRouteIndex(lastElement.TCSectionIndex, 0);

            if (blockstate > InternalBlockstate.Reservable && thisTrain != null)
            {
                int startAlternativeRoute = -1;
                int altRoute = -1;

                Train.TCSubpathRoute trainRoute = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex];
                Train.TCPosition thisPosition = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex];

                for (int iElement = lastElementIndex; iElement >= 0; iElement--)
                {
                    Train.TCRouteElement prevElement = thisRoute[iElement];
                    if (prevElement.StartAlternativePath != null)
                    {
                        startAlternativeRoute =
                            trainRoute.GetRouteIndex(thisRoute[iElement].TCSectionIndex, thisPosition.RouteListIndex);
                        altRoute = prevElement.StartAlternativePath[0];
                        break;
                    }
                }

                // check if alternative path may be used

                if (startAlternativeRoute > 0)
                {
                    Train.TCRouteElement startElement = trainRoute[startAlternativeRoute];
                    int endSectionIndex = startElement.StartAlternativePath[1];
                    TrackCircuitSection endSection = signalRef.TrackCircuitList[endSectionIndex];
                    if (endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                    {
                        startAlternativeRoute = -1; // reset use of alternative route
                    }
                }

                // if available, select part of route upto next signal

                if (startAlternativeRoute > 0)
                {
                    Train.TCSubpathRoute altRoutePart = thisTrain.Train.ExtractAlternativeRoute_pathBased(altRoute);

                    // check availability of alternative route

                    InternalBlockstate newblockstate = InternalBlockstate.Reservable;

                    foreach (Train.TCRouteElement thisElement in altRoutePart)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        int direction = thisElement.Direction;
                        newblockstate = thisSection.GetSectionState(enabledTrain, direction, newblockstate, thisRoute, thisRef);
                        if (newblockstate > InternalBlockstate.Reservable)
                            break;           // break on first non-reservable section //
                    }

                    // if available, use alternative route

                    if (newblockstate <= InternalBlockstate.Reservable)
                    {
                        blockstate = newblockstate;
                        thisTrain.Train.SetAlternativeRoute_pathBased(startAlternativeRoute, altRoute, this);
                        returnvalue = true;
                    }
                }
            }

            // check if approaching deadlock part, and if alternative route must be taken - if point where alt route start is not yet reserved
            // alternative route may not be taken if there is a train already waiting for the deadlock
            else if (thisTrain != null)
            {
                int startAlternativeRoute = -1;
                int altRoute = -1;
                TrackCircuitSection startSection = null;
                TrackCircuitSection endSection = null;

                Train.TCSubpathRoute trainRoute = thisTrain.Train.ValidRoute[thisTrain.TrainRouteDirectionIndex];
                Train.TCPosition thisPosition = thisTrain.Train.PresentPosition[thisTrain.TrainRouteDirectionIndex];

                for (int iElement = lastElementIndex; iElement >= 0; iElement--)
                {
                    Train.TCRouteElement prevElement = thisRoute[iElement];
                    if (prevElement.StartAlternativePath != null)
                    {
                        endSection = signalRef.TrackCircuitList[prevElement.StartAlternativePath[1]];
                        if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.CheckDeadlockAwaited(thisTrain.Train.Number))
                        {
                            altRoute = prevElement.StartAlternativePath[0];
                            startAlternativeRoute =
                                trainRoute.GetRouteIndex(prevElement.TCSectionIndex, thisPosition.RouteListIndex);
                            startSection = signalRef.TrackCircuitList[prevElement.TCSectionIndex];
                        }
                        break;
                    }
                }

                // use alternative route

                if (startAlternativeRoute > 0 &&
                    (startSection.CircuitState.TrainReserved == null || startSection.CircuitState.TrainReserved.Train != thisTrain.Train))
                {
                    Train.TCSubpathRoute altRoutePart = thisTrain.Train.ExtractAlternativeRoute_pathBased(altRoute);

                    // check availability of alternative route

                    InternalBlockstate newblockstate = InternalBlockstate.Reservable;

                    foreach (Train.TCRouteElement thisElement in altRoutePart)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        int direction = thisElement.Direction;
                        newblockstate = thisSection.GetSectionState(enabledTrain, direction, newblockstate, thisRoute, thisRef);
                        if (newblockstate > InternalBlockstate.Reservable)
                            break;           // break on first non-reservable section //
                    }

                    // if available, use alternative route

                    if (newblockstate <= InternalBlockstate.Reservable)
                    {
                        blockstate = newblockstate;
                        thisTrain.Train.SetAlternativeRoute_pathBased(startAlternativeRoute, altRoute, this);
                        if (endSection.DeadlockTraps.ContainsKey(thisTrain.Train.Number) && !endSection.DeadlockAwaited.Contains(thisTrain.Train.Number))
                            endSection.DeadlockAwaited.Add(thisTrain.Train.Number);
                        returnvalue = true;

                    }
                }
            }

            internalBlockState = blockstate;
            return (returnvalue);
        }

        //================================================================================================//
        /// <summary>
        /// Get block state
        /// Get internal state of full block for normal enabled signal upto next signal for clear request
        /// returns true if train set to use alternative route
        /// based on location-based deadlock processing
        /// </summary>

        private bool getBlockState_locationBased(Train.TCSubpathRoute thisRoute, Train.TrainRouted thisTrain, bool AIPermissionRequest)
        {
            List<int> SectionsWithAlternativePath = new List<int>();
            List<int> SectionsWithAltPathSet = new List<int>();
            bool altRouteAssigned = false;

            bool returnvalue = false;
            bool deadlockArea = false;

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // loop through all sections in route list

            Train.TCRouteElement lastElement = null;

            foreach (Train.TCRouteElement thisElement in thisRoute)
            {
                lastElement = thisElement;
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                int direction = thisElement.Direction;

                blockstate = thisSection.GetSectionState(enabledTrain, direction, blockstate, thisRoute, thisRef);
                if (blockstate > InternalBlockstate.OccupiedSameDirection)
                    break;     // exit on first none-available section

                // check if section is trigger section for waitany instruction
                if (thisTrain != null)
                {
                    if (thisTrain.Train.CheckAnyWaitCondition(thisSection.Index))
                    {
                        blockstate = InternalBlockstate.Blocked;
                    }
                }

                // check if this section is start of passing path area
                // if so, select which path must be used - but only if cleared by train in AUTO mode

                if (thisSection.DeadlockReference > 0 && thisElement.FacingPoint && thisTrain != null)
                {
                    if (thisTrain.Train.ControlMode == Train.TRAIN_CONTROL.AUTO_NODE || thisTrain.Train.ControlMode == Train.TRAIN_CONTROL.AUTO_SIGNAL)
                    {
                        DeadlockInfo sectionDeadlockInfo = signalRef.DeadlockInfoList[thisSection.DeadlockReference];

                        // if deadlock area and no path yet selected - exit loop; else follow assigned path
                        if (sectionDeadlockInfo.HasTrainAndSubpathIndex(thisTrain.Train.Number, thisTrain.Train.TCRoute.activeSubpath) &&
                            thisElement.UsedAlternativePath < 0)
                        {
                            deadlockArea = true;
                            break; // exits on deadlock area
                        }
                        else
                        {
                            SectionsWithAlternativePath.Add(thisElement.TCSectionIndex);
                            altRouteAssigned = true;
                        }
                    }
                }
                if (thisTrain != null && blockstate == InternalBlockstate.OccupiedSameDirection && (AIPermissionRequest || hasPermission == Permission.Requested)) break;
            }

            // if deadlock area : check alternative path if not yet selected - but only if opening junction is reservable
            // if free alternative path is found, set path available otherwise set path blocked

            if (deadlockArea && lastElement.UsedAlternativePath < 0)
            {
                if (blockstate <= InternalBlockstate.Reservable)
                {

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt",
                    "\n **** Get block state for section " + lastElement.TCSectionIndex.ToString() + " for train : " + thisTrain.Train.Number.ToString() + "\n");
#endif
                    TrackCircuitSection lastSection = signalRef.TrackCircuitList[lastElement.TCSectionIndex];
                    DeadlockInfo sectionDeadlockInfo = signalRef.DeadlockInfoList[lastSection.DeadlockReference];
                    List<int> availableRoutes = sectionDeadlockInfo.CheckDeadlockPathAvailability(lastSection, thisTrain.Train);

#if DEBUG_DEADLOCK
                File.AppendAllText(@"C:\Temp\deadlock.txt", "\nReturned no. of available paths : " + availableRoutes.Count.ToString() + "\n");
                File.AppendAllText(@"C:\Temp\deadlock.txt", "****\n\n");
#endif

                    if (availableRoutes.Count >= 1)
                    {
                        int endSectionIndex = -1;
                        int usedRoute = sectionDeadlockInfo.SelectPath(availableRoutes, thisTrain.Train, ref endSectionIndex);
                        lastElement.UsedAlternativePath = usedRoute;
                        SectionsWithAltPathSet.Add(lastElement.TCSectionIndex);
                        altRouteAssigned = true;

                        thisTrain.Train.SetAlternativeRoute_locationBased(lastSection.Index, sectionDeadlockInfo, usedRoute, this);
                        returnvalue = true;
                        blockstate = InternalBlockstate.Reservable;
                    }
                    else
                    {
                        blockstate = InternalBlockstate.Blocked;
                    }
                }
                else
                {
                    blockstate = InternalBlockstate.Blocked;
                }
            }

            internalBlockState = blockstate;

            // reset any alternative route selections if route is not available
            if (altRouteAssigned && blockstate != InternalBlockstate.Reservable && blockstate != InternalBlockstate.Reserved)
            {
                foreach (int SectionNo in SectionsWithAlternativePath)
                {
#if DEBUG_REPORTS
                    Trace.TraceInformation("Train : {0} : state {1} but route already set for section {2}",
                        thisTrain.Train.Name, blockstate, SectionNo);
#endif
                    int routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(SectionNo, thisTrain.Train.PresentPosition[0].RouteListIndex);
                    Train.TCRouteElement thisElement = thisTrain.Train.ValidRoute[0][routeIndex];
                    thisElement.UsedAlternativePath = -1;
                }
                foreach (int SectionNo in SectionsWithAltPathSet)
                {
#if DEBUG_REPORTS
                    Trace.TraceInformation("Train : {0} : state {1} but route now set for section {2}",
                        thisTrain.Train.Name, blockstate, SectionNo);
#endif
                    int routeIndex = thisTrain.Train.ValidRoute[0].GetRouteIndex(SectionNo, thisTrain.Train.PresentPosition[0].RouteListIndex);
                    Train.TCRouteElement thisElement = thisTrain.Train.ValidRoute[0][routeIndex];
                    thisElement.UsedAlternativePath = -1;
                }
            }

            return (returnvalue);
        }

        //================================================================================================//
        /// <summary>
        /// Get part block state
        /// Get internal state of part of block for normal enabled signal upto next signal for clear request
        /// if there are no switches before next signal or end of track, treat as full block
        /// </summary>

        private void getPartBlockState(Train.TCSubpathRoute thisRoute)
        {

            // check beyond last section for next signal or end of track 

            int listIndex = (thisRoute.Count > 0) ? (thisRoute.Count - 1) : thisTrainRouteIndex;

            Train.TCRouteElement lastElement = thisRoute[listIndex];
            int thisSectionIndex = lastElement.TCSectionIndex;
            Heading direction = (Heading)lastElement.Direction;

            Train.TCSubpathRoute additionalElements = new Train.TCSubpathRoute();

            bool end_of_info = false;

            while (!end_of_info)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];

                TrackCircuitType thisType = thisSection.CircuitType;

                switch (thisType)
                {
                    case (TrackCircuitType.EndOfTrack):
                        end_of_info = true;
                        break;

                    case (TrackCircuitType.Junction):
                    case (TrackCircuitType.Crossover):
                        end_of_info = true;
                        break;

                    default:
                        Train.TCRouteElement newElement = new Train.TCRouteElement(thisSectionIndex, (int)direction);
                        additionalElements.Add(newElement);

                        if (thisSection.EndSignals[direction] != null)
                        {
                            end_of_info = true;
                        }
                        break;
                }

                if (!end_of_info)
                {
                    thisSectionIndex = thisSection.Pins[(int)direction, 0].Link;
                    direction = (Heading)thisSection.Pins[(int)direction, 0].Direction;
                }
            }

            InternalBlockstate blockstate = InternalBlockstate.Reserved;  // preset to lowest possible state //

            // check all elements in original route

            foreach (Train.TCRouteElement thisElement in thisRoute)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                direction = (Heading)thisElement.Direction;
                blockstate = thisSection.GetSectionState(enabledTrain, (int)direction, blockstate, thisRoute, thisRef);
                if (blockstate > InternalBlockstate.Reservable)
                    break;           // break on first non-reservable section //
            }

            // check all additional elements upto signal, junction or end-of-track

            if (blockstate <= InternalBlockstate.Reservable)
            {
                foreach (Train.TCRouteElement thisElement in additionalElements)
                {
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                    direction = (Heading)thisElement.Direction;
                    blockstate = thisSection.GetSectionState(enabledTrain, (int)direction, blockstate, additionalElements, thisRef);
                    if (blockstate > InternalBlockstate.Reservable)
                        break;           // break on first non-reservable section //
                }
            }

            //          if (blockstate <= INTERNAL_BLOCKSTATE.RESERVABLE && end_at_junction)
            //          {
            //              blockstate = INTERNAL_BLOCKSTATE.OCCUPIED_SAMEDIR;  // set restricted state
            //          }

            internalBlockState = blockstate;

        }

        //================================================================================================//
        /// <summary>
        /// Set signal default route and next signal list as switch in route is reset
        /// Used in manual mode for signals which clear by default
        /// </summary>

        public void SetDefaultRoute()
        {
            signalRoute = new Train.TCSubpathRoute(fixedRoute);
            for (int iSigtype = 0; iSigtype <= defaultNextSignal.Count - 1; iSigtype++)
            {
                sigfound[iSigtype] = defaultNextSignal[iSigtype];
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reset signal and clear all train sections
        /// </summary>

        public void ResetSignal(bool propagateReset)
        {
            Train.TrainRouted thisTrain = enabledTrain;

            // search for last signal enabled for this train, start reset from there //

            Signal thisSignal = this;
            List<Signal> passedSignals = new List<Signal>();
            int thisSignalIndex = thisSignal.thisRef;

            if (propagateReset)
            {
                while (thisSignalIndex >= 0 && signalObjects[thisSignalIndex].enabledTrain == thisTrain)
                {
                    thisSignal = signalObjects[thisSignalIndex];
                    passedSignals.Add(thisSignal);
                    thisSignalIndex = thisSignal.sigfound[(int)SignalFunction.Normal];
                }
            }
            else
            {
                passedSignals.Add(thisSignal);
            }

            foreach (Signal nextSignal in passedSignals)
            {
                if (nextSignal.signalRoute != null)
                {
                    List<TrackCircuitSection> sectionsToClear = new List<TrackCircuitSection>();
                    foreach (Train.TCRouteElement thisElement in nextSignal.signalRoute)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        sectionsToClear.Add(thisSection);  // store in list as signalRoute is lost during remove action
                    }
                    foreach (TrackCircuitSection thisSection in sectionsToClear)
                    {
                        if (thisTrain != null)
                        {
                            thisSection.RemoveTrain(thisTrain, false);
                        }
                        else
                        {
                            thisSection.Unreserve();
                        }
                    }
                }

                nextSignal.resetSignalEnabled();
            }
        }

        //================================================================================================//
        /// <summary>
        /// Reset signal route and next signal list as switch in route is reset
        /// </summary>

        public void ResetRoute(int resetSectionIndex)
        {

            // remove this signal from any other junctions

            foreach (int thisSectionIndex in JunctionsPassed)
            {
                if (thisSectionIndex != resetSectionIndex)
                {
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];
                    thisSection.SignalsPassingRoutes.Remove(thisRef);
                }
            }

            JunctionsPassed.Clear();

            for (int fntype = 0; fntype < signalRef.ORTSSignalTypeCount; fntype++)
            {
                sigfound[fntype] = defaultNextSignal[fntype];
            }

            // if signal is enabled, ensure next normal signal is reset

            if (enabledTrain != null && sigfound[(int)SignalFunction.Normal] < 0)
            {
                sigfound[(int)SignalFunction.Normal] = SONextSignalNormal(TCNextTC);
            }

#if DEBUG_REPORTS
            File.AppendAllText(@"C:\temp\printproc.txt",
				String.Format("Signal {0} reset on Junction Change\n",
				thisRef));

            if (enabledTrain != null)
            {
				File.AppendAllText(@"C:\temp\printproc.txt",
					String.Format("Train {0} affected; new NORMAL signal : {1}\n",
					enabledTrain.Train.Number, sigfound[(int)MstsSignalFunction.NORMAL]));
            }
#endif
            if (enabledTrain != null && enabledTrain.Train.CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Signal {0} reset on Junction Change\n",
                    thisRef));
                File.AppendAllText(@"C:\temp\checktrain.txt",
                    String.Format("Train {0} affected; new NORMAL signal : {1}\n",
                    enabledTrain.Train.Number, sigfound[(int)SignalFunction.Normal]));
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set flag to allow signal to clear to partial route
        /// </summary>

        public void AllowClearPartialRoute(int setting)
        {
            AllowPartRoute = setting == 1 ? true : false;
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control - position only
        /// </summary>

        public bool ApproachControlPosition(int reqPositionM, string dumpfile, bool forced)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : no train approaching");
                }

                return (false);
            }

            // signal is not first signal for train - check only if not forced
            if (!forced)
            {
                if (enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] == null ||
                    enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef != thisRef)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} : First signal is not this signal but {1} \n",
                            enabledTrain.Train.Number, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlSet = true;  // approach control is selected but train is yet further out, so assume approach control has locked signal
                    return (false);
                }
            }

            // if already cleared - return true

            if (ApproachControlCleared)
            {
                ApproachControlSet = false;
                ClaimLocked = false;
                ForcePropOnApproachControl = false;
                return (true);
            }

            bool found = false;
            float distance = 0;
            int actDirection = enabledTrain.TrainRouteDirectionIndex;
            Train.TCSubpathRoute routePath = enabledTrain.Train.ValidRoute[actDirection];
            int actRouteIndex = routePath == null ? -1 : routePath.GetRouteIndex(enabledTrain.Train.PresentPosition[actDirection].TCSectionIndex, 0);
            if (actRouteIndex >= 0)
            {
                float offset = 0;
                if (enabledTrain.TrainRouteDirectionIndex == 0)
                    offset = enabledTrain.Train.PresentPosition[0].TCOffset;
                else
                    offset = signalRef.TrackCircuitList[enabledTrain.Train.PresentPosition[1].TCSectionIndex].Length - enabledTrain.Train.PresentPosition[1].TCOffset;
                while (!found)
                {
                    Train.TCRouteElement thisElement = routePath[actRouteIndex];
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                    distance += thisSection.Length - offset;
                    if (thisSection.EndSignals[(Heading)thisElement.Direction] == this)
                    {
                        found = true;
                    }
                    else
                    {
                        offset = 0;
                        int setSection = thisSection.ActivePins[thisElement.OutPin[0], thisElement.OutPin[1]].Link;
                        actRouteIndex++;
                        if (actRouteIndex >= routePath.Count || setSection < 0)
                            break;
                    }
                }
            }

            if (!found)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} has no valid path to signal, clear not allowed \n", enabledTrain.Train.Number);
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                ApproachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(distance) < reqPositionM)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear allowed \n",
                        enabledTrain.Train.Number, distance, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = false;
                ApproachControlCleared = true;
                ClaimLocked = false;
                ForcePropOnApproachControl = false;
                return (true);
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear not allowed \n",
                        enabledTrain.Train.Number, distance, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control - position and speed
        /// </summary>

        public bool ApproachControlSpeed(int reqPositionM, int reqSpeedMpS, string dumpfile)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : no train approaching");
                }

                return (false);
            }

            // signal is not first signal for train
            if (enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] != null &&
                enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef != thisRef)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} : First signal is not this signal but {1} \n",
                        enabledTrain.Train.Number, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }

            // if already cleared - return true

            if (ApproachControlCleared)
            {
                ApproachControlSet = false;
                ForcePropOnApproachControl = false;
                return (true);
            }

            // check if distance is valid

            if (!enabledTrain.Train.DistanceToSignal.HasValue)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} has no valid distance to signal, clear not allowed \n",
                        enabledTrain.Train.Number);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(enabledTrain.Train.DistanceToSignal.Value) < reqPositionM)
            {
                bool validSpeed = false;
                if (reqSpeedMpS > 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < reqSpeedMpS)
                    {
                        validSpeed = true;
                    }
                }
                else if (reqSpeedMpS == 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < 0.1)
                    {
                        validSpeed = true;
                    }
                }

                if (validSpeed)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlCleared = true;
                    ApproachControlSet = false;
                    ClaimLocked = false;
                    ForcePropOnApproachControl = false;
                    return (true);
                }
                else
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear not allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlSet = true;
                    return (false);
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear not allowed \n",
                        enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Test for approach control in case of APC on next STOP
        /// </summary>

        public bool ApproachControlNextStop(int reqPositionM, int reqSpeedMpS, string dumpfile)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : no train approaching\n");
                }

                return (false);
            }

            // signal is not first signal for train
            if (enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex] != null &&
                enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef != thisRef)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} : First signal is not this signal but {1} \n",
                        enabledTrain.Train.Number, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                ForcePropOnApproachControl = true;
                return (false);
            }

            // if already cleared - return true

            if (ApproachControlCleared)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "APPROACH CONTROL : cleared\n");
                }

                return (true);
            }

            // check if distance is valid

            if (!enabledTrain.Train.DistanceToSignal.HasValue)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} has no valid distance to signal, clear not allowed \n",
                        enabledTrain.Train.Number);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                return (false);
            }

            // test distance

            if (Convert.ToInt32(enabledTrain.Train.DistanceToSignal.Value) < reqPositionM)
            {
                bool validSpeed = false;
                if (reqSpeedMpS > 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < reqSpeedMpS)
                    {
                        validSpeed = true;
                    }
                }
                else if (reqSpeedMpS == 0)
                {
                    if (Math.Abs(enabledTrain.Train.SpeedMpS) < 0.1)
                    {
                        validSpeed = true;
                    }
                }

                if (validSpeed)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlCleared = true;
                    ApproachControlSet = false;
                    ClaimLocked = false;
                    ForcePropOnApproachControl = false;
                    return (true);
                }
                else
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}) and speed {3} (required {4}), clear not allowed \n",
                            enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM, enabledTrain.Train.SpeedMpS, reqSpeedMpS);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }

                    ApproachControlSet = true;
                    ForcePropOnApproachControl = true;
                    return (false);
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("APPROACH CONTROL : Train {0} at distance {1} (required {2}), clear not allowed \n",
                        enabledTrain.Train.Number, enabledTrain.Train.DistanceToSignal.Value, reqPositionM);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                ApproachControlSet = true;
                ForcePropOnApproachControl = true;
                return (false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Lock claim (only if approach control is active)
        /// </summary>

        public void LockClaim()
        {
            ClaimLocked = ApproachControlSet;
        }

        //================================================================================================//
        /// <summary>
        /// Activate timing trigger
        /// </summary>

        public void ActivateTimingTrigger()
        {
            TimingTriggerValue = signalRef.Simulator.GameTime;
        }

        //================================================================================================//
        /// <summary>
        /// Check timing trigger
        /// </summary>

        public bool CheckTimingTrigger(int reqTiming, string dumpfile)
        {
            int foundDelta = (int) (signalRef.Simulator.GameTime - TimingTriggerValue);
            bool triggerExceeded = foundDelta > reqTiming;

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("TIMING TRIGGER : found delta time : {0}; return state {1} \n", foundDelta, triggerExceeded.ToString());
                File.AppendAllText(dumpfile, sob.ToString());
            }

            return (triggerExceeded);
        }

        //================================================================================================//
        /// <summary>
        /// Test if train has call-on set
        /// </summary>

        public bool TrainHasCallOn(bool allowOnNonePlatform, bool allowAdvancedSignal, string dumpfile)
        {
            // no train approaching
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    File.AppendAllText(dumpfile, "CALL ON : no train approaching \n");
                }

                return (false);
            }

            // signal is not first signal for train
            var nextSignal = enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex];

            if (!allowAdvancedSignal &&
               nextSignal != null && nextSignal.thisRef != thisRef)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("CALL ON : Train {0} : First signal is not this signal but {1} \n",
                        enabledTrain.Train.Name, enabledTrain.Train.NextSignalObject[enabledTrain.TrainRouteDirectionIndex].thisRef);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                return (false);
            }

            if (enabledTrain.Train != null && signalRoute != null)
            {
                bool callOnValid = enabledTrain.Train.TestCallOn(this, allowOnNonePlatform, signalRoute, dumpfile);
                return (callOnValid);
            }

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("CALL ON : Train {0} : not valid \n", enabledTrain.Train.Name);
                File.AppendAllText(dumpfile, sob.ToString());
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Test if train requires next signal
        /// </summary>

        public bool RequiresNextSignal(int nextSignalId, int reqPosition, string dumpfile)
        {
            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("REQ_NEXT_SIGNAL : check for signal {0} \n", nextSignalId);
                File.AppendAllText(dumpfile, sob.ToString());
            }

            // no enabled train
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : no enabled train \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("REQ_NEXT_SIGNAL : enabled train : {0} = {1} \n", enabledTrain.Train.Name, enabledTrain.Train.Number);
                File.AppendAllText(dumpfile, sob.ToString());
            }

            // train has no path
            Train reqTrain = enabledTrain.Train;
            if (reqTrain.ValidRoute == null || reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex] == null || reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count <= 0)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : train has no valid route \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            // next signal is not valid
            if (nextSignalId < 0 || nextSignalId >= signalObjects.Length || !signalObjects[nextSignalId].isSignalNormal())
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : signal is not NORMAL signal \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            // trains present position is unknown
            if (reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex < 0 ||
                reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex >= reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : train has no valid position : {0} (of {1}) \n",
                        reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex,
                        reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count);
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (false);
            }

            // check if section beyond or ahead of next signal is within trains path ahead of present position of train
            int reqSection = reqPosition == 1 ? signalObjects[nextSignalId].TCNextTC : signalObjects[nextSignalId].TCReference;

            int sectionIndex = reqTrain.ValidRoute[enabledTrain.TrainRouteDirectionIndex].GetRouteIndex(reqSection, reqTrain.PresentPosition[enabledTrain.TrainRouteDirectionIndex].RouteListIndex);
            if (sectionIndex > 0)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("REQ_NEXT_SIGNAL : TRUE : signal position is in route : section {0} has index {1} \n",
                        signalObjects[nextSignalId].TCNextTC, sectionIndex);
                    File.AppendAllText(dumpfile, sob.ToString());
                }
                return (true);
            }

            if (!String.IsNullOrEmpty(dumpfile))
            {
                var sob = new StringBuilder();
                sob.AppendFormat("REQ_NEXT_SIGNAL : FALSE : signal position is not in route : section {0} has index {1} \n",
                    signalObjects[nextSignalId].TCNextTC, sectionIndex);
                File.AppendAllText(dumpfile, sob.ToString());
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Get ident of signal ahead with specific details
        /// </summary>

        public int FindReqNormalSignal(int req_value, string dumpfile)
        {
            int foundSignal = -1;

            // signal not enabled - no route available
            if (enabledTrain == null)
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.Append("FIND_REQ_NORMAL_SIGNAL : not found : signal is not enabled");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
            }
            else
            {
                int startIndex = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TCNextTC, enabledTrain.Train.PresentPosition[0].RouteListIndex);
                if (startIndex < 0)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : not found : cannot find signal {0} at section {1} in path of train {2}\n", thisRef, TCNextTC, enabledTrain.Train.Name);
                        File.AppendAllText(dumpfile, sob.ToString());
                    }
                }
                else
                {
                    for (int iRouteIndex = startIndex; iRouteIndex < enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex].Count; iRouteIndex++)
                    {
                        Train.TCRouteElement thisElement = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex][iRouteIndex];
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                        if (thisSection.EndSignals[(Heading)thisElement.Direction] != null)
                        {
                            Signal endSignal = thisSection.EndSignals[(Heading)thisElement.Direction];

                            // found signal, check required value
                            bool found_value = false;

                            foreach (SignalHead thisHead in endSignal.SignalHeads)
                            {
                                if (thisHead.OrtsNormalSubtypeIndex == req_value)
                                {
                                    found_value = true;
                                    if (!String.IsNullOrEmpty(dumpfile))
                                    {
                                        var sob = new StringBuilder();
                                        sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : signal found : {0} : head : {1} : state : {2} \n", endSignal.thisRef, thisHead.TDBIndex, found_value);
                                    }
                                    break;
                                }
                            }

                            if (found_value)
                            {
                                foundSignal = endSignal.thisRef;
                                if (!String.IsNullOrEmpty(dumpfile))
                                {
                                    var sob = new StringBuilder();
                                    sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : signal found : {0} : ( ", endSignal.thisRef);

                                    foreach (SignalHead otherHead in endSignal.SignalHeads)
                                    {
                                        sob.AppendFormat(" {0} ", otherHead.TDBIndex);
                                    }

                                    sob.AppendFormat(") \n");
                                    File.AppendAllText(dumpfile, sob.ToString());
                                }
                                break;
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(dumpfile))
                                {
                                    var sob = new StringBuilder();
                                    sob.AppendFormat("FIND_REQ_NORMAL_SIGNAL : signal found : {0} : ( ", endSignal.thisRef);

                                    foreach (SignalHead otherHead in endSignal.SignalHeads)
                                    {
                                        sob.AppendFormat(" {0} ", otherHead.TDBIndex);
                                    }

                                    sob.AppendFormat(") ");
                                    sob.AppendFormat("incorrect variable value : {0} \n", found_value);
                                    File.AppendAllText(dumpfile, sob.ToString());
                                }
                            }
                        }
                    }
                }
            }

            return (foundSignal);
        }

        //================================================================================================//
        /// <summary>
        /// Check if route for train is cleared upto or beyond next required signal
        /// parameter req_position : 0 = check upto signal, 1 = check beyond signal
        /// </summary>

        public SignalBlockState RouteClearedToSignal(int req_signalid, bool allowCallOn, string dumpfile)
        {
            SignalBlockState routeState = SignalBlockState.Jn_Obstructed;
            if (enabledTrain != null && enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex] != null && req_signalid >= 0 && req_signalid < signalRef.SignalObjects.Length)
            {
                Signal otherSignal = signalRef.SignalObjects[req_signalid];

                TrackCircuitSection reqSection = null;
                reqSection = signalRef.TrackCircuitList[otherSignal.TCReference];
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : signal checked : {0} , section [ahead] found : {1} \n", req_signalid, reqSection.Index);
                    File.AppendAllText(dumpfile, sob.ToString());
                }

                Train.TCSubpathRoute trainRoute = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex];

                int thisRouteIndex = trainRoute.GetRouteIndex(isSignalNormal() ? TCNextTC : TCReference, 0);
                int otherRouteIndex = trainRoute.GetRouteIndex(otherSignal.TCReference, thisRouteIndex);

                if (otherRouteIndex < 0)
                {
                    if (!String.IsNullOrEmpty(dumpfile))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : section found is not in this trains route \n");
                        File.AppendAllText(dumpfile, sob.ToString());
                    }
                }

                // extract route
                else
                {
                    bool routeCleared = true;
                    Train.TCSubpathRoute reqPath = new Train.TCSubpathRoute(trainRoute, thisRouteIndex, otherRouteIndex);

                    for (int iIndex = 0; iIndex < reqPath.Count && routeCleared; iIndex++)
                    {
                        TrackCircuitSection thisSection = signalRef.TrackCircuitList[reqPath[iIndex].TCSectionIndex];
                        if (!thisSection.IsSet(enabledTrain, false))
                        {
                            routeCleared = false;
                            if (!String.IsNullOrEmpty(dumpfile))
                            {
                                var sob = new StringBuilder();
                                sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : section {0} is not set for required train \n", thisSection.Index);
                                File.AppendAllText(dumpfile, sob.ToString());
                            }
                        }
                    }

                    if (routeCleared)
                    {
                        routeState = SignalBlockState.Clear;
                        if (!String.IsNullOrEmpty(dumpfile))
                        {
                            var sob = new StringBuilder();
                            sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : all sections set \n");
                            File.AppendAllText(dumpfile, sob.ToString());
                        }
                    }
                    else if (allowCallOn)
                    {
                        if (enabledTrain.Train.TestCallOn(this, false, reqPath, dumpfile))
                        {
                            routeCleared = true;
                            routeState = SignalBlockState.Occupied;
                            if (!String.IsNullOrEmpty(dumpfile))
                            {
                                var sob = new StringBuilder();
                                sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : callon allowed \n");
                                File.AppendAllText(dumpfile, sob.ToString());
                            }
                        }
                    }

                    if (!routeCleared)
                    {
                        routeState = SignalBlockState.Jn_Obstructed;
                        if (!String.IsNullOrEmpty(dumpfile))
                        {
                            var sob = new StringBuilder();
                            sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : route not available \n");
                            File.AppendAllText(dumpfile, sob.ToString());
                        }
                    }
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(dumpfile))
                {
                    var sob = new StringBuilder();
                    sob.AppendFormat("ROUTE_CLEARED_TO_SIGNAL : found state : invalid request (no enabled train or invalid signalident) \n");
                    File.AppendAllText(dumpfile, sob.ToString());
                }
            }

            return (routeState);
        }

        //================================================================================================//
        /// <summary>
        /// LockForTrain
        /// Add a lock for a train and a specific subpath (default 0).  This allow the control of this signal by a specific action
        /// </summary>

        public bool LockForTrain(int trainNumber, int subpath = 0)
        {
            KeyValuePair<int, int> newLock = new KeyValuePair<int, int>(trainNumber, subpath);
            LockedTrains.Add(newLock);
            return false;
        }

        public bool UnlockForTrain(int trainNumber, int subpath = 0)
        {
            bool info = LockedTrains.Remove(LockedTrains.First(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
            return info;
        }

        public bool HasLockForTrain(int trainNumber, int subpath = 0)
        {
            bool info = (LockedTrains.Count > 0 && LockedTrains.Exists(item => item.Key.Equals(trainNumber) && item.Value.Equals(subpath)));
            return info;
        }

        public bool CleanAllLock(int trainNumber)
        {
            int info = LockedTrains.RemoveAll(item => item.Key.Equals(trainNumber));
            if (info > 0)
                return true;
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// HasHead
        ///
        /// Returns 1 if signal has optional head set, 0 if not
        /// </summary>

        public int HasHead(int requiredHeadIndex)
        {
            if (WorldObject == null || WorldObject.HeadsSet == null)
            {
                Trace.TraceInformation("Signal {0} (TDB {1}) has no heads", thisRef, SignalHeads[0].TDBIndex);
                return (0);
            }
            return ((requiredHeadIndex < WorldObject.HeadsSet.Length) ? (WorldObject.HeadsSet[requiredHeadIndex] ? 1 : 0) : 0);
        }

        //================================================================================================//
        /// <summary>
        /// IncreaseSignalNumClearAhead
        ///
        /// Increase SignalNumClearAhead from its default value with the value as passed
        /// <summary>

        public void IncreaseSignalNumClearAhead(int requiredIncreaseValue)
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAhead_ORTS + requiredIncreaseValue;
            }
        }

        //================================================================================================//
        /// <summary>
        /// DecreaseSignalNumClearAhead
        ///
        /// Decrease SignalNumClearAhead from its default value with the value as passed
        /// </summary>

        public void DecreaseSignalNumClearAhead(int requiredDecreaseValue)
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAhead_ORTS - requiredDecreaseValue;
            }
        }

        //================================================================================================//
        /// <summary>
        /// SetSignalNumClearAhead
        ///
        /// Set SignalNumClearAhead to the value as passed
        /// <summary>

        public void SetSignalNumClearAhead(int requiredValue)
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = requiredValue;
            }
        }

        //================================================================================================//
        /// <summary>
        /// ResetSignalNumClearAhead
        ///
        /// Reset SignalNumClearAhead to the default value
        /// </summary>

        public void ResetSignalNumClearAhead()
        {
            if (SignalNumClearAhead_ORTS > -2)
            {
                SignalNumClearAheadActive = SignalNumClearAhead_ORTS;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set HOLD state for dispatcher control
        ///
        /// Parameter : bool, if set signal must be reset if set (and train position allows)
        ///
        /// Returned : bool[], dimension 2,
        ///            field [0] : if true, hold state is set
        ///            field [1] : if true, signal is reset (always returns false if reset not requested)
        /// </summary>

        public bool[] requestHoldSignalDispatcher(bool requestResetSignal)
        {
            bool[] returnValue = new bool[2] { false, false };
            SignalAspectState thisAspect = this_sig_lr(SignalFunction.Normal);

            // signal not enabled - set lock, reset if cleared (auto signal can clear without enabling)

            if (enabledTrain == null || enabledTrain.Train == null)
            {
                holdState = HoldState.ManualLock;
                if (thisAspect > SignalAspectState.Stop) ResetSignal(true);
                returnValue[0] = true;
            }

            // if enabled, cleared and reset not requested : no action

            else if (!requestResetSignal && thisAspect > SignalAspectState.Stop)
            {
                holdState = HoldState.ManualLock; //just in case this one later will be set to green by the system
                returnValue[0] = true;
            }

            // if enabled and not cleared : set hold, no reset required

            else if (thisAspect == SignalAspectState.Stop)
            {
                holdState = HoldState.ManualLock;
                returnValue[0] = true;
            }

            // enabled, cleared , reset required : check train speed
            // if train is moving : no action
            //temporarily removed by JTang, before the full revision is ready
            //          else if (Math.Abs(enabledTrain.Train.SpeedMpS) > 0.1f)
            //          {
            //          }

            // if train is stopped : reset signal, breakdown train route, set holdstate

            else
            {
                int signalRouteIndex = enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex].GetRouteIndex(TCNextTC, 0);
                if (signalRouteIndex >= 0)
                {
                    signalRef.BreakDownRouteList(enabledTrain.Train.ValidRoute[enabledTrain.TrainRouteDirectionIndex], signalRouteIndex, enabledTrain);
                    ResetSignal(true);
                    holdState = HoldState.ManualLock;
                    returnValue[0] = true;
                    returnValue[1] = true;
                }
                else //hopefully this does not happen
                {
                    holdState = HoldState.ManualLock;
                    returnValue[0] = true;
                }
            }

            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Reset HOLD state for dispatcher control
        /// </summary>

        public void clearHoldSignalDispatcher()
        {
            holdState = HoldState.None;
        }

    }  // SignalObject

}
