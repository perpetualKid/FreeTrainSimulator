// COPYRIGHT 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.World;

namespace Orts.Simulation.RollingStocks.SubSystems
{
    /// <summary>
    /// An FreightAnimations object is created for any engine or wagon having an 
    /// ORTSFreightAnims block in its ENG/WAG file. It contains a collection of
    /// FreightAnimation objects.
    /// Called from within the MSTSWagon class.
    /// </summary>
    public class FreightAnimations
    {
        public List<FreightAnimation> Animations { get; } = new List<FreightAnimation>();
        public List<FreightAnimationDiscrete> EmptyAnimations { get; } = new List<FreightAnimationDiscrete>();
        public float FreightWeight { get; internal set; }
        public float StaticFreightWeight { get; private set; }
        public PickupType FreightType { get; internal set; }
        public bool MSTSFreightAnimEnabled { get; private set; } = true;
        public float WagonEmptyWeight { get; private set; } = -1;
        public FreightAnimationContinuous LoadedOne { get; internal set; }
        public FreightAnimationContinuous FullPhysicsContinuousOne { get; private set; } // Allow reading of full physics parameters for continuous freight animation
        public FreightAnimationStatic FullPhysicsStaticOne { get; private set; } // Allow reading of full physics for static freight animation
        public FreightAnimationDiscrete DiscreteLoadedOne { get; private set; }
        public float LoadingStartDelay { get; private set; }
        public float LoadingEndDelay { get; private set; }
        public float UnloadingStartDelay { get; private set; }
        public bool IsGondola { get; private set; }
        public float LoadingAreaLength { get; private set; } = 12.19f;
        public float AboveLoadingAreaLength { get; private set; } = 12.19f;
        public Vector3 Offset { get; private set; }
        public IntakePoint GeneralIntakePoint { get; private set; }
        public bool DoubleStacker { get; private set; }
        public MSTSWagon Wagon { get; private set; }
        public const float EmptySuperpositionTolerance = 0.10f;
        public List<LoadData> LoadDataList { get; private set; }
        public const float FullSuperpositionTolerance = 0.2f;

        // additions to manage consequences of variable weight on friction and brake forces
        public float EmptyORTSDavis_A { get; private set; } = -9999;
        public float EmptyORTSDavis_B { get; private set; } = -9999;
        public float EmptyORTSDavis_C { get; private set; } = -9999;
        public float EmptyORTSWagonFrontalAreaM2 { get; private set; } = -9999;
        public float EmptyORTSDavisDragConstant { get; private set; } = -9999;
        public float EmptyMaxBrakeForceN { get; private set; } = -9999;
        public float EmptyMaxHandbrakeForceN { get; private set; } = -9999;
        public float EmptyCentreOfGravityM_Y { get; private set; } = -9999; // get centre of gravity after adjusted for freight animation
        public bool ContinuousFreightAnimationsPresent { get; private set; } // Flag to indicate that a continuous freight animation is present
        public bool StaticFreightAnimationsPresent { get; private set; } // Flag to indicate that a static freight animation is present
        public bool DiscreteFreightAnimationsPresent { get; private set; } // Flag to indicate that a discrete freight animation is present

