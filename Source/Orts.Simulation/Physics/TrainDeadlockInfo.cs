using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

namespace Orts.Simulation.Physics
{
    internal class TrainDeadlockInfo
    {
        private readonly Dictionary<int, List<Dictionary<int, int>>> deadlockInfo;
        private readonly Train train;

        public TrainDeadlockInfo(Train train)
        {
            deadlockInfo = new Dictionary<int, List<Dictionary<int, int>>>();
            this.train = train;
        }

        internal TrainDeadlockInfo(Train train, Dictionary<int, List<Dictionary<int, int>>> deadlockInfo)
        {
            this.train = train;
            this.deadlockInfo = deadlockInfo;
        }

        public Dictionary<int, List<Dictionary<int, int>>> State() { return deadlockInfo; }

        public bool TryGet(int trackSectionIndex, out List<Dictionary<int, int>> result) => deadlockInfo.TryGetValue(trackSectionIndex, out result);

        // Set deadlock information
        public void SetDeadlockInfo(int firstSection, int lastSection, int otherTrainNumber)
        {
            if (!deadlockInfo.TryGetValue(firstSection, out List<Dictionary<int, int>> deadlockList))
            {
                deadlockList = new List<Dictionary<int, int>>();
                deadlockInfo.Add(firstSection, deadlockList);
            }
            Dictionary<int, int> deadlock = new Dictionary<int, int>
            {
                { otherTrainNumber, lastSection }
            };
            deadlockList.Add(deadlock);
        }

        // Check if conflict is real deadlock situation
        // Conditions :
        //   if section is part of deadlock definition, it is a deadlock
        //   if section has intermediate signals, it is a deadlock
        //   if section has no intermediate signals but there are signals on both approaches to the deadlock, it is not a deadlock
        // Return value : boolean to indicate it is a deadlock or not
        // If not a deadlock, the REF int elementIndex is set to index of the last common section (will be increased in the loop)
        public bool CheckRealDeadlockLocationBased(TrackCircuitPartialPathRoute route, TrackCircuitPartialPathRoute otherRoute, ref int elementIndex)
        {
            bool isValidDeadlock = false;

            TrackCircuitSection section = route[elementIndex].TrackCircuitSection;

            // check if section is start or part of deadlock definition
            if (section.DeadlockReference >= 0 || (section.DeadlockBoundaries != null && section.DeadlockBoundaries.Count > 0))
            {
                return true;
            }

            // loop through common section - if signal is found, it is a deadlock 
            bool validLoop = true;
            int otherRouteIndex = otherRoute.GetRouteIndex(section.Index, 0);

            for (int i = 0; validLoop; i++)
            {
                int thisElementIndex = elementIndex + i;
                int otherElementIndex = otherRouteIndex - i;

                if (thisElementIndex > route.Count - 1)
                    validLoop = false;
                if (otherElementIndex < 0)
                    validLoop = false;

                if (validLoop)
                {
                    TrackCircuitSection thisRouteSection = route[thisElementIndex].TrackCircuitSection;
                    TrackCircuitSection otherRouteSection = otherRoute[otherElementIndex].TrackCircuitSection;

                    if (thisRouteSection.Index != otherRouteSection.Index)
                    {
                        validLoop = false;
                    }
                    else if (thisRouteSection.EndSignals[TrackDirection.Ahead] != null || thisRouteSection.EndSignals[TrackDirection.Reverse] != null)
                    {
                        isValidDeadlock = true;
                        validLoop = false;
                    }
                }
            }

            // if no signals along section, check if section is protected by signals - if so, it is not a deadlock
            // check only as far as maximum signal check distance
            if (!isValidDeadlock)
            {
                // this route backward first
                float totalDistance = 0.0f;
                bool signalFound = false;
                validLoop = true;

                for (int i = 0; validLoop; i--)
                {
                    int thisElementIndex = elementIndex + i; // going backward as iIndex is negative!
                    if (thisElementIndex < 0)
                    {
                        validLoop = false;
                    }
                    else
                    {
                        TrackCircuitRouteElement thisElement = route[thisElementIndex];
                        TrackCircuitSection thisRouteSection = thisElement.TrackCircuitSection;
                        totalDistance += thisRouteSection.Length;

                        if (thisRouteSection.EndSignals[thisElement.Direction] != null)
                        {
                            validLoop = false;
                            signalFound = true;
                        }

                        if (totalDistance > Train.MinCheckDistanceM)
                            validLoop = false;
                    }
                }

                // other route backward next
                totalDistance = 0.0f;
                bool otherSignalFound = false;
                validLoop = true;

                for (int iIndex = 0; validLoop; iIndex--)
                {
                    int thisElementIndex = otherRouteIndex + iIndex; // going backward as iIndex is negative!
                    if (thisElementIndex < 0)
                    {
                        validLoop = false;
                    }
                    else
                    {
                        TrackCircuitRouteElement thisElement = otherRoute[thisElementIndex];
                        TrackCircuitSection thisRouteSection = thisElement.TrackCircuitSection;
                        totalDistance += thisRouteSection.Length;

                        if (thisRouteSection.EndSignals[thisElement.Direction] != null)
                        {
                            validLoop = false;
                            otherSignalFound = true;
                        }

                        if (totalDistance > Train.MinCheckDistanceM)
                            validLoop = false;
                    }
                }

                if (!signalFound || !otherSignalFound)
                    isValidDeadlock = true;
            }

            // if not a valid deadlock, find end of common section
            if (!isValidDeadlock)
            {
                elementIndex = EndCommonSection(elementIndex, route, otherRoute);
                ;
            }

            return isValidDeadlock;
        }