        public FreightAnimations(STFReader stf, MSTSWagon wagon)
        {
            ArgumentNullException.ThrowIfNull(stf);

            Wagon = wagon;
            stf.MustMatch("(");
            bool empty = true;
            stf.ParseBlock(new[] {
                new STFReader.TokenProcessor("mstsfreightanimenabled", ()=>{ MSTSFreightAnimEnabled = stf.ReadBoolBlock(true);}),
                new STFReader.TokenProcessor("wagonemptyweight", ()=>{ WagonEmptyWeight = stf.ReadFloatBlock(STFReader.Units.Mass, -1); }),
                new STFReader.TokenProcessor("loadingstartdelay", ()=>{ UnloadingStartDelay = stf.ReadFloatBlock(STFReader.Units.None, 0); }),
                new STFReader.TokenProcessor("loadingenddelay", ()=>{ LoadingEndDelay = stf.ReadFloatBlock(STFReader.Units.None, 0); }),
                new STFReader.TokenProcessor("unloadingstartdelay", ()=>{ UnloadingStartDelay = stf.ReadFloatBlock(STFReader.Units.None, 0); }),
                new STFReader.TokenProcessor("isgondola", ()=>{ IsGondola = stf.ReadBoolBlock(false);}),
                new STFReader.TokenProcessor("loadingarealength", ()=>{ LoadingAreaLength = stf.ReadFloatBlock(STFReader.Units.Distance, 12.19f); }),
                new STFReader.TokenProcessor("aboveloadingarealength", ()=>{ AboveLoadingAreaLength = stf.ReadFloatBlock(STFReader.Units.Distance, 12.19f); }),
                new STFReader.TokenProcessor("intakepoint", ()=>
                {
                    GeneralIntakePoint = new IntakePoint(stf);
                }),
                new STFReader.TokenProcessor("offset", ()=>
                {
                    Offset = stf.ReadVector3Block(STFReader.Units.Distance,  new Vector3(0, 0, 0));
                    Offset *= new Vector3(1, 1,-1); // MSTS --> XNA
                }),
                new STFReader.TokenProcessor("doublestacker", ()=>{ DoubleStacker = stf.ReadBoolBlock(true);}),
                // additions to manage consequences of variable weight on friction and brake forces
                new STFReader.TokenProcessor("emptyortsdavis_a", ()=>{ EmptyORTSDavis_A = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
                new STFReader.TokenProcessor("emptyortsdavis_b", ()=>{ EmptyORTSDavis_B = stf.ReadFloatBlock(STFReader.Units.Resistance, -1); }),
                new STFReader.TokenProcessor("emptyortsdavis_c", ()=>{ EmptyORTSDavis_C = stf.ReadFloatBlock(STFReader.Units.ResistanceDavisC, -1); }),
                new STFReader.TokenProcessor("emptyortswagonfrontalarea", ()=>{ EmptyORTSWagonFrontalAreaM2 = stf.ReadFloatBlock(STFReader.Units.AreaDefaultFT2, -1); }),
                new STFReader.TokenProcessor("emptyortsdavisdragconstant", ()=>{ EmptyORTSDavisDragConstant = stf.ReadFloatBlock(STFReader.Units.Any, -1); }),
                new STFReader.TokenProcessor("emptymaxbrakeforce", ()=>{ EmptyMaxBrakeForceN = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
                new STFReader.TokenProcessor("emptymaxhandbrakeforce", ()=>{ EmptyMaxHandbrakeForceN = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
                new STFReader.TokenProcessor("emptycentreofgravity_y", ()=>{ EmptyCentreOfGravityM_Y = stf.ReadFloatBlock(STFReader.Units.Distance, -1); }),
                new STFReader.TokenProcessor("freightanimcontinuous", ()=>
                {
                    Animations.Add(new FreightAnimationContinuous(stf, wagon));
                    FullPhysicsContinuousOne = Animations.Last() as FreightAnimationContinuous;
                    if (wagon.WeightLoadController == null) wagon.WeightLoadController = new MSTSNotchController(0, 1, 0.01f);
                    if ((Animations.Last() as FreightAnimationContinuous).FullAtStart)
                    {
                        if (empty)
                        {
                            empty = false;
                            FreightType = wagon.IntakePointList.Last().Type;
                            LoadedOne = Animations.Last() as FreightAnimationContinuous;
                            FreightWeight += LoadedOne.FreightWeightWhenFull;
                            LoadedOne.LoadPerCent = 100;
                        }
                        else
                        {
                            (Animations.Last() as FreightAnimationContinuous).FullAtStart = false;
                            Trace.TraceWarning("The wagon can't be full with two different materials, only first is retained");
                        }
                    }
                    ContinuousFreightAnimationsPresent = true;
                 }),
                new STFReader.TokenProcessor("freightanimstatic", ()=>
                {
                    Animations.Add(new FreightAnimationStatic(stf));
                    StaticFreightWeight += (Animations.Last() as FreightAnimationStatic).FreightWeight;
                    StaticFreightAnimationsPresent = true;
                    FullPhysicsStaticOne = Animations.Last() as FreightAnimationStatic;
                }),
                new STFReader.TokenProcessor("loaddata", ()=>
                {
                    stf.MustMatch("(");
                    LoadDataList ??= new List<LoadData>();
                    LoadDataList.Add(new LoadData(stf));
                }),
            });
        }

        /// <summary>
        /// Saves the general variable parameters
        /// Called from within the MSTSWagon class.
        /// </summary>
        public void Save(BinaryWriter outf)
        {
            outf.Write(FreightWeight);
            outf.Write((int)FreightType);
            outf.Write(StaticFreightWeight);
            int discreteAnimCount = 0;
            foreach (FreightAnimation freightAnim in Animations)
            {
                if (freightAnim is FreightAnimationDiscrete)
                    discreteAnimCount++;
            }
            outf.Write(discreteAnimCount);
            foreach (FreightAnimation freightAnim in Animations)
            {
                if (freightAnim is FreightAnimationDiscrete freightAnimationDiscrete)
                {
                    freightAnimationDiscrete.Save(outf);
                }
            }
            outf.Write(EmptyAnimations.Count);
            foreach (FreightAnimationDiscrete emptyAnim in EmptyAnimations)
                emptyAnim.Save(outf);
        }

        /// <summary>
        /// Restores the general variable parameters
        /// Called from within the MSTSWagon class.
        /// </summary>
        public void Restore(BinaryReader inf)
        {
            FreightWeight = inf.ReadSingle();
            FreightType = (PickupType)inf.ReadInt32();
            StaticFreightWeight = inf.ReadSingle();
            LoadedOne = null;
            foreach (FreightAnimation freightAnim in Animations)
            {
                if (freightAnim is FreightAnimationContinuous freightAnimationContinuous && freightAnimationContinuous.LinkedIntakePoint != null)
                {
                    if (freightAnimationContinuous.LinkedIntakePoint.Type == FreightType)
                    {
                        LoadedOne = freightAnimationContinuous;
                        LoadedOne.LoadPerCent = FreightWeight / LoadedOne.FreightWeightWhenFull * 100;
                    }
                    else
                    {
                        freightAnimationContinuous.LoadPerCent = 0;
                    }
                }
            }
            int discreteAnimCount = inf.ReadInt32();
            if (discreteAnimCount > 0)
            {
                for (int i = 0; i < discreteAnimCount; i++)
                {
                    FreightAnimationDiscrete discreteFreightAnim = new FreightAnimationDiscrete(inf, this);
                    Animations.Add(discreteFreightAnim);
                }
            }
            int emptyAnimCount = inf.ReadInt32();
            if (emptyAnimCount > 0)
            {
                for (int i = 0; i < emptyAnimCount; i++)
                {
                    FreightAnimationDiscrete emptyFreightAnim = new FreightAnimationDiscrete(inf, this);
                    EmptyAnimations.Add(emptyFreightAnim);
                }
            }
        }

        public FreightAnimations(FreightAnimations source, MSTSWagon wagon)
        {
            ArgumentNullException.ThrowIfNull(source);

            Wagon = wagon;
            foreach (FreightAnimation freightAnim in source.Animations)
            {
                if (freightAnim is FreightAnimationContinuous)
                {
                    Animations.Add(new FreightAnimationContinuous(freightAnim as FreightAnimationContinuous, wagon));
                    if ((Animations.Last() as FreightAnimationContinuous).FullAtStart)
                        LoadedOne = Animations.Last() as FreightAnimationContinuous;

                }
                else if (freightAnim is FreightAnimationStatic)
                {
                    Animations.Add(new FreightAnimationStatic(freightAnim as FreightAnimationStatic));
                }
            }
            FreightWeight = source.FreightWeight;
            FreightType = source.FreightType;
            MSTSFreightAnimEnabled = source.MSTSFreightAnimEnabled;
            WagonEmptyWeight = source.WagonEmptyWeight;
            LoadingStartDelay = source.LoadingStartDelay;
            UnloadingStartDelay = source.UnloadingStartDelay;
            IsGondola = source.IsGondola;
            LoadingAreaLength = source.LoadingAreaLength;
            AboveLoadingAreaLength = source.AboveLoadingAreaLength;
            Offset = source.Offset;
            if (source.GeneralIntakePoint != null)
                GeneralIntakePoint = new IntakePoint(source.GeneralIntakePoint);
            DoubleStacker = source.DoubleStacker;
            if (source.LoadDataList?.Count >= 0)
            {
                LoadDataList ??= new List<LoadData>();
                LoadDataList.AddRange(source.LoadDataList);
            }

            // additions to manage consequences of variable weight on friction and brake forces
            EmptyORTSDavis_A = source.EmptyORTSDavis_A;
            EmptyORTSDavis_B = source.EmptyORTSDavis_B;
            EmptyORTSDavis_C = source.EmptyORTSDavis_C;
            EmptyORTSWagonFrontalAreaM2 = source.EmptyORTSWagonFrontalAreaM2;
            EmptyORTSDavisDragConstant = source.EmptyORTSDavisDragConstant;
            EmptyMaxBrakeForceN = source.EmptyMaxBrakeForceN;
            EmptyMaxHandbrakeForceN = source.EmptyMaxHandbrakeForceN;
            EmptyCentreOfGravityM_Y = source.EmptyCentreOfGravityM_Y;
            ContinuousFreightAnimationsPresent = source.ContinuousFreightAnimationsPresent;
            StaticFreightAnimationsPresent = source.StaticFreightAnimationsPresent;
            DiscreteFreightAnimationsPresent = source.DiscreteFreightAnimationsPresent;
        }

        public void Load(string loadFilePath, LoadPosition loadPosition, LoadState loadState)
        {
            ContainerManager containerManager = Simulator.Instance.ContainerManager;

            if (GeneralIntakePoint.Type == PickupType.Container)
            {
                Container container;
                container = new Container(Wagon, loadFilePath);
                if (containerManager.LoadedContainers.TryGetValue(loadFilePath, out Container value))
                {
                    container.Copy(value);
                }
                else
                {
                    container.LoadFromContainerFile(loadFilePath, Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder);
                    containerManager.LoadedContainers.Add(loadFilePath, container);
                }
                container.ComputeLoadWeight(loadState);

                Vector3 offset;
                var validity = Validity(Wagon, container, loadPosition, Offset, LoadingAreaLength, out offset);
                if (validity)
                {
                    var freightAnimDiscrete = new FreightAnimationDiscrete(this, container, loadPosition, offset);
                    Animations.Add(freightAnimDiscrete);
                    container.ComputeWorldPosition(freightAnimDiscrete);
                    containerManager.Containers.Add(container);
                    UpdateEmptyFreightAnims(container.LengthM);
                }
                else
                    Trace.TraceWarning($"Container {container.ShapeFileName} could not be allocated on wagon {Wagon.WagFilePath} with ID {Wagon.CarID}");
            }
            else
                Trace.TraceWarning("No match between wagon and load");
        }

        public void Load(IList<LoadData> loadDataList, bool listInWagFile = false)
        {
            if (GeneralIntakePoint == null)
                return;
            string TrainsetFolder = Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder;
            if (!(listInWagFile && Wagon.Train.TrainType == TrainType.Remote))
            {
                if (loadDataList?.Count > 0)

                {
                    foreach (LoadData loadData in loadDataList)
                    {
                        string loadFilePath = Path.Combine(TrainsetFolder, loadData.Folder, Path.ChangeExtension(loadData.Name, ".load-or"));
                        if (!File.Exists(loadFilePath))
                        {
                            Trace.TraceWarning($"Ignored missing load {loadFilePath}");
                            continue;
                        }
                        Load(loadFilePath, loadData.LoadPosition, loadData.LoadState);
                    }
                }
            }
            if (listInWagFile)
                return;
            if (!Animations.OfType<FreightAnimationDiscrete>().Any())
                // generate an empty freightAnim
                EmptyAnimations.Add(new FreightAnimationDiscrete(this, LoadPosition.Center));
            EmptyAbove();
            if (!listInWagFile)
                Wagon.UpdateLoadPhysics();
        }

        public void EmptyAbove()
        {
            if (!DoubleStacker)
                return;
            bool aboveAllowed = AboveAllowed();
            if (aboveAllowed)
            {
                foreach (var animation in Animations)
                {
                    if (animation is FreightAnimationDiscrete discreteAnimation && discreteAnimation.LoadPosition == LoadPosition.Above)
                    {
                        aboveAllowed = false;
                        break;
                    }
                }
            }
            if (aboveAllowed)
                // generate an empty freightAnim
                EmptyAnimations.Add(new FreightAnimationDiscrete(this, LoadPosition.Above));
        }

        public bool Validity(MSTSWagon wagon, Container container, LoadPosition loadPosition, Vector3 inOffset, float loadingAreaLength, out Vector3 offset)
        {
            offset = inOffset;
            var validity = false;
            var zOffset = 0f;
            var freightAnimDiscreteCount = 0;
            switch (loadPosition)
            {
                case LoadPosition.Center:
                    break;
                case LoadPosition.CenterRear:
                    zOffset += container.LengthM / 2;
                    break;
                case LoadPosition.CenterFront:
                    zOffset -= container.LengthM / 2;
                    break;
                case LoadPosition.Rear:
                    zOffset += (LoadingAreaLength - container.LengthM) / 2;
                    break;
                case LoadPosition.Front:
                    zOffset -= (LoadingAreaLength - container.LengthM) / 2;
                    break;
                case LoadPosition.Above:
                    if (container.ContainerType == ContainerType.C20ft || !DoubleStacker)
                        return false;
                    float heightBelow = 0.0f;
                    ContainerType contType = ContainerType.C20ft;
                    foreach (var animation in Animations)
                    {
                        if (animation is FreightAnimationDiscrete freightAnimationDiscrete)
                        {
                            if (freightAnimationDiscrete.LoadPosition == LoadPosition.Above)
                                return false;
                            if (heightBelow != 0 && freightAnimationDiscrete.Container.HeightM != heightBelow)
                                return false;
                            heightBelow = freightAnimationDiscrete.Container.HeightM;
                            freightAnimDiscreteCount++;
                            contType = freightAnimationDiscrete.Container.ContainerType;
                        }
                    }
                    if (freightAnimDiscreteCount == 0 || freightAnimDiscreteCount == 1 && contType == ContainerType.C20ft)
                        return false;
                    if (heightBelow != 0)
                        offset.Y += heightBelow;
                    else
                        return false;
                    break;
            }
            offset.Z = Offset.Z + zOffset;
            if (container.LengthM > loadingAreaLength + 0.01f && loadPosition != LoadPosition.Above)
                return false;
            if (container.LengthM > AboveLoadingAreaLength && loadPosition == LoadPosition.Above)
                return false;
            if (container.LengthM > LoadingAreaLength / 2 + 0.01f && (loadPosition == LoadPosition.CenterFront ||
                loadPosition == LoadPosition.CenterRear))
                return false;
            if (Animations.Count == 0 && loadPosition != LoadPosition.Above)
                return true;
            freightAnimDiscreteCount = 0;
            foreach (var animation in Animations)
                if (animation is FreightAnimationDiscrete)
                    freightAnimDiscreteCount++;
            if (freightAnimDiscreteCount == 0 && loadPosition != LoadPosition.Above)
                return true;
            // there are already other containers present; check that there aren't superpositions
            if (loadPosition == LoadPosition.Above)
                return true;
            foreach (var animation in Animations)
            {
                if (animation is FreightAnimationDiscrete animationDiscrete)
                    if (animationDiscrete.LoadPosition != LoadPosition.Above && Math.Abs(offset.Z - (animationDiscrete).Offset.Z) + 0.01f <
                        (container.LengthM + animationDiscrete.Container.LengthM) / 2)
                        return false;
                    else
                        return true;
            }
            return validity;
        }

        public void UpdateEmptyFreightAnims(float containerLengthM)
        {
            var anim = Animations.Last() as FreightAnimationDiscrete;
            if (anim.LoadPosition == LoadPosition.Above)
            {
                if (EmptyAnimations.Count > 0 && EmptyAnimations.Last().LoadPosition == LoadPosition.Above)
                {
                    anim.Wagon.IntakePointList.Remove(EmptyAnimations.Last().LinkedIntakePoint);
                    EmptyAnimations.RemoveAt(EmptyAnimations.Count - 1);
                }
                return;
            }
            if (EmptyAnimations.Count == 1 && EmptyAnimations[0].LoadPosition == LoadPosition.Center &&
                EmptyAnimations[0].LoadingAreaLength == LoadingAreaLength)
            {
                anim.Wagon.IntakePointList.Remove(EmptyAnimations[0].LinkedIntakePoint);
                EmptyAnimations.RemoveAt(0);
            }
            if (EmptyAnimations.Count == 0)
            {
                if (anim.LoadPosition == LoadPosition.Above)
                    return;
                if (containerLengthM < LoadingAreaLength - EmptySuperpositionTolerance)
                {
                    Vector3 offset = anim.Offset;
                    switch (anim.LoadPosition)
                    {
                        case LoadPosition.Center:
                            if ((LoadingAreaLength + EmptySuperpositionTolerance - anim.Container.LengthM) / 2 > Container.Length20ftM)
                            {
                                // one empty area behind, one in front
                                var emptyLength = (LoadingAreaLength - anim.Container.LengthM) / 2;
                                offset.Z = Offset.Z + LoadingAreaLength / 2 - (LoadingAreaLength - anim.Container.LengthM) / 4;
                                EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, LoadPosition.Rear, offset, emptyLength));
                                offset.Z = Offset.Z - LoadingAreaLength / 2 + (LoadingAreaLength - anim.Container.LengthM) / 4;
                                EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, LoadPosition.Front, offset, emptyLength));
                            }
                            break;
                        case LoadPosition.CenterRear:
                            // one empty area in front, check if enough place for the rear one
                            offset = Offset;
                            offset.Z -= LoadingAreaLength / 4;
                            EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, DoubleStacker ? LoadPosition.CenterFront : LoadPosition.Front,
                                offset, LoadingAreaLength / 2));
                            if (LoadingAreaLength / 2 + 0.01f - containerLengthM > 6.10)
                            {
                                offset.Z = Offset.Z + anim.Container.LengthM / 2 + LoadingAreaLength / 4;
                                EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, LoadPosition.Rear, offset,
                                    LoadingAreaLength / 2 - containerLengthM));
                            }
                            break;
                        case LoadPosition.CenterFront:
                            offset = Offset;
                            offset.Z += LoadingAreaLength / 4;
                            EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, DoubleStacker ? LoadPosition.CenterRear : LoadPosition.Rear,
                                offset, LoadingAreaLength / 2));
                            if (LoadingAreaLength / 2 + EmptySuperpositionTolerance - containerLengthM > Container.Length20ftM)
                            {
                                offset.Z = Offset.Z - containerLengthM / 2 - LoadingAreaLength / 4;
                                EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, LoadPosition.Front, offset,
                                    LoadingAreaLength / 2 - containerLengthM));
                            }
                            break;
                        case LoadPosition.Rear:
                            if (LoadingAreaLength + EmptySuperpositionTolerance - containerLengthM > Container.Length20ftM)
                            {
                                offset = Offset;
                                offset.Z -= anim.Container.LengthM / 2;
                                EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, LoadPosition.Front, offset,
                                LoadingAreaLength - containerLengthM));
                            }
                            break;
                        case LoadPosition.Front:
                            if (LoadingAreaLength + EmptySuperpositionTolerance - containerLengthM > Container.Length20ftM)
                            {
                                offset = Offset;
                                offset.Z += containerLengthM / 2;
                                EmptyAnimations.Add(new FreightAnimationDiscrete(this, null, LoadPosition.Rear, offset,
                                LoadingAreaLength - containerLengthM));
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                List<FreightAnimationDiscrete> deletableEmptyAnims = new List<FreightAnimationDiscrete>();

                // more complex case, there is more than one container present at the floor level
                foreach (var emptyAnim in EmptyAnimations)
                {
                    if (emptyAnim.LoadPosition == anim.LoadPosition && emptyAnim.LoadingAreaLength <= anim.LoadingAreaLength + 5)
                    {
                        anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                        deletableEmptyAnims.Add(emptyAnim);
                        continue;
                    }
                    if (emptyAnim.LoadPosition == LoadPosition.CenterRear && anim.LoadPosition == LoadPosition.CenterFront ||
                        emptyAnim.LoadPosition == LoadPosition.CenterFront && anim.LoadPosition == LoadPosition.CenterRear)
                        continue;
                    if (emptyAnim.LoadPosition == LoadPosition.CenterRear && anim.LoadPosition == LoadPosition.Rear ||
                        emptyAnim.LoadPosition == LoadPosition.Rear && anim.LoadPosition == LoadPosition.CenterRear ||
                        emptyAnim.LoadPosition == LoadPosition.CenterFront && anim.LoadPosition == LoadPosition.Front ||
                        emptyAnim.LoadPosition == LoadPosition.Front && anim.LoadPosition == LoadPosition.CenterFront
                        )
                    {
                        if (emptyAnim.LoadingAreaLength + anim.LoadingAreaLength <= LoadingAreaLength / 2 + 0.02)
                            continue;
                        else if (LoadingAreaLength / 2 - anim.LoadingAreaLength < 6.09)
                        {
                            anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                            deletableEmptyAnims.Add(emptyAnim);
                            continue;
                        }
                        // emptyAnim might be 40ft ; if complex, delete Empty animation
                        if (emptyAnim.LoadPosition == LoadPosition.Front || emptyAnim.LoadPosition == LoadPosition.Rear)
                        {
                            var multiplier = 1;
                            if (anim.LoadPosition == LoadPosition.CenterFront)
                                multiplier = -1;
                            Vector3 offset = emptyAnim.Offset;
                            offset.Z = Offset.Z + multiplier * (LoadingAreaLength / 2 - (LoadingAreaLength / 2 - anim.LoadingAreaLength) / 2);
                            emptyAnim.Offset = offset;
                            emptyAnim.LinkedIntakePoint.OffsetM = -emptyAnim.Offset.Z;
                            emptyAnim.LoadingAreaLength = LoadingAreaLength / 2 - anim.LoadingAreaLength;
                            continue;
                        }
                        else
                        {
                            var multiplier = 1;
                            if (anim.LoadPosition == LoadPosition.Front)
                                multiplier = -1;
                            Vector3 offset = emptyAnim.Offset;
                            offset.Z = Offset.Z + multiplier * (LoadingAreaLength / 2 - anim.LoadingAreaLength) / 2;
                            emptyAnim.Offset = offset;
                            emptyAnim.LinkedIntakePoint.OffsetM = -emptyAnim.Offset.Z;
                            emptyAnim.LoadingAreaLength = LoadingAreaLength / 2 - anim.LoadingAreaLength;
                            continue;
                        }
                    }
                    if (emptyAnim.LoadPosition == LoadPosition.Center && (anim.LoadPosition == LoadPosition.Rear ||
                        anim.LoadPosition == LoadPosition.Front))
                    {
                        if (emptyAnim.LoadingAreaLength / 2 + anim.LoadingAreaLength <= LoadingAreaLength / 2 + 0.02)
                            continue;
                        else if (LoadingAreaLength / 2 - anim.LoadingAreaLength < 3.045)
                        {
                            anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                            deletableEmptyAnims.Add(emptyAnim);
                            continue;
                        }
                        // add superposition case
                        anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                        deletableEmptyAnims.Add(emptyAnim);
                        continue;
                    }
                    if (anim.LoadPosition == LoadPosition.Center && (emptyAnim.LoadPosition == LoadPosition.Rear ||
                        emptyAnim.LoadPosition == LoadPosition.Front))
                    {
                        if (anim.LoadingAreaLength / 2 + emptyAnim.LoadingAreaLength <= LoadingAreaLength / 2 + 0.02)
                            continue;
                        else if (LoadingAreaLength / 2 - anim.LoadingAreaLength / 2 < 3.045)
                        {
                            anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                            deletableEmptyAnims.Add(emptyAnim);
                            continue;
                        }
                        // add superposition case
                        anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                        deletableEmptyAnims.Add(emptyAnim);
                        continue;
                    }

                    if (anim.LoadPosition == LoadPosition.Rear && emptyAnim.LoadPosition == LoadPosition.Front ||
                        anim.LoadPosition == LoadPosition.Front && emptyAnim.LoadPosition == LoadPosition.Rear)
                    {
                        if (anim.LoadingAreaLength + emptyAnim.LoadingAreaLength <= LoadingAreaLength + 0.02)
                            continue;
                        else if (LoadingAreaLength - anim.LoadingAreaLength < 5.0)
                        {
                            anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                            deletableEmptyAnims.Add(emptyAnim);
                            continue;
                        }
                        // superposition case
                        var multiplier = 1;
                        if (anim.LoadPosition == LoadPosition.Rear)
                            multiplier = -1;
                        Vector3 offset = emptyAnim.Offset;
                        offset.Z += multiplier * (LoadingAreaLength - anim.LoadingAreaLength) / 2;
                        emptyAnim.Offset = offset;
                        emptyAnim.LinkedIntakePoint.OffsetM = -emptyAnim.Offset.Z;
                        emptyAnim.LoadingAreaLength = LoadingAreaLength - anim.LoadingAreaLength;
                        continue;
                    }

                    if (anim.LoadPosition == LoadPosition.CenterRear && emptyAnim.LoadPosition == LoadPosition.Front ||
                        anim.LoadPosition == LoadPosition.CenterFront && emptyAnim.LoadPosition == LoadPosition.Rear)
                    {
                        if (emptyAnim.LoadingAreaLength <= LoadingAreaLength / 2 + 0.02)
                            continue;
                        else
                        {
                            // superposition case
                            var multiplier = 1;
                            if (anim.LoadPosition == LoadPosition.CenterRear)
                                multiplier = -1;
                            Vector3 offset = emptyAnim.Offset;
                            offset.Z = Offset.Z + multiplier * LoadingAreaLength / 4;
                            emptyAnim.Offset = offset;
                            emptyAnim.LinkedIntakePoint.OffsetM = -emptyAnim.Offset.Z;
                            emptyAnim.LoadingAreaLength = LoadingAreaLength / 2;
                            continue;
                        }
                    }
                    if (anim.LoadPosition == LoadPosition.Front && emptyAnim.LoadPosition == LoadPosition.Front ||
                        anim.LoadPosition == LoadPosition.Rear && emptyAnim.LoadPosition == LoadPosition.Rear)
                    {
                        if (emptyAnim.LoadingAreaLength <= anim.LoadingAreaLength + 0.02)
                        {
                            anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                            deletableEmptyAnims.Add(emptyAnim);
                            continue;
                        }
                        else
                        {
                            // superposition case
                            var multiplier = 1;
                            if (anim.LoadPosition == LoadPosition.Rear)
                                multiplier = -1;
                            Vector3 offset = emptyAnim.Offset;
                            offset.Z += multiplier * anim.LoadingAreaLength / 2;
                            emptyAnim.Offset = offset;
                            emptyAnim.LinkedIntakePoint.OffsetM = -emptyAnim.Offset.Z;
                            emptyAnim.LoadingAreaLength -= anim.LoadingAreaLength;
                            if (Math.Abs(emptyAnim.Offset.Z - Offset.Z) < FullSuperpositionTolerance)
                                emptyAnim.LoadPosition = LoadPosition.Center;
                            else if (Math.Abs(emptyAnim.LoadingAreaLength + anim.LoadingAreaLength - LoadingAreaLength / 2) < FullSuperpositionTolerance)
                                emptyAnim.LoadPosition = anim.LoadPosition == LoadPosition.Front ? LoadPosition.CenterFront : LoadPosition.CenterRear;
                            else if (emptyAnim.LoadingAreaLength + EmptySuperpositionTolerance > Container.Length20ftM)
                                emptyAnim.LoadPosition = anim.LoadPosition == LoadPosition.Front ? LoadPosition.CenterRear : LoadPosition.CenterFront;
                            continue;
                        }
                    }
                    if (emptyAnim.LoadPosition == LoadPosition.Center && (anim.LoadPosition == LoadPosition.CenterRear || anim.LoadPosition == LoadPosition.CenterFront))
                    {
                        // superposition case
                        var multiplier = 1;
                        if (anim.LoadPosition == LoadPosition.CenterRear)
                            multiplier = -1;
                        emptyAnim.LoadingAreaLength /= 2;
                        Vector3 offset = emptyAnim.Offset;
                        offset.Z += multiplier * emptyAnim.LoadingAreaLength / 2;
                        emptyAnim.Offset = offset;
                        emptyAnim.LinkedIntakePoint.OffsetM = -emptyAnim.Offset.Z;
                        emptyAnim.LoadPosition = anim.LoadPosition == LoadPosition.CenterRear ? LoadPosition.CenterFront : LoadPosition.CenterRear;
                        continue;
                    }
                    Trace.TraceWarning("Uncovered case by updating empty freight animations, deleting it");
                    anim.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                    deletableEmptyAnims.Add(emptyAnim);
                }
                foreach (var deletableAnim in deletableEmptyAnims)
                    EmptyAnimations.Remove(deletableAnim);
            }
            EmptyAbove();
        }

        public bool AboveAllowed()
        {
            int freightAnimDiscreteCount = 0;
            float heightBelow = 0.0f;
            ContainerType contType = ContainerType.C20ft;
            foreach (FreightAnimation animation in Animations)
            {
                if (animation is FreightAnimationDiscrete freightAnimationDiscrete)
                {
                    if (freightAnimationDiscrete.LoadPosition == LoadPosition.Above)
                        return false;
                    if (heightBelow != 0 && freightAnimationDiscrete.Container.HeightM != heightBelow)
                        return false;
                    heightBelow = freightAnimationDiscrete.Container.HeightM;
                    freightAnimDiscreteCount++;
                    contType = freightAnimationDiscrete.Container.ContainerType;
                }
            }
            if (freightAnimDiscreteCount == 0 || freightAnimDiscreteCount == 1 && contType == ContainerType.C20ft)
                return false;
            return heightBelow != 0;
        }

        public void MergeEmptyAnims()
        {
            if (EmptyAnimations.Count < 2)
                return;
            int i = 0;
            int other = (i + 1) % 2;
            if (CheckForMerge(i))
            {
                Wagon.IntakePointList.Remove(EmptyAnimations[other].LinkedIntakePoint);
                EmptyAnimations.RemoveAt(other);
                return;
            }
            else
            {
                i++;
                if (CheckForMerge(i))
                {
                    other = (i + 1) % 2;
                    Wagon.IntakePointList.Remove(EmptyAnimations[other].LinkedIntakePoint);
                    EmptyAnimations.RemoveAt(other);
                }
            }
            return;
        }

        private bool CheckForMerge(int i)
        {
            int other = (i + 1) % 2;
            Vector3 offset = EmptyAnimations[i].Offset;

            switch (EmptyAnimations[i].LoadPosition)
            {
                case LoadPosition.Front:
                    switch (EmptyAnimations[other].LoadPosition)
                    {
                        case LoadPosition.CenterFront:
                            if (Math.Abs(EmptyAnimations[i].LoadingAreaLength + EmptyAnimations[other].LoadingAreaLength - LoadingAreaLength / 2) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength = LoadingAreaLength / 2;
                                offset.Z = Offset.Z - LoadingAreaLength / 4;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                return true;
                            }
                            break;
                        case LoadPosition.CenterRear:
                            if (Math.Abs(EmptyAnimations[i].LoadingAreaLength - LoadingAreaLength / 2) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength += EmptyAnimations[other].LoadingAreaLength;
                                offset.Z = Offset.Z - LoadingAreaLength / 4 + EmptyAnimations[other].LoadingAreaLength / 2;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                return true;
                            }
                            break;
                        case LoadPosition.Rear:
                            if (Math.Abs(EmptyAnimations[i].LoadingAreaLength + EmptyAnimations[other].LoadingAreaLength - LoadingAreaLength) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength = LoadingAreaLength;
                                offset.Z = Offset.Z;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                return true;
                            }
                            break;
                        case LoadPosition.Center:
                            if (Math.Abs(EmptyAnimations[i].LoadingAreaLength + EmptyAnimations[other].LoadingAreaLength / 2 - LoadingAreaLength / 2) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength += EmptyAnimations[other].LoadingAreaLength;
                                offset.Z = Offset.Z - LoadingAreaLength / 2 + EmptyAnimations[i].LoadingAreaLength / 2;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                return true;
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case LoadPosition.CenterFront:
                    switch (EmptyAnimations[other].LoadPosition)
                    {
                        case LoadPosition.CenterRear:
                            if (Math.Abs(EmptyAnimations[i].LoadingAreaLength - EmptyAnimations[other].LoadingAreaLength) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength += EmptyAnimations[other].LoadingAreaLength;
                                offset.Z = Offset.Z;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                EmptyAnimations[i].LoadPosition = LoadPosition.Center;
                                return true;
                            }
                            break;
                        case LoadPosition.Rear:
                            if (Math.Abs(EmptyAnimations[other].LoadingAreaLength - LoadingAreaLength / 2) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength += EmptyAnimations[other].LoadingAreaLength;
                                offset.Z = Offset.Z + LoadingAreaLength / 2 - EmptyAnimations[i].LoadingAreaLength / 2;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                EmptyAnimations[i].LoadPosition = LoadPosition.Rear;
                                return true;
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case LoadPosition.Center:
                    switch (EmptyAnimations[other].LoadPosition)
                    {
                        case LoadPosition.Rear:
                            if (Math.Abs(EmptyAnimations[other].LoadingAreaLength + EmptyAnimations[i].LoadingAreaLength / 2 - LoadingAreaLength / 2) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength += EmptyAnimations[other].LoadingAreaLength;
                                offset.Z = Offset.Z + LoadingAreaLength / 2 - EmptyAnimations[i].LoadingAreaLength / 2;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                EmptyAnimations[i].LoadPosition = LoadPosition.Rear;
                                return true;
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case LoadPosition.CenterRear:
                    switch (EmptyAnimations[other].LoadPosition)
                    {
                        case LoadPosition.Rear:
                            if (Math.Abs(EmptyAnimations[other].LoadingAreaLength + EmptyAnimations[i].LoadingAreaLength - LoadingAreaLength / 2) < 0.02)
                            {
                                EmptyAnimations[i].LoadingAreaLength += EmptyAnimations[other].LoadingAreaLength;
                                offset.Z = Offset.Z + LoadingAreaLength / 4;
                                EmptyAnimations[i].Offset = offset;
                                EmptyAnimations[i].LinkedIntakePoint.OffsetM = -EmptyAnimations[i].Offset.Z;
                                EmptyAnimations[i].LoadPosition = LoadPosition.Rear;
                                return true;
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
            return false;
        }

        /// <summary>
        /// Removes loads (e.g. containers) when AI train is deleted
        /// </summary>
        public void RemoveLoads()
        {
            foreach (FreightAnimation animation in Animations)
            {
                if (animation is FreightAnimationDiscrete discreteAnimation && discreteAnimation.Container != null)
                {
                    Simulator.Instance.ContainerManager.Containers.Remove(discreteAnimation.Container);
                }
            }
        }

        /// <summary>
        /// Hides discrete freight animations and containers when remote train quits
        /// </summary>
        public void HideDiscreteFreightAnimations()
        {
            foreach (var animation in Animations)
            {
                if (animation is FreightAnimationDiscrete discreteAnimation && discreteAnimation.Container != null)
                {
                    discreteAnimation.Container.Visible = false;
                }
            }
        }

        /// <summary>
        /// Shows discrete freight animations and containers when remote train quits
        /// </summary>
        public void ShowDiscreteFreightAnimations()
        {
            foreach (var animation in Animations)
            {
                if (animation is FreightAnimationDiscrete discreteAnimation && discreteAnimation.Container != null)
                {
                    discreteAnimation.Container.Visible = true;
                }
            }
        }
    }

    /// <summary>
    /// The 3 types of freightanims are inherited from the abstract FreightAnimation class.
    /// </summary>
    public abstract class FreightAnimation
    {
        public string ShapeFileName { get; set; }
    }

    public class FreightAnimationContinuous : FreightAnimation
    {
        public bool TriggerOnStop;  // Value assumed if property not found.
        public float MaxHeight;
        public float MinHeight;
        public float FreightWeightWhenFull;
        public bool FullAtStart;
        public float LoadPerCent;
        public IntakePoint LinkedIntakePoint;

        // additions to manage consequences of variable weight on friction and brake forces
        public float FullORTSDavis_A = -9999;
        public float FullORTSDavis_B = -9999;
        public float FullORTSDavis_C = -9999;
        public float FullORTSWagonFrontalAreaM2 = -9999;
        public float FullORTSDavisDragConstant = -9999;
        public float FullMaxBrakeForceN = -9999;
        public float FullMaxHandbrakeForceN = -9999;
        public float FullCentreOfGravityM_Y = -9999; // get centre of gravity after adjusted for freight animation

        public FreightAnimationContinuous(STFReader stf, MSTSWagon wagon)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("intakepoint", ()=>
                {
                    wagon.IntakePointList.Add(new IntakePoint(stf));
                    wagon.IntakePointList.Last().LinkedFreightAnim = this;
                    LinkedIntakePoint = wagon.IntakePointList.Last();
                }),
                new STFReader.TokenProcessor("shape", ()=>{ ShapeFileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("maxheight", ()=>{ MaxHeight = stf.ReadFloatBlock(STFReader.Units.Distance, 0); }),
                new STFReader.TokenProcessor("minheight", ()=>{ MinHeight = stf.ReadFloatBlock(STFReader.Units.Distance, 0); }),
                new STFReader.TokenProcessor("freightweightwhenfull", ()=>{ FreightWeightWhenFull = stf.ReadFloatBlock(STFReader.Units.Mass, 0); }),
                new STFReader.TokenProcessor("fullatstart", ()=>{ FullAtStart = stf.ReadBoolBlock(true);}),

                // additions to manage consequences of variable weight on friction and brake forces
                new STFReader.TokenProcessor("fullortsdavis_a", ()=>{ FullORTSDavis_A = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
                new STFReader.TokenProcessor("fullortsdavis_b", ()=>{ FullORTSDavis_B = stf.ReadFloatBlock(STFReader.Units.Resistance, -1); }),
                new STFReader.TokenProcessor("fullortsdavis_c", ()=>{ FullORTSDavis_C = stf.ReadFloatBlock(STFReader.Units.ResistanceDavisC, -1); }),
                new STFReader.TokenProcessor("fullortswagonfrontalarea", ()=>{ FullORTSWagonFrontalAreaM2 = stf.ReadFloatBlock(STFReader.Units.AreaDefaultFT2, -1); }),
                new STFReader.TokenProcessor("fullortsdavisdragconstant", ()=>{ FullORTSDavisDragConstant = stf.ReadFloatBlock(STFReader.Units.Any, -1); }),
                new STFReader.TokenProcessor("fullmaxbrakeforce", ()=>{ FullMaxBrakeForceN = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
                new STFReader.TokenProcessor("fullmaxhandbrakeforce", ()=>{ FullMaxHandbrakeForceN = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
                new STFReader.TokenProcessor("fullcentreofgravity_y", ()=>{ FullCentreOfGravityM_Y = stf.ReadFloatBlock(STFReader.Units.Distance, -1); })
            });
        }

        // For copy
        public FreightAnimationContinuous(FreightAnimationContinuous freightAnimContin, MSTSWagon wagon)
        {
            if (freightAnimContin.LinkedIntakePoint != null)
            {
                wagon.IntakePointList.Add(new IntakePoint(freightAnimContin.LinkedIntakePoint));
                wagon.IntakePointList.Last().LinkedFreightAnim = this;
                LinkedIntakePoint = wagon.IntakePointList.Last();
            }
            ShapeFileName = freightAnimContin.ShapeFileName;
            MaxHeight = freightAnimContin.MaxHeight;
            MinHeight = freightAnimContin.MinHeight;
            FreightWeightWhenFull = freightAnimContin.FreightWeightWhenFull;
            FullAtStart = freightAnimContin.FullAtStart;
            LoadPerCent = freightAnimContin.LoadPerCent;

            // additions to manage consequences of variable weight on friction and brake forces
            FullORTSDavis_A = freightAnimContin.FullORTSDavis_A;
            FullORTSDavis_B = freightAnimContin.FullORTSDavis_B;
            FullORTSDavis_C = freightAnimContin.FullORTSDavis_C;
            FullORTSWagonFrontalAreaM2 = freightAnimContin.FullORTSWagonFrontalAreaM2;
            FullORTSDavisDragConstant = freightAnimContin.FullORTSDavisDragConstant;
            FullMaxBrakeForceN = freightAnimContin.FullMaxBrakeForceN;
            FullMaxHandbrakeForceN = freightAnimContin.FullMaxHandbrakeForceN;
            FullCentreOfGravityM_Y = freightAnimContin.FullCentreOfGravityM_Y;
        }
    }

    public class FreightAnimationStatic : FreightAnimation
    {
        public enum Type
        {
            DEFAULT
        }
        // index of visibility flag vector
        public enum VisibleFrom
        {
            Outside,
            Cab2D,
            Cab3D
        }
        public Type SubType;
        public float FreightWeight;
        public bool Flipped;
        public bool Cab3DFreightAnim;
        public bool[] Visibility = { true, false, false };
        public float XOffset;
        public float YOffset;
        public float ZOffset;

        // additions to manage consequences of variable weight on friction and brake forces
        public float FullStaticORTSDavis_A = -9999;
        public float FullStaticORTSDavis_B = -9999;
        public float FullStaticORTSDavis_C = -9999;
        public float FullStaticORTSWagonFrontalAreaM2 = -9999;
        public float FullStaticORTSDavisDragConstant = -9999;
        public float FullStaticMaxBrakeForceN = -9999;
        public float FullStaticMaxHandbrakeForceN = -9999;
        public float FullStaticCentreOfGravityM_Y = -9999; // get centre of gravity after adjusted for freight animation

        public FreightAnimationStatic(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
            new STFReader.TokenProcessor("subtype", ()=>
            {
                var typeString = stf.ReadStringBlock(null);
                switch (typeString)
                {
                    default:
                        SubType = FreightAnimationStatic.Type.DEFAULT;
                        break;
                }
            }),
            new STFReader.TokenProcessor("shape", ()=>{ ShapeFileName = stf.ReadStringBlock(null); }),
            new STFReader.TokenProcessor("freightweight", ()=>{ FreightWeight = stf.ReadFloatBlock(STFReader.Units.Mass, 0); }),
            new STFReader.TokenProcessor("offset", ()=>{
                stf.MustMatchBlockStart();
                XOffset = stf.ReadFloat(STFReader.Units.Distance, 0);
                YOffset = stf.ReadFloat(STFReader.Units.Distance, 0);
                ZOffset = stf.ReadFloat(STFReader.Units.Distance, 0);
                stf.MustMatchBlockEnd();
            }),
            new STFReader.TokenProcessor("flip", ()=>{ Flipped = stf.ReadBoolBlock(true);}),
            new STFReader.TokenProcessor("visibility", ()=>{
                for (int index = 0; index < 3; index++)
                    Visibility[index] = false;
                foreach (var visibilityPlace in stf.ReadStringBlock("").ToLower().Replace(" ", "").Split(','))
                {
                    switch (visibilityPlace)
                    {
                        case "outside":
                            Visibility[(int)VisibleFrom.Outside] = true;
                            break;
                        case "cab2d":
                            Visibility[(int)VisibleFrom.Cab2D] = true;
                            break;
                        case "cab3d":
                            Visibility[(int)VisibleFrom.Cab3D] = true;
                            break;
                        default:
                            break;
                    }
                }
            }),
            // additions to manage consequences of variable weight on friction and brake forces
            new STFReader.TokenProcessor("fullortsdavis_a", ()=>{ FullStaticORTSDavis_A = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
            new STFReader.TokenProcessor("fullortsdavis_b", ()=>{ FullStaticORTSDavis_B = stf.ReadFloatBlock(STFReader.Units.Resistance, -1); }),
            new STFReader.TokenProcessor("fullortsdavis_c", ()=>{ FullStaticORTSDavis_C = stf.ReadFloatBlock(STFReader.Units.ResistanceDavisC, -1); }),
            new STFReader.TokenProcessor("fullortswagonfrontalarea", ()=>{ FullStaticORTSWagonFrontalAreaM2 = stf.ReadFloatBlock(STFReader.Units.AreaDefaultFT2, -1); }),
            new STFReader.TokenProcessor("fullortsdavisdragconstant", ()=>{ FullStaticORTSDavisDragConstant = stf.ReadFloatBlock(STFReader.Units.Any, -1); }),
            new STFReader.TokenProcessor("fullmaxbrakeforce", ()=>{ FullStaticMaxBrakeForceN = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
            new STFReader.TokenProcessor("fullmaxhandbrakeforce", ()=>{ FullStaticMaxHandbrakeForceN = stf.ReadFloatBlock(STFReader.Units.Force, -1); }),
            new STFReader.TokenProcessor("fullcentreofgravity_y", ()=>{ FullStaticCentreOfGravityM_Y = stf.ReadFloatBlock(STFReader.Units.Distance, -1); })
            });
        }

        // for copy
        public FreightAnimationStatic(FreightAnimationStatic freightAnimStatic)
        {
            SubType = freightAnimStatic.SubType;
            ShapeFileName = freightAnimStatic.ShapeFileName;
            XOffset = freightAnimStatic.XOffset;
            YOffset = freightAnimStatic.YOffset;
            ZOffset = freightAnimStatic.ZOffset;
            Flipped = freightAnimStatic.Flipped;
            for (int index = 0; index < 3; index++)
                Visibility[index] = freightAnimStatic.Visibility[index];
            FreightWeight = freightAnimStatic.FreightWeight;

            // additions to manage consequences of variable weight on friction and brake forces
            FullStaticORTSDavis_A = freightAnimStatic.FullStaticORTSDavis_A;
            FullStaticORTSDavis_B = freightAnimStatic.FullStaticORTSDavis_B;
            FullStaticORTSDavis_C = freightAnimStatic.FullStaticORTSDavis_C;
            FullStaticORTSWagonFrontalAreaM2 = freightAnimStatic.FullStaticORTSWagonFrontalAreaM2;
            FullStaticORTSDavisDragConstant = freightAnimStatic.FullStaticORTSDavisDragConstant;
            FullStaticMaxBrakeForceN = freightAnimStatic.FullStaticMaxBrakeForceN;
            FullStaticMaxHandbrakeForceN = freightAnimStatic.FullStaticMaxHandbrakeForceN;
            FullStaticCentreOfGravityM_Y = freightAnimStatic.FullStaticCentreOfGravityM_Y;
        }
    }

    public class FreightAnimationDiscrete : FreightAnimation
    {
        public FreightAnimationType SubType { get; private set; }
        public bool Loaded { get; internal set; }
        public bool LoadedAtStart { get; private set; }
        public IntakePoint LinkedIntakePoint { get; private set; }
        public Vector3 Offset { get; internal set; }
        public MSTSWagon Wagon { get; private set; }
        public FreightAnimations FreightAnimations { get; private set; }
        public Container Container { get; internal set; }
        public float LoadingAreaLength { get; internal set; } = 12.19f;
        public float AboveLoadingAreaLength { get; internal set; } = -1f;
        public LoadPosition LoadPosition { get; internal set; } = LoadPosition.Center;

        // for copy
        public FreightAnimationDiscrete(FreightAnimationDiscrete source, FreightAnimations freightAnimations)
        {
            ArgumentNullException.ThrowIfNull(source);

            ContainerManager containerManager = Simulator.Instance.ContainerManager;
            FreightAnimations = freightAnimations;
            Wagon = FreightAnimations.Wagon;
            if (source.LinkedIntakePoint != null)
            {
                Wagon.IntakePointList.Add(new IntakePoint(source.LinkedIntakePoint));
                Wagon.IntakePointList[^1].LinkedFreightAnim = this;
                LinkedIntakePoint = Wagon.IntakePointList.Last();
            }
            SubType = source.SubType;
            LoadedAtStart = source.LoadedAtStart;
            LoadingAreaLength = source.LoadingAreaLength;
            AboveLoadingAreaLength = source.AboveLoadingAreaLength;
            LoadPosition = source.LoadPosition;
            Offset = source.Offset;
            if (containerManager.FreightAnimNeedsInitialization && source.Container != null)
            {
                Container = new Container(source, this);
                containerManager.Containers.Add(Container);
            }
        }

        public FreightAnimationDiscrete(FreightAnimations freightAnimations, Container container, LoadPosition loadPosition, Vector3 offset, float loadingAreaLength = 0)
        {
            ArgumentNullException.ThrowIfNull(freightAnimations);

            FreightAnimations = freightAnimations;
            Wagon = FreightAnimations.Wagon;
            Container = container;
            AboveLoadingAreaLength = freightAnimations.AboveLoadingAreaLength;
            if (container != null)
            {
                Loaded = true;
                LoadedAtStart = true;
                if (LoadPosition != LoadPosition.Above)
                    LoadingAreaLength = Container.LengthM;
            }
            else
                LoadingAreaLength = loadingAreaLength;
            Offset = offset;
            LoadPosition = loadPosition;
            IntakePoint intake = new IntakePoint(-Offset.Z, 6, PickupType.Container)
            {
                LinkedFreightAnim = this
            };
            Wagon.IntakePointList.Add(intake);
            LinkedIntakePoint = intake;
        }

        // empty FreightAnimationDiscrete covering the whole Loading area
        public FreightAnimationDiscrete(FreightAnimations freightAnimations, LoadPosition loadPosition)
        {
            ArgumentNullException.ThrowIfNull(freightAnimations);

            FreightAnimations = freightAnimations;
            Wagon = FreightAnimations.Wagon;
            Loaded = false;
            LoadedAtStart = false;
            LoadPosition = loadPosition;
            LinkedIntakePoint = new IntakePoint(freightAnimations.GeneralIntakePoint);
            LinkedIntakePoint.LinkedFreightAnim = this;
            Wagon.IntakePointList.Add(LinkedIntakePoint);
            LoadingAreaLength = freightAnimations.LoadingAreaLength;
            AboveLoadingAreaLength = freightAnimations.AboveLoadingAreaLength;
            Offset = freightAnimations.Offset;
        }

        public FreightAnimationDiscrete(BinaryReader inf, FreightAnimations freightAnimations)
        {
            ArgumentNullException.ThrowIfNull(freightAnimations);

            FreightAnimations = freightAnimations;
            Wagon = freightAnimations.Wagon;
            LinkedIntakePoint = new IntakePoint(inf.ReadSingle(), inf.ReadSingle(), (PickupType)inf.ReadInt32())
            {
                LinkedFreightAnim = this
            };
            Offset = new Vector3(inf.ReadSingle(), inf.ReadSingle(), inf.ReadSingle());
            LoadingAreaLength = inf.ReadSingle();
            AboveLoadingAreaLength = inf.ReadSingle();
            LoadPosition = (LoadPosition)inf.ReadInt32();
            Loaded = inf.ReadBoolean();
            IntakePoint intake = new IntakePoint(inf.ReadSingle(), inf.ReadSingle(), (PickupType)inf.ReadInt32())
            {
                LinkedFreightAnim = this
            };
            Wagon.IntakePointList.Add(intake);
            LinkedIntakePoint = intake;
            bool containerPresent = inf.ReadBoolean();
            if (containerPresent)
            {
                Container = new Container(inf, this, null, false);
                Simulator.Instance.ContainerManager.Containers.Add(Container);
            }
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write(LinkedIntakePoint.OffsetM);
            outf.Write(LinkedIntakePoint.WidthM);
            outf.Write((int)LinkedIntakePoint.Type);
            outf.Write(Offset.X);
            outf.Write(Offset.Y);
            outf.Write(Offset.Z);
            outf.Write(LoadingAreaLength);
            outf.Write(AboveLoadingAreaLength);
            outf.Write((int)LoadPosition);
            outf.Write(Loaded);
            outf.Write(LinkedIntakePoint.OffsetM);
            outf.Write(LinkedIntakePoint.WidthM);
            outf.Write((int)LinkedIntakePoint.Type);
            if (Container != null)
            {
                outf.Write(true);
                Container.Save(outf);
            }
            else
                outf.Write(false);
        }
    }
}