        // set any deadlocks for sections ahead of start with end beyond start
        internal void SetDeadlocksAhead(int rearIndex)
        {
            for (int i = 0; i < rearIndex; i++)
            {
                int rearSectionIndex = train.ValidRoutes[Direction.Forward][i].TrackCircuitSection.Index;
                if (deadlockInfo.TryGetValue(rearSectionIndex, out List<Dictionary<int, int>> value))
                {
                    foreach (Dictionary<int, int> deadlock in value)
                    {
                        foreach (KeyValuePair<int, int> deadlockDetail in deadlock)
                        {
                            int endSectionIndex = deadlockDetail.Value;
                            if (train.ValidRoutes[Direction.Forward].GetRouteIndex(endSectionIndex, rearIndex) >= 0)
                            {
                                TrackCircuitSection.TrackCircuitList[endSectionIndex].SetDeadlockTrap(train.Number, deadlockDetail.Key);
                            }
                        }
                    }
                }
            }
        }

        // Check if waiting for deadlock
        internal bool CheckDeadlockWait(Signal nextSignal)
        {

            bool deadlockWait = false;

            // check section list of signal for any deadlock traps
            foreach (TrackCircuitRouteElement routeElement in nextSignal.SignalRoute)
            {
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                if (section.DeadlockTraps.TryGetValue(train.Number, out List<int> deadlockTrains))              // deadlock trap
                {
                    deadlockWait = true;

                    if (deadlockInfo.TryGetValue(section.Index, out List<Dictionary<int, int>> value) && !train.CheckWaitCondition(section.Index)) // reverse deadlocks and not waiting
                    {
                        foreach (Dictionary<int, int> deadlockList in value)
                        {
                            foreach (KeyValuePair<int, int> deadlock in deadlockList)
                            {
                                if (!deadlockTrains.Contains(deadlock.Key))
                                {
                                    TrackCircuitSection.TrackCircuitList[deadlock.Value].SetDeadlockTrap(train.Number, deadlock.Key);
                                }
                                else
                                {
                                    // check if train has reversal before end of path of other train
                                    if (train.TCRoute.TCRouteSubpaths.Count > (train.TCRoute.ActiveSubPath + 1))
                                    {
                                        Train otherTrain = Train.GetOtherTrainByNumber(deadlock.Key);

                                        bool commonSectionFound = false;
                                        for (int i = otherTrain.PresentPosition[Direction.Forward].RouteListIndex + 1;
                                             i < otherTrain.ValidRoutes[Direction.Forward].Count - 1 && !commonSectionFound;
                                             i++)
                                        {
                                            TrackCircuitSection otherSection = otherTrain.ValidRoutes[Direction.Forward][i].TrackCircuitSection;
                                            for (int j = train.PresentPosition[Direction.Forward].RouteListIndex; j < train.ValidRoutes[Direction.Forward].Count - 1; j++)
                                            {
                                                if (otherSection.Index == train.ValidRoutes[Direction.Forward][j].TrackCircuitSection.Index)
                                                {
                                                    commonSectionFound = true;
                                                    break;
                                                }
                                            }
                                            if (otherSection.CircuitState.TrainReserved == null || otherSection.CircuitState.TrainReserved.Train.Number != otherTrain.Number)
                                            {
                                                break;
                                            }
                                            //if (sectionIndex == otherTrain.LastReservedSection[0]) lastReserved = true;
                                        }

                                        if (!commonSectionFound)
                                        {
                                            TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[deadlock.Value];
                                            endSection.ClearDeadlockTrap(train.Number);
                                            section.ClearDeadlockTrap(otherTrain.Number);
                                            deadlockWait = false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return deadlockWait;
        }

        // Set train route to alternative route - location based deadlock processing
        internal protected void ClearDeadlocks()
        {
            // clear deadlocks
            foreach (KeyValuePair<int, List<Dictionary<int, int>>> deadlock in deadlockInfo)
            {
                TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[deadlock.Key];
                foreach (Dictionary<int, int> deadlockTrapInfo in deadlock.Value)
                {
                    foreach (KeyValuePair<int, int> deadlockedTrain in deadlockTrapInfo)
                    {
                        Train otherTrain = Train.GetOtherTrainByNumber(deadlockedTrain.Key);
                        if (otherTrain != null && otherTrain.TrainDeadlockInfo.deadlockInfo.TryGetValue(deadlockedTrain.Value, out List<Dictionary<int, int>> value))
                        {
                            List<Dictionary<int, int>> otherDeadlock = value;
                            for (int i = otherDeadlock.Count - 1; i >= 0; i--)
                            {
                                Dictionary<int, int> otherDeadlockInfo = otherDeadlock[i];
                                otherDeadlockInfo.Remove(train.Number);
                                if (otherDeadlockInfo.Count <= 0)
                                    otherDeadlock.RemoveAt(i);
                            }

                            if (otherDeadlock.Count <= 0)
                                otherTrain.TrainDeadlockInfo.deadlockInfo.Remove(deadlockedTrain.Value);

                            if (otherTrain.TrainDeadlockInfo.deadlockInfo.Count <= 0)
                                section.ClearDeadlockTrap(otherTrain.Number);
                        }
                        TrackCircuitSection otherSection = TrackCircuitSection.TrackCircuitList[deadlockedTrain.Value];
                        otherSection.ClearDeadlockTrap(train.Number);
                    }
                }
            }

            deadlockInfo.Clear();
        }

        // Check on deadlock
        internal void CheckDeadlock(TrackCircuitPartialPathRoute route, int number)
        {
            // clear existing deadlock info
            ClearDeadlocks();

            // build new deadlock info
            foreach (Train otherTrain in Simulator.Instance.Trains)
            {
                // check if not AI_Static
                if (Simulator.Instance.SignalEnvironment.UseLocationPassingPaths && otherTrain.GetAiMovementState() == AiMovementState.Static)
                {
                    continue;
                }

                if (otherTrain.Number != number && otherTrain.TrainType != TrainType.Static)
                {
                    TrackCircuitPartialPathRoute otherRoute = otherTrain.ValidRoutes[Direction.Forward];
                    ILookup<int, TrackDirection> otherRouteDict = otherRoute.ConvertRoute();

                    for (int i = 0; i < route.Count; i++)
                    {
                        TrackCircuitRouteElement routeElement = route[i];
                        TrackCircuitSection section = routeElement.TrackCircuitSection;
                        TrackDirection sectionDirection = routeElement.Direction;

                        if (section.CircuitType != TrackCircuitType.Crossover)
                        {
                            if (otherRouteDict.Contains(section.Index))
                            {
                                TrackDirection otherTrainDirection = otherRouteDict[section.Index].First();
                                //<CSComment> Right part of OR clause refers to initial placement with trains back-to-back and running away one from the other</CSComment>
                                if (otherTrainDirection == sectionDirection ||
                                    (train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex == otherTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex && section.Index == train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex &&
                                    train.PresentPosition[Direction.Backward].Offset + otherTrain.PresentPosition[Direction.Backward].Offset - 1 > section.Length))
                                {
                                    i = EndCommonSection(i, route, otherRoute);
                                }
                                else
                                {
                                    if (Simulator.Instance.SignalEnvironment.UseLocationPassingPaths) //new style location based logic
                                    {
                                        if (CheckRealDeadlockLocationBased(route, otherRoute, ref i))
                                        {
                                            int[] endDeadlock = SetDeadlockLocationBased(i, route, otherRoute, otherTrain);
                                            // use end of alternative path if set
                                            i = endDeadlock[1] > 0 ? --endDeadlock[1] : endDeadlock[0];
                                        }
                                    }
                                    else //old style path based logic
                                    {
                                        int[] endDeadlock = SetDeadlockPathBased(i, route, otherRoute, otherTrain);
                                        // use end of alternative path if set - if so, compensate for iElement++
                                        i = endDeadlock[1] > 0 ? --endDeadlock[1] : endDeadlock[0];
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Obtain deadlock details - old style path based logic
        private int[] SetDeadlockPathBased(int index, TrackCircuitPartialPathRoute route, TrackCircuitPartialPathRoute otherRoute, Train otherTrain)
        {
            int[] returnValue = new int[2];
            returnValue[1] = -1;  // set to no alternative path used

            TrackCircuitRouteElement firstElement = route[index];
            int firstSectionIndex = firstElement.TrackCircuitSection.Index;
            bool allreadyActive = false;

            int trainSection = firstSectionIndex;
            int otherTrainSection = firstSectionIndex;

            int trainIndex = index;
            int otherTrainIndex = otherRoute.GetRouteIndex(firstSectionIndex, 0);

            int firstIndex = trainIndex;
            int otherFirstIndex = otherTrainIndex;

            TrackCircuitRouteElement trainElement;
            TrackCircuitRouteElement otherTrainElement;

            // loop while not at end of route for either train and sections are equal
            // loop is also exited when alternative path is found for either train
            for (int i = 0; ((firstIndex + i) <= (route.Count - 1)) && ((otherFirstIndex - i)) >= 0 && (trainSection == otherTrainSection); i++)
            {
                trainIndex = firstIndex + i;
                otherTrainIndex = otherFirstIndex - i;

                trainElement = route[trainIndex];
                otherTrainElement = otherRoute[otherTrainIndex];
                trainSection = trainElement.TrackCircuitSection.Index;
                otherTrainSection = otherTrainElement.TrackCircuitSection.Index;

                if (trainElement.StartAlternativePath != null)
                {
                    int endAlternativeSection = trainElement.StartAlternativePath.TrackCircuitSection.Index;
                    returnValue[1] = route.GetRouteIndex(endAlternativeSection, index);
                    break;
                }

                if (otherTrainElement.EndAlternativePath != null)
                {
                    int endAlternativeSection = otherTrainElement.EndAlternativePath.TrackCircuitSection.Index;
                    returnValue[1] = route.GetRouteIndex(endAlternativeSection, index);
                    break;
                }

                TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[trainSection];
                if (section.IsSet(otherTrain, true))
                {
                    allreadyActive = true;
                }
            }

            // get sections on which loop ended
            trainElement = route[trainIndex];
            trainSection = trainElement.TrackCircuitSection.Index;

            otherTrainElement = otherRoute[otherTrainIndex];
            otherTrainSection = otherTrainElement.TrackCircuitSection.Index;

            // if last sections are still equal - end of route reached for one of the trains
            // otherwise, last common sections was previous sections for this train
            int lastSectionIndex = (trainSection == otherTrainSection) ? trainSection : route[trainIndex - 1].TrackCircuitSection.Index;

            // if section is not a junction, check if either route not ended, if so continue up to next junction
            TrackCircuitSection lastSection = TrackCircuitSection.TrackCircuitList[lastSectionIndex];
            if (lastSection.CircuitType != TrackCircuitType.Junction)
            {
                bool endSectionFound = false;
                if (trainIndex < (route.Count - 1))
                {
                    for (int i = trainIndex + 1; i < route.Count - 1 && !endSectionFound; i++)
                    {
                        lastSection = route[i].TrackCircuitSection;
                        endSectionFound = lastSection.CircuitType == TrackCircuitType.Junction;
                    }
                }
                else if (otherTrainIndex > 0)
                {
                    for (int i = otherTrainIndex - 1; i >= 0 && !endSectionFound; i--)
                    {
                        lastSection = otherRoute[i].TrackCircuitSection;
                        endSectionFound = lastSection.CircuitType == TrackCircuitType.Junction;
                        if (lastSection.IsSet(otherTrain, true))
                        {
                            allreadyActive = true;
                        }
                    }
                }
                lastSectionIndex = lastSection.Index;
            }

            // set deadlock info for both trains

            SetDeadlockInfo(firstSectionIndex, lastSectionIndex, otherTrain.Number);
            otherTrain.TrainDeadlockInfo.SetDeadlockInfo(lastSectionIndex, firstSectionIndex, train.Number);

            if (allreadyActive)
            {
                TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[lastSectionIndex];
                section.SetDeadlockTrap(otherTrain, otherTrain.TrainDeadlockInfo.deadlockInfo[lastSectionIndex]);
            }

            returnValue[0] = route.GetRouteIndex(lastSectionIndex, index);
            if (returnValue[0] < 0)
                returnValue[0] = trainIndex;
            return returnValue;
        }

        // Obtain deadlock details - new style location based logic
        private int[] SetDeadlockLocationBased(int index, TrackCircuitPartialPathRoute route, TrackCircuitPartialPathRoute otherRoute, Train otherTrain)
        {
            int[] returnValue = new int[2];
            returnValue[1] = -1;  // set to no alternative path used

            TrackCircuitRouteElement firstElement = route[index];
            int firstSectionIndex = firstElement.TrackCircuitSection.Index;
            bool alreadyActive = false;

            int trainSectionIndex;
            int otherTrainSectionIndex;

            // double index variables required as last valid index must be known when exiting loop
            int trainIndex = index;
            int trainNextIndex = trainIndex;
            int otherTrainIndex = otherRoute.GetRouteIndex(firstSectionIndex, 0);
            int otherTrainNextIndex = otherTrainIndex;
            int otherFirstIndex = otherTrainIndex;

            TrackCircuitRouteElement trainElement;
            TrackCircuitRouteElement otherTrainElement;

            bool validPassLocation = false;
            int endSectionRouteIndex = -1;

            bool endOfLoop = false;

            // loop while not at end of route for either train and sections are equal
            // loop is also exited when alternative path is found for either train
            while (!endOfLoop)
            {
                trainIndex = trainNextIndex;
                trainElement = route[trainIndex];
                otherTrainIndex = otherTrainNextIndex;
                trainSectionIndex = trainElement.TrackCircuitSection.Index;

                otherTrainElement = otherRoute[otherTrainIndex];
                otherTrainSectionIndex = otherTrainElement.TrackCircuitSection.Index;

                TrackCircuitSection section = otherTrainElement.TrackCircuitSection;

                // if sections not equal : test length of next not-common section, if long enough then exit loop
                if (trainSectionIndex != otherTrainSectionIndex)
                {
                    int nextThisRouteIndex = trainIndex;
                    TrackCircuitSection passLoopSection = train.ValidRoutes[Direction.Forward][nextThisRouteIndex].TrackCircuitSection;
                    _ = otherRoute.GetRouteIndex(passLoopSection.Index, otherTrainIndex);

                    float passLength = passLoopSection.Length;
                    bool endOfPassLoop = false;

                    while (!endOfPassLoop)
                    {
                        // loop is longer as at least one of the trains so is valid
                        if (passLength > train.Length || passLength > otherTrain.Length)
                        {
                            endOfPassLoop = true;
                            endOfLoop = true;
                        }

                        // get next section
                        else if (nextThisRouteIndex < train.ValidRoutes[Direction.Forward].Count - 2)
                        {
                            nextThisRouteIndex++;
                            passLoopSection = train.ValidRoutes[Direction.Forward][nextThisRouteIndex].TrackCircuitSection;
                            int nextOtherRouteIndex = otherRoute.GetRouteIndexBackward(passLoopSection.Index, otherTrainIndex);

                            // new common section after too short loop - not a valid deadlock point
                            if (nextOtherRouteIndex >= 0)
                            {
                                endOfPassLoop = true;
                                trainNextIndex = nextThisRouteIndex;
                                otherTrainNextIndex = nextOtherRouteIndex;
                            }
                            else
                            {
                                passLength += passLoopSection.Length;
                            }
                        }
                        // end of route
                        else
                        {
                            endOfPassLoop = true;
                            endOfLoop = true;
                        }
                    }
                }
                // if section is a deadlock boundary, check available paths for both trains
                else
                {

                    List<int> trainAllocatedPaths = new List<int>();
                    List<int> otherTrainAllocatedPaths = new List<int>();

                    bool gotoNextSection = true;

                    if (section.DeadlockReference >= 0 && trainElement.FacingPoint) // test for facing points only
                    {
                        bool trainFits = false;
                        bool otherTrainFits = false;

                        int endSectionIndex = -1;

                        validPassLocation = true;

                        // get allocated paths for this train
                        DeadlockInfo deadlockInfo = Simulator.Instance.SignalEnvironment.DeadlockInfoList[section.DeadlockReference];

                        // get allocated paths for this train - if none yet set, create references
                        int trainReferenceIndex = deadlockInfo.GetTrainAndSubpathIndex(train.Number, train.TCRoute.ActiveSubPath);
                        if (!deadlockInfo.TrainReferences.ContainsKey(trainReferenceIndex))
                        {
                            deadlockInfo.SetTrainDetails(train.Number, train.TCRoute.ActiveSubPath, train.Length, train.ValidRoutes[Direction.Forward], trainIndex);
                        }

                        // if valid path for this train
                        if (deadlockInfo.TrainReferences.ContainsKey(trainReferenceIndex))
                        {
                            trainAllocatedPaths = deadlockInfo.TrainReferences[deadlockInfo.GetTrainAndSubpathIndex(train.Number, train.TCRoute.ActiveSubPath)];

                            // if paths available, get end section and check train against shortest path
                            if (trainAllocatedPaths.Count > 0)
                            {
                                endSectionIndex = deadlockInfo.AvailablePathList[trainAllocatedPaths[0]].EndSectionIndex;
                                endSectionRouteIndex = route.GetRouteIndex(endSectionIndex, trainIndex);
                                Dictionary<int, bool> trainFitList = deadlockInfo.TrainLengthFit[deadlockInfo.GetTrainAndSubpathIndex(train.Number, train.TCRoute.ActiveSubPath)];
                                foreach (int i in trainAllocatedPaths)
                                {
                                    if (trainFitList[i])
                                    {
                                        trainFits = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            validPassLocation = false;
                        }

                        // get allocated paths for other train - if none yet set, create references
                        int otherTrainReferenceIndex = deadlockInfo.GetTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath);
                        if (!deadlockInfo.TrainReferences.ContainsKey(otherTrainReferenceIndex))
                        {
                            int otherTrainElementIndex = otherTrain.ValidRoutes[Direction.Forward].GetRouteIndexBackward(endSectionIndex, otherFirstIndex);
                            if (otherTrainElementIndex < 0) // train joins deadlock area on different node
                            {
                                validPassLocation = false;
                                deadlockInfo.RemoveTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath); // remove index as train has no valid path
                            }
                            else
                            {
                                deadlockInfo.SetTrainDetails(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath, otherTrain.Length,
                                    otherTrain.ValidRoutes[Direction.Forward], otherTrainElementIndex);
                            }
                        }

                        // if valid path for other train
                        if (validPassLocation && deadlockInfo.TrainReferences.ContainsKey(otherTrainReferenceIndex))
                        {
                            otherTrainAllocatedPaths =
                                deadlockInfo.TrainReferences[deadlockInfo.GetTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath)];

                            // if paths available, get end section (if not yet set) and check train against shortest path
                            if (otherTrainAllocatedPaths.Count > 0)
                            {
                                if (endSectionRouteIndex < 0)
                                {
                                    endSectionIndex = deadlockInfo.AvailablePathList[otherTrainAllocatedPaths[0]].EndSectionIndex;
                                    endSectionRouteIndex = route.GetRouteIndex(endSectionIndex, trainIndex);
                                }

                                Dictionary<int, bool> otherTrainFitList =
                                    deadlockInfo.TrainLengthFit[deadlockInfo.GetTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath)];
                                foreach (int iPath in otherTrainAllocatedPaths)
                                {
                                    if (otherTrainFitList[iPath])
                                    {
                                        otherTrainFits = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        // other train has no valid path relating to the passing path, so passing not possible
                        {
                            validPassLocation = false;
                        }

                        // if both trains have only one route, make sure it's not the same (inverse) route

                        if (trainAllocatedPaths.Count == 1 && otherTrainAllocatedPaths.Count == 1)
                        {
                            if (deadlockInfo.InverseInfo.TryGetValue(trainAllocatedPaths[0], out int value) && value == otherTrainAllocatedPaths[0])
                            {
                                validPassLocation = false;
                            }
                        }

                        // if there are passing paths and at least one train fits in shortest path, it is a valid location so break loop
                        if (validPassLocation)
                        {
                            gotoNextSection = false;
                            if (trainFits || otherTrainFits)
                            {
                                if (section.IsSet(otherTrain, true))
                                {
                                    alreadyActive = true;
                                }
                                endOfLoop = true;
                            }
                            else
                            {
                                trainNextIndex = endSectionRouteIndex;
                                otherTrainNextIndex = otherRoute.GetRouteIndexBackward(endSectionIndex, otherTrainIndex);
                                if (otherTrainNextIndex < 0)
                                    endOfLoop = true;
                            }
                        }
                    }

                    // if loop not yet ended - not a valid pass location, move to next section (if available)

                    if (gotoNextSection)
                    {
                        // if this section is occupied by other train, break loop - further checks are of no use
                        if (section.IsSet(otherTrain, true))
                        {
                            alreadyActive = true;
                            endOfLoop = true;
                        }
                        else
                        {
                            trainNextIndex++;
                            otherTrainNextIndex--;

                            if (trainNextIndex > route.Count - 1 || otherTrainNextIndex < 0)
                            {
                                endOfLoop = true; // end of path reached for either train
                            }
                        }
                    }
                }
            }

            // if valid pass location : set return index

            if (validPassLocation && endSectionRouteIndex >= 0)
            {
                returnValue[1] = endSectionRouteIndex;
            }

            // get sections on which loop ended
            trainElement = route[trainIndex];
            trainSectionIndex = trainElement.TrackCircuitSection.Index;

            otherTrainElement = otherRoute[otherTrainIndex];
            otherTrainSectionIndex = otherTrainElement.TrackCircuitSection.Index;

            // if last sections are still equal - end of route reached for one of the trains
            // otherwise, last common sections was previous sections for this train
            TrackCircuitSection lastSection = (trainSectionIndex == otherTrainSectionIndex) ? TrackCircuitSection.TrackCircuitList[trainSectionIndex] :
                route[trainIndex - 1].TrackCircuitSection;

            // TODO : if section is not a junction but deadlock is already active, wind back to last junction
            // if section is not a junction, check if either route not ended, if so continue up to next junction
            if (lastSection.CircuitType != TrackCircuitType.Junction)
            {
                bool endSectionFound = false;
                if (trainIndex < (route.Count - 1))
                {
                    for (int iIndex = trainIndex; iIndex < route.Count - 1 && !endSectionFound; iIndex++)
                    {
                        lastSection = route[iIndex].TrackCircuitSection;
                        endSectionFound = lastSection.CircuitType == TrackCircuitType.Junction;
                    }
                }

                else if (otherTrainIndex > 0)
                {
                    for (int i = otherTrainIndex; i >= 0 && !endSectionFound; i--)
                    {
                        lastSection = otherRoute[i].TrackCircuitSection;
                        endSectionFound = false;

                        // junction found - end of loop
                        if (lastSection.CircuitType == TrackCircuitType.Junction)
                        {
                            endSectionFound = true;
                        }
                        // train has active wait condition at this location - end of loop
                        else if (otherTrain.CheckWaitCondition(lastSection.Index))
                        {
                            endSectionFound = true;
                        }

                        if (lastSection.IsSet(otherTrain, true))
                        {
                            alreadyActive = true;
                        }
                    }
                }
            }

            // set deadlock info for both trains
            SetDeadlockInfo(firstSectionIndex, lastSection.Index, otherTrain.Number);
            otherTrain.TrainDeadlockInfo.SetDeadlockInfo(lastSection.Index, firstSectionIndex, train.Number);

            if (alreadyActive)
            {
                lastSection.SetDeadlockTrap(otherTrain, otherTrain.TrainDeadlockInfo.deadlockInfo[lastSection.Index]);
                returnValue[1] = route.Count;  // set beyond end of route - no further checks required
            }

            // if any section occupied by own train, reverse deadlock is active
            TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[firstSectionIndex];

            int firstRouteIndex = train.ValidRoutes[Direction.Forward].GetRouteIndex(firstSectionIndex, 0);
            int lastRouteIndex = train.ValidRoutes[Direction.Forward].GetRouteIndex(lastSection.Index, 0);

            for (int i = firstRouteIndex; i < lastRouteIndex; i++)
            {
                TrackCircuitSection partSection = train.ValidRoutes[Direction.Forward][i].TrackCircuitSection;
                if (partSection.IsSet(train, true))
                {
                    firstSection.SetDeadlockTrap(train, deadlockInfo[firstSectionIndex]);
                }
            }

            returnValue[0] = route.GetRouteIndex(lastSection.Index, index);
            if (returnValue[0] < 0)
                returnValue[0] = trainIndex;
            return returnValue;
        }

        // Get end of common section
        private static int EndCommonSection(int index, TrackCircuitPartialPathRoute route, TrackCircuitPartialPathRoute otherRoute)
        {
            int firstSection = route[index].TrackCircuitSection.Index;

            int trainSection = firstSection;
            int otherTrainSection = firstSection;

            int trainIndex = index;
            int otherTrainIndex = otherRoute.GetRouteIndex(firstSection, 0);

            while (trainSection == otherTrainSection && trainIndex < (route.Count - 1) && otherTrainIndex > 0)
            {
                trainIndex++;
                otherTrainIndex--;
                trainSection = route[trainIndex].TrackCircuitSection.Index;
                otherTrainSection = otherRoute[otherTrainIndex].TrackCircuitSection.Index;
            }

            return trainIndex;
        }
    }
}
