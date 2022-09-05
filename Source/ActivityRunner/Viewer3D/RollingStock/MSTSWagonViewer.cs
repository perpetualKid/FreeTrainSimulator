// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

// Debug for Sound Variables
//#define DEBUG_WHEEL_ANIMATION 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    public class MSTSWagonViewer : TrainCarViewer
    {
        protected PoseableShape TrainCarShape;
        protected AnimatedShape FreightShape;
        protected AnimatedShape InteriorShape;
        protected AnimatedShape FrontCouplerShape;
        protected AnimatedShape FrontCouplerOpenShape;
        protected AnimatedShape RearCouplerShape;
        protected AnimatedShape RearCouplerOpenShape;

        protected AnimatedShape FrontAirHoseShape;
        protected AnimatedShape FrontAirHoseDisconnectedShape;
        protected AnimatedShape RearAirHoseShape;
        protected AnimatedShape RearAirHoseDisconnectedShape;

        // Wheels are rotated by hand instead of in the shape file.
        private float WheelRotationR;
        private readonly List<int> wheelPartIndexes = new List<int>();

        // Everything else is animated through the shape file.
        private AnimatedPart RunningGear;
        private AnimatedPart Pantograph1;
        private AnimatedPart Pantograph2;
        private AnimatedPart Pantograph3;
        private AnimatedPart Pantograph4;
        private AnimatedPart LeftDoor;
        private AnimatedPart RightDoor;
        private AnimatedPart Mirrors;
        protected AnimatedPart Wipers;
        protected AnimatedPart Bell;
        protected AnimatedPart Item1Continuous;
        protected AnimatedPart Item2Continuous;
        private AnimatedPart Item1TwoState;
        private AnimatedPart Item2TwoState;
        private AnimatedPart UnloadingParts;

        public Dictionary<string, List<ParticleEmitterViewer>> ParticleDrawers = new Dictionary<string, List<ParticleEmitterViewer>>();

        protected MSTSWagon MSTSWagon { get { return (MSTSWagon)Car; } }


        // Create viewers for special steam/smoke effects on car
        private List<ParticleEmitterViewer> HeatingHose = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> HeatingCompartmentSteamTrap = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> HeatingMainPipeSteamTrap = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> WaterScoop = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> WaterScoopReverse = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> TenderWaterOverflow = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> WagonSmoke = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> HeatingSteamBoiler = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> BearingHotBox = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> SteamBrake = new List<ParticleEmitterViewer>();

        // Create viewers for special steam effects on car
        private List<ParticleEmitterViewer> WagonGenerator = new List<ParticleEmitterViewer>();
        private bool HasFirstPanto;
        private int numBogie1, numBogie2, bogie1Axles, bogie2Axles;
        private int bogieMatrix1, bogieMatrix2;
        private FreightAnimationsViewer FreightAnimations;

        public MSTSWagonViewer(Viewer viewer, MSTSWagon car)
            : base(viewer, car)
        {

            string steamTexture = viewer.Simulator.RouteFolder.ContentFolder.TextureFile("smokemain.ace");
            string dieselTexture = viewer.Simulator.RouteFolder.ContentFolder.TextureFile("dieselsmoke.ace");

            // Particle Drawers called in Wagon so that wagons can also have steam effects.
            ParticleDrawers = (
                from effect in MSTSWagon.EffectData
                select new KeyValuePair<string, List<ParticleEmitterViewer>>(effect.Key, new List<ParticleEmitterViewer>(
                    from data in effect.Value
                    select new ParticleEmitterViewer(viewer, data, car)))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Initaialise particle viewers for special steam effects
            foreach (KeyValuePair<string, List<ParticleEmitterViewer>> emitter in ParticleDrawers)
            {

                // Exhaust for steam heating boiler
                if (emitter.Key.Equals("heatingsteamboilerfx", StringComparison.OrdinalIgnoreCase))
                {
                    HeatingSteamBoiler.AddRange(emitter.Value);
                    car.InitializeBoilerHeating();
                }

                foreach (var drawer in HeatingSteamBoiler)
                {
                    drawer.Initialize(dieselTexture);
                }

                // Exhaust for HEP/Power Generator
                if (emitter.Key.Equals("wagongeneratorfx", StringComparison.OrdinalIgnoreCase))
                    WagonGenerator.AddRange(emitter.Value);

                foreach (var drawer in WagonGenerator)
                {
                    drawer.Initialize(dieselTexture);
                }

                // Smoke for wood/coal fire
                if (emitter.Key.Equals("wagonsmokefx", StringComparison.OrdinalIgnoreCase))
                    WagonSmoke.AddRange(emitter.Value);

                foreach (var drawer in WagonSmoke)
                {
                    drawer.Initialize(steamTexture);
                }

                // Smoke for bearing hot box
                if (emitter.Key.Equals("bearinghotboxfx", StringComparison.OrdinalIgnoreCase))
                    BearingHotBox.AddRange(emitter.Value);

                foreach (var drawer in BearingHotBox)
                {
                    drawer.Initialize(steamTexture);
                }

                // Steam leak in heating hose 

                if (emitter.Key.Equals("heatinghosefx", StringComparison.OrdinalIgnoreCase))
                    HeatingHose.AddRange(emitter.Value);

                foreach (var drawer in HeatingHose)
                {
                    drawer.Initialize(steamTexture);
                }

                // Steam leak in heating compartment steam trap

                if (emitter.Key.Equals("heatingcompartmentsteamtrapfx", StringComparison.OrdinalIgnoreCase))
                    HeatingCompartmentSteamTrap.AddRange(emitter.Value);

                foreach (var drawer in HeatingCompartmentSteamTrap)
                {
                    drawer.Initialize(steamTexture);
                }

                // Steam leak in heating steam trap

                if (emitter.Key.Equals("heatingmainpipesteamtrapfx", StringComparison.OrdinalIgnoreCase))
                    HeatingMainPipeSteamTrap.AddRange(emitter.Value);

                foreach (var drawer in HeatingMainPipeSteamTrap)
                {
                    drawer.Initialize(steamTexture);
                }

                // Water spray for when water scoop is in use (use steam effects for the time being)
                // Forward motion
                if (emitter.Key.Equals("waterscoopfx", StringComparison.OrdinalIgnoreCase))
                    WaterScoop.AddRange(emitter.Value);

                foreach (var drawer in WaterScoop)
                {
                    drawer.Initialize(steamTexture);
                }

                // Reverse motion

                if (emitter.Key.Equals("waterscoopreversefx", StringComparison.OrdinalIgnoreCase))
                    WaterScoopReverse.AddRange(emitter.Value);

                foreach (var drawer in WaterScoopReverse)
                {
                    drawer.Initialize(steamTexture);
                }

                // Water overflow when tender is over full during water trough filling (use steam effects for the time being) 

                if (emitter.Key.Equals("tenderwateroverflowfx", StringComparison.OrdinalIgnoreCase))
                    TenderWaterOverflow.AddRange(emitter.Value);

                foreach (var drawer in TenderWaterOverflow)
                {
                    drawer.Initialize(steamTexture);
                }

                if (emitter.Key.Equals("steambrakefx", StringComparison.OrdinalIgnoreCase))
                    SteamBrake.AddRange(emitter.Value);

                foreach (var drawer in SteamBrake)
                {
                    drawer.Initialize(steamTexture);
                }

            }

            var wagonFolderSlash = Path.GetDirectoryName(car.WagFilePath) + @"\";

            TrainCarShape = !string.IsNullOrEmpty(car.MainShapeFileName)
                ? new PoseableShape(wagonFolderSlash + car.MainShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster)
                : new PoseableShape(null, car);

            // This insection initialises the MSTS style freight animation - can either be for a coal load, which will adjust with usage, or a static animation, such as additional shape.
            if (car.FreightShapeFileName != null)
            {
                FreightShape = new AnimatedShape(wagonFolderSlash + car.FreightShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);

                // Reproducing MSTS "bug" of not allowing tender animation in case both minLevel and maxLevel are 0 or maxLevel <  minLevel 
                // Applies to both a standard tender locomotive or a tank locomotive (where coal load is on same "wagon" as the locomotive -  for the coal load on a tender or tank locomotive - in operation it will raise or lower with caol usage

                if (MSTSWagon.WagonType == WagonType.Tender || MSTSWagon is MSTSSteamLocomotive)
                {

                    var NonTenderSteamLocomotive = MSTSWagon as MSTSSteamLocomotive;

                    if ((MSTSWagon.WagonType == WagonType.Tender || MSTSWagon is MSTSLocomotive && (MSTSWagon.EngineType == EngineType.Steam && NonTenderSteamLocomotive.IsTenderRequired == 0.0)) && MSTSWagon.FreightAnimMaxLevelM != 0 && MSTSWagon.FreightAnimFlag > 0 && MSTSWagon.FreightAnimMaxLevelM > MSTSWagon.FreightAnimMinLevelM)
                    {
                        // Force allowing animation:
                        if (FreightShape.SharedShape.LodControls.Length > 0 && FreightShape.SharedShape.LodControls[0].DistanceLevels.Length > 0 && FreightShape.SharedShape.LodControls[0].DistanceLevels[0].SubObjects.Length > 0 && FreightShape.SharedShape.LodControls[0].DistanceLevels[0].SubObjects[0].ShapePrimitives.Length > 0 && FreightShape.SharedShape.LodControls[0].DistanceLevels[0].SubObjects[0].ShapePrimitives[0].Hierarchy.Length > 0)
                            FreightShape.SharedShape.LodControls[0].DistanceLevels[0].SubObjects[0].ShapePrimitives[0].Hierarchy[0] = 1;
                    }
                }
            }

            // Initialise Coupler shapes 
            if (car.FrontCouplerAnimation != null)
            {
                FrontCouplerShape = new AnimatedShape(wagonFolderSlash + car.FrontCouplerAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            if (car.FrontCouplerOpenAnimation != null)
            {
                FrontCouplerOpenShape = new AnimatedShape(wagonFolderSlash + car.FrontCouplerOpenAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            if (car.RearCouplerAnimation != null)
            {
                RearCouplerShape = new AnimatedShape(wagonFolderSlash + car.RearCouplerAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            if (car.RearCouplerOpenAnimation != null)
            {
                RearCouplerOpenShape = new AnimatedShape(wagonFolderSlash + car.RearCouplerOpenAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            // Initialise air hose shapes

            if (car.FrontAirHoseAnimation != null)
            {
                FrontAirHoseShape = new AnimatedShape(wagonFolderSlash + car.FrontAirHoseAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            if (car.FrontAirHoseDisconnectedAnimation != null)
            {
                FrontAirHoseDisconnectedShape = new AnimatedShape(wagonFolderSlash + car.FrontAirHoseDisconnectedAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            if (car.RearAirHoseAnimation != null)
            {
                RearAirHoseShape = new AnimatedShape(wagonFolderSlash + car.RearAirHoseAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }

            if (car.RearAirHoseDisconnectedAnimation != null)
            {
                RearAirHoseDisconnectedShape = new AnimatedShape(wagonFolderSlash + car.RearAirHoseDisconnectedAnimation.ShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.ShadowCaster);
            }


            if (car.InteriorShapeFileName != null)
                InteriorShape = new AnimatedShape(wagonFolderSlash + car.InteriorShapeFileName + '\0' + wagonFolderSlash, car, ShapeFlags.Interior, 30.0f);

            RunningGear = new AnimatedPart(TrainCarShape);
            Pantograph1 = new AnimatedPart(TrainCarShape);
            Pantograph2 = new AnimatedPart(TrainCarShape);
            Pantograph3 = new AnimatedPart(TrainCarShape);
            Pantograph4 = new AnimatedPart(TrainCarShape);
            LeftDoor = new AnimatedPart(TrainCarShape);
            RightDoor = new AnimatedPart(TrainCarShape);
            Mirrors = new AnimatedPart(TrainCarShape);
            Wipers = new AnimatedPart(TrainCarShape);
            UnloadingParts = new AnimatedPart(TrainCarShape);
            Bell = new AnimatedPart(TrainCarShape);
            Item1Continuous = new AnimatedPart(TrainCarShape);
            Item2Continuous = new AnimatedPart(TrainCarShape);
            Item1TwoState = new AnimatedPart(TrainCarShape);
            Item2TwoState = new AnimatedPart(TrainCarShape);

            if (car.FreightAnimations != null)
                FreightAnimations = new FreightAnimationsViewer(viewer, car, wagonFolderSlash);

            LoadCarSounds(wagonFolderSlash);
            //if (!(MSTSWagon is MSTSLocomotive))
            //    LoadTrackSounds();
            Viewer.SoundProcess.AddSoundSource(this, new TrackSoundSource(this));

            // Determine if it has first pantograph. So we can match unnamed panto parts correctly
            for (var i = 0; i < TrainCarShape.Hierarchy.Length; i++)
                if (TrainCarShape.SharedShape.MatrixNames[i].Contains('1', StringComparison.OrdinalIgnoreCase))
                {
                    if (TrainCarShape.SharedShape.MatrixNames[i].StartsWith("PANTO", StringComparison.OrdinalIgnoreCase)) { HasFirstPanto = true; break; }
                }

            // Check bogies and wheels to find out what we have.
            for (var i = 0; i < TrainCarShape.Hierarchy.Length; i++)
            {
                if (TrainCarShape.SharedShape.MatrixNames[i].Equals("BOGIE1", StringComparison.OrdinalIgnoreCase))
                {
                    bogieMatrix1 = i;
                    numBogie1 += 1;
                }
                if (TrainCarShape.SharedShape.MatrixNames[i].Equals("BOGIE2", StringComparison.OrdinalIgnoreCase))
                {
                    bogieMatrix2 = i;
                    numBogie2 += 1;
                }
                if (TrainCarShape.SharedShape.MatrixNames[i].Equals("BOGIE", StringComparison.OrdinalIgnoreCase))
                {
                    bogieMatrix1 = i;
                }
                // For now, the total axle count consisting of axles that are part of the bogie are being counted.
                if (TrainCarShape.SharedShape.MatrixNames[i].Contains("WHEELS", StringComparison.OrdinalIgnoreCase))
                    if (TrainCarShape.SharedShape.MatrixNames[i].Length == 8)
                    {
                        var tpmatrix = TrainCarShape.SharedShape.GetParentMatrix(i);
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS11", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix1)
                            bogie1Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS12", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix1)
                            bogie1Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS13", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix1)
                            bogie1Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS21", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix1)
                            bogie1Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS22", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix1)
                            bogie1Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS23", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix1)
                            bogie1Axles += 1;

                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS11", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix2)
                            bogie2Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS12", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix2)
                            bogie2Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS13", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix2)
                            bogie2Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS21", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix2)
                            bogie2Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS21", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix2)
                            bogie2Axles += 1;
                        if (TrainCarShape.SharedShape.MatrixNames[i].Equals("WHEELS23", StringComparison.OrdinalIgnoreCase) && tpmatrix == bogieMatrix2)
                            bogie2Axles += 1;
                    }
            }

            // Match up all the matrices with their parts.
            for (var i = 0; i < TrainCarShape.Hierarchy.Length; i++)
                if (TrainCarShape.Hierarchy[i] == -1)
                    MatchMatrixToPart(car, i, 0);

            car.SetUpWheels();

            // If we have two pantographs, 2 is the forwards pantograph, unlike when there's only one.
            if (!(car.Flipped ^ (car.Train.IsActualPlayerTrain && Viewer.PlayerLocomotive.Flipped)) && !Pantograph1.Empty() && !Pantograph2.Empty())
                AnimatedPart.Swap(ref Pantograph1, ref Pantograph2);

            Pantograph1.SetState(MSTSWagon.Pantographs[1].CommandUp);
            Pantograph2.SetState(MSTSWagon.Pantographs[2].CommandUp);
            if (MSTSWagon.Pantographs.List.Count > 2) Pantograph3.SetState(MSTSWagon.Pantographs[3].CommandUp);
            if (MSTSWagon.Pantographs.List.Count > 3) Pantograph4.SetState(MSTSWagon.Pantographs[4].CommandUp);
            LeftDoor.SetState(MSTSWagon.LeftDoor.State == DoorState.Open || MSTSWagon.LeftDoor.State == DoorState.Opening);
            RightDoor.SetState(MSTSWagon.RightDoor.State == DoorState.Open || MSTSWagon.RightDoor.State == DoorState.Opening);
            Mirrors.SetState(MSTSWagon.MirrorOpen);
            Item1TwoState.SetState(MSTSWagon.GenericItem1);
            Item2TwoState.SetState(MSTSWagon.GenericItem2);
            UnloadingParts.SetState(MSTSWagon.UnloadingPartsOpen);
        }

        private void MatchMatrixToPart(MSTSWagon car, int matrix, int bogieMatrix)
        {
            string matrixName = TrainCarShape.SharedShape.MatrixNames[matrix];
            // Gate all RunningGearPartIndexes on this!
            var matrixAnimated = TrainCarShape.SharedShape.Animations != null && TrainCarShape.SharedShape.Animations.Count > 0 && TrainCarShape.SharedShape.Animations[0].AnimationNodes.Count > matrix && TrainCarShape.SharedShape.Animations[0].AnimationNodes[matrix].Controllers.Count > 0;
            if (matrixName.StartsWith("Wheels", StringComparison.OrdinalIgnoreCase) && (matrixName.Length == 7 || matrixName.Length == 8 || matrixName.Length == 9))
            {
                // Standard WHEELS length would be 8 to test for WHEELS11. Came across WHEELS tag that used a period(.) between the last 2 numbers, changing max length to 9.
                // Changing max length to 9 is not a problem since the initial WHEELS test will still be good.
                Matrix m = TrainCarShape.SharedShape.GetMatrixProduct(matrix);
                //someone uses wheel to animate fans, thus check if the wheel is not too high (lower than 3m), will animate it as real wheel
                if (m.M42 < 3)
                {
                    int id = 0;
                    // Model makers are not following the standard rules, For example, one tender uses naming convention of wheels11/12 instead of using Wheels1,2,3 when not part of a bogie.
                    // The next 2 lines will sort out these axles.
                    int tmatrix = TrainCarShape.SharedShape.GetParentMatrix(matrix);
                    if (matrixName.Length == 8 && bogieMatrix == 0 && tmatrix == 0) // In this test, both tmatrix and bogieMatrix are 0 since these wheels are not part of a bogie.
                        matrixName = TrainCarShape.SharedShape.MatrixNames[matrix].Substring(0, 7); // Changing wheel name so that it reflects its actual use since it is not p
                    if (matrixName.Length == 8 || matrixName.Length == 9)
                        _ = int.TryParse(matrixName.AsSpan(6, 1), out id);
                    if (matrixName.Length == 8 || matrixName.Length == 9 || !matrixAnimated)
                        wheelPartIndexes.Add(matrix);
                    else
                        RunningGear.AddMatrix(matrix);
                    int pmatrix = TrainCarShape.SharedShape.GetParentMatrix(matrix);
                    car.AddWheelSet(m.M43, id, pmatrix, matrixName.ToString(), bogie1Axles, bogie2Axles);
                }
                // Standard wheels are processed above, but wheels used as animated fans that are greater than 3m are processed here.
                else
                    RunningGear.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("Bogie", StringComparison.OrdinalIgnoreCase) && matrixName.Length <= 6) //BOGIE1 is valid, BOGIE11 is not, it is used by some modelers to indicate this is part of bogie1
            {
                if (matrixName.Length == 6)
                {
                    _ = int.TryParse(matrixName.AsSpan(5), out int id);
                    Matrix m = TrainCarShape.SharedShape.GetMatrixProduct(matrix);
                    car.AddBogie(m.M43, matrix, id, matrixName.ToString(), numBogie1, numBogie2);
                    bogieMatrix = matrix; // Bogie matrix needs to be saved for test with axles.
                }
                else
                {
                    // Since the string content is BOGIE, Int32.TryParse(matrixName.Substring(5), out id) is not needed since its sole purpose is to
                    //  parse the string number from the string.
                    int id = 1;
                    Matrix m = TrainCarShape.SharedShape.GetMatrixProduct(matrix);
                    car.AddBogie(m.M43, matrix, id, matrixName.ToString(), numBogie1, numBogie2);
                    bogieMatrix = matrix; // Bogie matrix needs to be saved for test with axles.
                }
                // Bogies contain wheels!
                for (var i = 0; i < TrainCarShape.Hierarchy.Length; i++)
                    if (TrainCarShape.Hierarchy[i] == matrix)
                        MatchMatrixToPart(car, i, bogieMatrix);
            }
            else if (matrixName.StartsWith("Wiper", StringComparison.OrdinalIgnoreCase)) // wipers
            {
                Wipers.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("Door", StringComparison.OrdinalIgnoreCase)) // doors (left / right)
            {
                if (matrixName.StartsWith("Door_D", StringComparison.OrdinalIgnoreCase) || 
                    matrixName.StartsWith("Door_E", StringComparison.OrdinalIgnoreCase) || 
                    matrixName.StartsWith("Door_F", StringComparison.OrdinalIgnoreCase))
                    LeftDoor.AddMatrix(matrix);
                else if (matrixName.StartsWith("Door_A", StringComparison.OrdinalIgnoreCase) || 
                    matrixName.StartsWith("Door_B", StringComparison.OrdinalIgnoreCase) || 
                    matrixName.StartsWith("Door_C", StringComparison.OrdinalIgnoreCase))
                    RightDoor.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("Pantograph", StringComparison.OrdinalIgnoreCase)) //pantographs (1/2)
            {
                matrixName = matrixName.ToUpperInvariant();
                switch (matrixName)
                {
                    case "PANTOGRAPHBOTTOM1":
                    case "PANTOGRAPHBOTTOM1A":
                    case "PANTOGRAPHBOTTOM1B":
                    case "PANTOGRAPHMIDDLE1":
                    case "PANTOGRAPHMIDDLE1A":
                    case "PANTOGRAPHMIDDLE1B":
                    case "PANTOGRAPHTOP1":
                    case "PANTOGRAPHTOP1A":
                    case "PANTOGRAPHTOP1B":
                        Pantograph1.AddMatrix(matrix);
                        break;
                    case "PANTOGRAPHBOTTOM2":
                    case "PANTOGRAPHBOTTOM2A":
                    case "PANTOGRAPHBOTTOM2B":
                    case "PANTOGRAPHMIDDLE2":
                    case "PANTOGRAPHMIDDLE2A":
                    case "PANTOGRAPHMIDDLE2B":
                    case "PANTOGRAPHTOP2":
                    case "PANTOGRAPHTOP2A":
                    case "PANTOGRAPHTOP2B":
                        Pantograph2.AddMatrix(matrix);
                        break;
                    default://someone used other language
                        if (matrixName.Contains('1', StringComparison.OrdinalIgnoreCase))
                            Pantograph1.AddMatrix(matrix);
                        else if (matrixName.Contains('2', StringComparison.OrdinalIgnoreCase))
                            Pantograph2.AddMatrix(matrix);
                        else if (matrixName.Contains('3', StringComparison.OrdinalIgnoreCase))
                            Pantograph3.AddMatrix(matrix);
                        else if (matrixName.Contains('4', StringComparison.OrdinalIgnoreCase))
                            Pantograph4.AddMatrix(matrix);
                        else
                        {
                            if (HasFirstPanto) Pantograph1.AddMatrix(matrix); //some may have no first panto, will put it as panto 2
                            else Pantograph2.AddMatrix(matrix);
                        }
                        break;
                }
            }
            else if (matrixName.StartsWith("MIRROR", StringComparison.OrdinalIgnoreCase)) // mirrors
            {
                Mirrors.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("UNLOADINGPARTS", StringComparison.OrdinalIgnoreCase)) // unloading parts
            {
                UnloadingParts.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("PANTO", StringComparison.OrdinalIgnoreCase))  // TODO, not sure why this is needed, see above!
            {
                Trace.TraceInformation("Pantograph matrix with unusual name {1} in shape {0}", TrainCarShape.SharedShape.FilePath, matrixName);
                if (matrixName.Contains('1', StringComparison.OrdinalIgnoreCase))
                    Pantograph1.AddMatrix(matrix);
                else if (matrixName.Contains('2', StringComparison.OrdinalIgnoreCase))
                    Pantograph2.AddMatrix(matrix);
                else if (matrixName.Contains('3', StringComparison.OrdinalIgnoreCase))
                    Pantograph3.AddMatrix(matrix);
                else if (matrixName.Contains('4', StringComparison.OrdinalIgnoreCase))
                    Pantograph4.AddMatrix(matrix);
                else
                {
                    if (HasFirstPanto) Pantograph1.AddMatrix(matrix); //some may have no first panto, will put it as panto 2
                    else Pantograph2.AddMatrix(matrix);
                }
            }
            else if (matrixName.StartsWith("ORTSBELL", StringComparison.OrdinalIgnoreCase)) // bell
            {
                Bell.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("ORTSITEM1CONTINUOUS", StringComparison.OrdinalIgnoreCase)) // generic item 1, continuous animation
            {
                Item1Continuous.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("ORTSITEM2CONTINUOUS", StringComparison.OrdinalIgnoreCase)) // generic item 2, continuous animation
            {
                Item2Continuous.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("ORTSITEM1TWOSTATE", StringComparison.OrdinalIgnoreCase)) // generic item 1, continuous animation
            {
                Item1TwoState.AddMatrix(matrix);
            }
            else if (matrixName.StartsWith("ORTSITEM2TWOSTATE", StringComparison.OrdinalIgnoreCase)) // generic item 2, continuous animation
            {
                Item2TwoState.AddMatrix(matrix);
            }
            else
            {
                if (matrixAnimated && matrix != 0)
                    RunningGear.AddMatrix(matrix);

                for (var i = 0; i < TrainCarShape.Hierarchy.Length; i++)
                    if (TrainCarShape.Hierarchy[i] == matrix)
                        MatchMatrixToPart(car, i, 0);
            }
        }

        public override void HandleUserInput(in ElapsedTime elapsedTime)
        {
        }

        public override void RegisterUserCommandHandling()
        {
            if (MSTSWagon.Pantographs.List.Count > 0)
                Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph1, KeyEventType.KeyPressed, Pantograph1Command, true);
            if (MSTSWagon.Pantographs.List.Count > 1)
                Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph2, KeyEventType.KeyPressed, Pantograph2Command, true);
            if (MSTSWagon.Pantographs.List.Count > 2)
                Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph3, KeyEventType.KeyPressed, Pantograph3Command, true);
            if (MSTSWagon.Pantographs.List.Count > 3)
                Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph4, KeyEventType.KeyPressed, Pantograph4Command, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDoorLeft, KeyEventType.KeyPressed, ToggleDoorsLeftCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDoorRight, KeyEventType.KeyPressed, ToggleDoorsRightCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlMirror, KeyEventType.KeyPressed, ToggleMirrorsCommand, true);
        }

        public override void UnregisterUserCommandHandling()
        {
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph1, KeyEventType.KeyPressed, Pantograph1Command);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph2, KeyEventType.KeyPressed, Pantograph2Command);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph3, KeyEventType.KeyPressed, Pantograph1Command);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph4, KeyEventType.KeyPressed, Pantograph2Command);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDoorLeft, KeyEventType.KeyPressed, ToggleDoorsLeftCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDoorRight, KeyEventType.KeyPressed, ToggleDoorsRightCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlMirror, KeyEventType.KeyPressed, ToggleMirrorsCommand);
        }

#pragma warning disable IDE0022 // Use block body for methods
        private void Pantograph1Command() => _ = new PantographCommand(Viewer.Log, 1, !MSTSWagon.Pantographs[1].CommandUp);
        private void Pantograph2Command() => _ = new PantographCommand(Viewer.Log, 2, !MSTSWagon.Pantographs[2].CommandUp);
        private void Pantograph3Command() => _ = new PantographCommand(Viewer.Log, 3, !MSTSWagon.Pantographs[3].CommandUp);
        private void Pantograph4Command() => _ = new PantographCommand(Viewer.Log, 4, !MSTSWagon.Pantographs[4].CommandUp);
        private void ToggleDoorsLeftCommand() => _ = new ToggleDoorsLeftCommand(Viewer.Log);
        private void ToggleDoorsRightCommand() => _ = new ToggleDoorsRightCommand(Viewer.Log);
        private void ToggleMirrorsCommand() => _ = new ToggleMirrorsCommand(Viewer.Log);
#pragma warning restore IDE0022 // Use block body for methods

        /// <summary>
        /// Called at the full frame rate
        /// elapsedTime is time since last frame
        /// Executes in the UpdaterThread
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            Pantograph1.UpdateState(MSTSWagon.Pantographs[1].CommandUp, elapsedTime);
            Pantograph2.UpdateState(MSTSWagon.Pantographs[2].CommandUp, elapsedTime);
            if (MSTSWagon.Pantographs.List.Count > 2) Pantograph3.UpdateState(MSTSWagon.Pantographs[3].CommandUp, elapsedTime);
            if (MSTSWagon.Pantographs.List.Count > 3) Pantograph4.UpdateState(MSTSWagon.Pantographs[4].CommandUp, elapsedTime);
            LeftDoor.UpdateState(MSTSWagon.LeftDoor.State == DoorState.Open || MSTSWagon.LeftDoor.State == DoorState.Opening, elapsedTime);
            RightDoor.UpdateState(MSTSWagon.RightDoor.State == DoorState.Open || MSTSWagon.RightDoor.State == DoorState.Opening, elapsedTime);
            Mirrors.UpdateState(MSTSWagon.MirrorOpen, elapsedTime);
            UnloadingParts.UpdateState(MSTSWagon.UnloadingPartsOpen, elapsedTime);
            Item1TwoState.UpdateState(MSTSWagon.GenericItem1, elapsedTime);
            Item2TwoState.UpdateState(MSTSWagon.GenericItem2, elapsedTime);
            UpdateAnimation(frame, elapsedTime);

            var car = Car as MSTSWagon;
            // Steam leak in heating hose
            foreach (var drawer in HeatingHose)
            {
                drawer.SetOutput(car.HeatingHoseSteamVelocityMpS, car.HeatingHoseSteamVolumeM3pS, car.HeatingHoseParticleDurationS);
            }

            // Steam leak in heating compartment steamtrap
            foreach (var drawer in HeatingCompartmentSteamTrap)
            {
                drawer.SetOutput(car.HeatingCompartmentSteamTrapVelocityMpS, car.HeatingCompartmentSteamTrapVolumeM3pS, car.HeatingCompartmentSteamTrapParticleDurationS);
            }

            // Steam leak in heating main pipe steamtrap
            foreach (var drawer in HeatingMainPipeSteamTrap)
            {
                drawer.SetOutput(car.HeatingMainPipeSteamTrapVelocityMpS, car.HeatingMainPipeSteamTrapVolumeM3pS, car.HeatingMainPipeSteamTrapDurationS);
            }

            // Heating Steam Boiler Exhaust
            foreach (var drawer in HeatingSteamBoiler)
            {
                drawer.SetOutput(car.HeatingSteamBoilerVolumeM3pS, car.HeatingSteamBoilerDurationS, car.HeatingSteamBoilerSteadyColor);
            }

            // Exhaust for HEP/Electrical Generator
            foreach (var drawer in WagonGenerator)
            {
                drawer.SetOutput(car.WagonGeneratorVolumeM3pS, car.WagonGeneratorDurationS, car.WagonGeneratorSteadyColor);
            }

            // Wagon fire smoke
            foreach (var drawer in WagonSmoke)
            {
                drawer.SetOutput(car.WagonSmokeVelocityMpS, car.WagonSmokeVolumeM3pS, car.WagonSmokeDurationS, car.WagonSmokeSteadyColor);
            }

            if (car.Train != null) // only process this visual feature if this is a valid car in the train
            {
                // Water spray for water scoop (uses steam effects currently) - Forward direction
                if (car.Direction == MidpointDirection.Forward)
                {
                    foreach (var drawer in WaterScoop)
                    {
                        drawer.SetOutput(car.WaterScoopWaterVelocityMpS, car.WaterScoopWaterVolumeM3pS, car.WaterScoopParticleDurationS);
                    }
                }
                // If travelling in reverse turn on rearward facing effect
                else if (car.Direction == MidpointDirection.Reverse)
                {
                    foreach (var drawer in WaterScoopReverse)
                    {
                        drawer.SetOutput(car.WaterScoopWaterVelocityMpS, car.WaterScoopWaterVolumeM3pS, car.WaterScoopParticleDurationS);
                    }
                }
            }

            // Water overflow from tender (uses steam effects currently)
            foreach (var drawer in TenderWaterOverflow)
            {
                drawer.SetOutput(car.TenderWaterOverflowVelocityMpS, car.TenderWaterOverflowVolumeM3pS, car.TenderWaterOverflowParticleDurationS);
            }

            // Bearing Hot box smoke
            foreach (var drawer in BearingHotBox)
            {
                drawer.SetOutput(car.BearingHotBoxSmokeVelocityMpS, car.BearingHotBoxSmokeVolumeM3pS, car.BearingHotBoxSmokeDurationS, car.BearingHotBoxSmokeSteadyColor);
            }

            // Steam Brake effects
            foreach (var drawer in SteamBrake)
            {
                drawer.SetOutput(car.SteamBrakeLeaksVelocityMpS, car.SteamBrakeLeaksVolumeM3pS, car.SteamBrakeLeaksDurationS);
            }

            foreach (List<ParticleEmitterViewer> drawers in ParticleDrawers.Values)
                foreach (ParticleEmitterViewer drawer in drawers)
                    drawer.PrepareFrame(frame, elapsedTime);

        }


        private void UpdateAnimation(RenderFrame frame, in ElapsedTime elapsedTime)
        {

            float distanceTravelledM = 0.0f; // Distance travelled by non-driven wheels
            float distanceTravelledDrivenM = 0.0f;  // Distance travelled by driven wheels
            float AnimationWheelRadiusM = MSTSWagon.WheelRadiusM; // Radius of non driven wheels
            float AnimationDriveWheelRadiusM = MSTSWagon.DriverWheelRadiusM; // Radius of driven wheels

            if (MSTSWagon is MSTSLocomotive mstsLocomotive && Viewer.Settings.UseAdvancedAdhesion && !Viewer.Settings.SimpleControlPhysics)
            {
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, the line should be changed as follows:
                //                                distanceTravelledM = MSTSWagon.WheelSpeedMpS * elapsedTime.ClockSeconds;

                distanceTravelledM = ((MSTSWagon.Train != null && MSTSWagon.Train.IsPlayerDriven && mstsLocomotive.UsingRearCab) ? -1 : 1) * MSTSWagon.WheelSpeedMpS * (float)elapsedTime.ClockSeconds;
                distanceTravelledDrivenM = Car.EngineType == EngineType.Steam
                    ? ((MSTSWagon.Train != null && MSTSWagon.Train.IsPlayerDriven && mstsLocomotive.UsingRearCab) ? -1 : 1) * MSTSWagon.WheelSpeedSlipMpS * (float)elapsedTime.ClockSeconds
                    : ((MSTSWagon.Train != null && MSTSWagon.Train.IsPlayerDriven && mstsLocomotive.UsingRearCab) ? -1 : 1) * MSTSWagon.WheelSpeedMpS * (float)elapsedTime.ClockSeconds;
            }
            else // set values for simple adhesion
            {

                distanceTravelledM = ((MSTSWagon is MSTSLocomotive locomotive1 && MSTSWagon.Train != null && MSTSWagon.Train.IsPlayerDriven && locomotive1.UsingRearCab) ? -1 : 1) * MSTSWagon.SpeedMpS * (float)elapsedTime.ClockSeconds;
                distanceTravelledDrivenM = ((MSTSWagon is MSTSLocomotive locomotive && MSTSWagon.Train != null && MSTSWagon.Train.IsPlayerDriven && locomotive.UsingRearCab) ? -1 : 1) * MSTSWagon.SpeedMpS * (float)elapsedTime.ClockSeconds;
            }

            if (Car.BrakeSkid) // if car wheels are skidding because of brakes locking wheels up then stop wheels rotating.
            {
                if ( ((MSTSLocomotive)MSTSWagon).DriveWheelOnlyBrakes)
                {
                    distanceTravelledDrivenM = 0.0f;
                }
                else
                {
                    distanceTravelledM = 0.0f;
                    distanceTravelledDrivenM = 0.0f;
                }

            }

            // Running gear and drive wheel rotation (animation) in steam locomotives
            if (!RunningGear.Empty() && AnimationDriveWheelRadiusM > 0.001)
                RunningGear.UpdateLoop(distanceTravelledDrivenM / MathHelper.TwoPi / AnimationDriveWheelRadiusM);


            // Wheel rotation (animation) - for non-drive wheels in steam locomotives and all wheels in other stock
            if (wheelPartIndexes.Count > 0)
            {
                var wheelCircumferenceM = MathHelper.TwoPi * AnimationWheelRadiusM;
                var rotationalDistanceR = MathHelper.TwoPi * distanceTravelledM / wheelCircumferenceM;  // in radians
                WheelRotationR = MathHelper.WrapAngle(WheelRotationR - rotationalDistanceR);
                var wheelRotationMatrix = Matrix.CreateRotationX(WheelRotationR);
                foreach (var iMatrix in wheelPartIndexes)
                {
                    TrainCarShape.XNAMatrices[iMatrix] = wheelRotationMatrix * TrainCarShape.SharedShape.Matrices[iMatrix];
                }
            }

#if DEBUG_WHEEL_ANIMATION

            Trace.TraceInformation("========================== Debug Animation in MSTSWagonViewer.cs ==========================================");
            Trace.TraceInformation("Slip speed - Car ID: {0} WheelDistance: {1} SlipWheelDistance: {2}", Car.CarID, distanceTravelledM, distanceTravelledDrivenM);
            Trace.TraceInformation("Wag Speed - Wheelspeed: {0} Slip: {1} Train: {2}", MSTSWagon.WheelSpeedMpS, MSTSWagon.WheelSpeedSlipMpS, MSTSWagon.SpeedMpS);
            Trace.TraceInformation("Wheel Radius - DriveWheel: {0} NonDriveWheel: {1}", AnimationDriveWheelRadiusM, AnimationWheelRadiusM);

#endif

            // truck angle animation
            foreach (var p in Car.Parts)
            {
                if (p.Matrix <= 0)
                    continue;
                Matrix m = Matrix.Identity;
                m.Translation = TrainCarShape.SharedShape.Matrices[p.Matrix].Translation;
                m.M11 = p.Cos;
                m.M13 = p.Sin;
                m.M31 = -p.Sin;
                m.M33 = p.Cos;

                // To cancel out any vibration, apply the inverse here. If no vibration is present, this matrix will be Matrix.Identity.
                TrainCarShape.XNAMatrices[p.Matrix] = Car.VibrationInverseMatrix * m;
            }

            if ((MSTSWagon.Train?.IsPlayerDriven ?? false) && !Viewer.Settings.SimpleControlPhysics)
                // Place the coupler in the centre of the car
                Car.WorldPosition.XNAMatrix.Decompose(out Vector3 scale, out Quaternion quaternion, out Vector3 translation);
            {
                UpdateCouplers(frame, elapsedTime);
            }

            // Applies MSTS style freight animation for coal load on the locomotive, crews, and other static animations.
            // Takes the form of FreightAnim ( A B C )
            // MSTS allowed crew figures to be inserted into the tender WAG file and thus be displayed on the locomotive.
            // It appears that only one MSTS type FA can be used per vehicle (to be confirmed?)
            // For coal load variation, C should be absent (set to 1 when read in WAG file) or >0 - sets FreightAnimFlag; and A > B
            // To disable coal load variation and insert a static (crew) shape on the tender breech, one of the conditions indicated above
            if (FreightShape != null && !(Viewer.Camera.AttachedCar == this.MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D))
            {
                bool SteamAnimShape = false;
                float FuelControllerLevel = 0.0f;

                // For coal load variation on locomotives determine the current fuel level - and whether locomotive is a tender or tank type locomotive.
                if (MSTSWagon.WagonType == WagonType.Tender || MSTSWagon is MSTSSteamLocomotive)
                {

                    var NonTenderSteamLocomotive = MSTSWagon as MSTSSteamLocomotive;

                    if (MSTSWagon.WagonType == WagonType.Tender || MSTSWagon is MSTSLocomotive && (MSTSWagon.EngineType == EngineType.Steam && NonTenderSteamLocomotive.IsTenderRequired == 0.0))
                    {

                        if (MSTSWagon.TendersSteamLocomotive == null)
                            MSTSWagon.FindTendersSteamLocomotive();

                        if (MSTSWagon.TendersSteamLocomotive != null)
                        {
                            FuelControllerLevel = MSTSWagon.TendersSteamLocomotive.FuelController.CurrentValue;
                            SteamAnimShape = true;
                        }
                        else if (NonTenderSteamLocomotive != null)
                        {
                            FuelControllerLevel = NonTenderSteamLocomotive.FuelController.CurrentValue;
                            SteamAnimShape = true;
                        }
                    }
                }
                Orts.Common.Position.WorldPosition freightLocation = Car.WorldPosition;
                // Set height of FAs - if relevant conditions met, use default position co-ords defined above
                if (FreightShape.XNAMatrices.Length > 0)
                {
                    // For tender coal load animation 
                    if (MSTSWagon.FreightAnimFlag > 0 && MSTSWagon.FreightAnimMaxLevelM > MSTSWagon.FreightAnimMinLevelM && SteamAnimShape)
                    {
                        FreightShape.XNAMatrices[0].M42 = MSTSWagon.FreightAnimMinLevelM + FuelControllerLevel * (MSTSWagon.FreightAnimMaxLevelM - MSTSWagon.FreightAnimMinLevelM);
                    }
                    // reproducing MSTS strange behavior; used to display loco crew when attached to tender
                    else if (MSTSWagon.WagonType == WagonType.Tender)
                    {
                        //freightLocation.M42 += MSTSWagon.FreightAnimMaxLevelM;
                        freightLocation = Car.WorldPosition.ChangeTranslation(0, MSTSWagon.FreightAnimMaxLevelM, 0);
                    }
                }
                // Display Animation Shape                    
                FreightShape.PrepareFrame(frame, elapsedTime, freightLocation);
            }

            if (FreightAnimations != null)
            {
                foreach (var freightAnim in FreightAnimations.Animations)
                {
                    if (freightAnim.Animation is FreightAnimationStatic)
                    {
                        var animation = freightAnim.Animation as FreightAnimationStatic;
                        if (!((animation.Visibility[(int)FreightAnimationStatic.VisibleFrom.Cab3D] &&
                            Viewer.Camera.AttachedCar == this.MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D) ||
                            (animation.Visibility[(int)FreightAnimationStatic.VisibleFrom.Cab2D] &&
                            Viewer.Camera.AttachedCar == this.MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab) ||
                            (animation.Visibility[(int)FreightAnimationStatic.VisibleFrom.Outside] && (Viewer.Camera.AttachedCar != this.MSTSWagon ||
                            (Viewer.Camera.Style != CameraStyle.Cab3D && Viewer.Camera.Style != CameraStyle.Cab))))) continue;
                    }
                    if (freightAnim.FreightShape != null && !((freightAnim.Animation is FreightAnimationContinuous) && (freightAnim.Animation as FreightAnimationContinuous).LoadPerCent == 0))
                    {
                        //freightAnim.FreightShape.Location = Car.WorldPosition;
                        if (freightAnim.FreightShape.XNAMatrices.Length > 0)
                        {
                            if (freightAnim.Animation is FreightAnimationContinuous)
                            {
                                var continuousFreightAnim = freightAnim.Animation as FreightAnimationContinuous;
                                if (MSTSWagon.FreightAnimations.IsGondola) freightAnim.FreightShape.XNAMatrices[0] = TrainCarShape.XNAMatrices[1];
                                freightAnim.FreightShape.XNAMatrices[0].M42 = continuousFreightAnim.MinHeight +
                                   continuousFreightAnim.LoadPerCent / 100 * (continuousFreightAnim.MaxHeight - continuousFreightAnim.MinHeight);
                            }
                            if (freightAnim.Animation is FreightAnimationStatic)
                            {
                                var staticFreightAnim = freightAnim.Animation as FreightAnimationStatic;
                                freightAnim.FreightShape.XNAMatrices[0].M41 = staticFreightAnim.XOffset;
                                freightAnim.FreightShape.XNAMatrices[0].M42 = staticFreightAnim.YOffset;
                                freightAnim.FreightShape.XNAMatrices[0].M43 = staticFreightAnim.ZOffset;
                            }

                        }
                        // Forcing rotation of freight shape
                        freightAnim.FreightShape.PrepareFrame(frame, elapsedTime);
                    }
                }
            }

            // Get the current height above "sea level" for the relevant car
            Car.CarHeightAboveSeaLevel = Viewer.Tiles.GetElevation(Car.WorldPosition.WorldLocation);

            // Control visibility of passenger cabin when inside it
            if (Viewer.Camera.AttachedCar == this.MSTSWagon
                 && //( Viewer.ViewPoint == Viewer.ViewPoints.Cab ||  // TODO, restore when we complete cab views - 
                     Viewer.Camera.Style == CameraStyle.Passenger)
            {
                // We are in the passenger cabin
                if (InteriorShape != null)
                    InteriorShape.PrepareFrame(frame, elapsedTime);
                else
                    TrainCarShape.PrepareFrame(frame, elapsedTime);
            }
            else
            {
                // Skip drawing if 2D or 3D Cab view - Cab view already drawn - by GeorgeS changed by DennisAT
                if (Viewer.Camera.AttachedCar == this.MSTSWagon &&
                    (Viewer.Camera.Style == CameraStyle.Cab || Viewer.Camera.Style == CameraStyle.Cab3D))
                    return;

                // We are outside the passenger cabin
                TrainCarShape.PrepareFrame(frame, elapsedTime);
            }

        }

        /// <summary>
        /// Position couplers at each end of car and adjust their angle to mate with adjacent car
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="elapsedTime"></param>
        private void UpdateCouplers(RenderFrame frame, ElapsedTime elapsedTime)
        {
            AnimatedShape couplerShape;
            Matrix couplerPosition;
            AnimatedShape airhoseShape;
            Matrix airhosePosition;

            // Display front coupler in sim if open coupler shape is configured, otherwise skip to next section, and just display closed (default) coupler if configured
            if (FrontCouplerShape != null && !(Viewer.Camera.AttachedCar == MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D))
            {
                // Get the movement that would be needed to locate the coupler on the car if they were pointing in the default direction.
                Vector3 displacement = new Vector3
                {
                    X = Car.FrontCouplerAnimation.Width,
                    Y = Car.FrontCouplerAnimation.Height,
                    Z = (Car.FrontCouplerAnimation.Length + (Car.CarLengthM / 2.0f) + Car.FrontCouplerSlackM - Car.WagonFrontCouplerCurveExtM)
                };

                Vector3 placement = PositionCoupler(Car, displacement);

                int tileX = Car.WorldPosition.TileX;
                int tileZ = Car.WorldPosition.TileZ;
                couplerPosition = MatrixExtension.ChangeTranslation(Car.WorldPosition.XNAMatrix, placement);
                couplerPosition = AlignCouplerWithCar(couplerPosition, Car.Flipped);
                if (Car.CarAhead != null) // Display animated coupler if there is a car infront of this car
                {
                    // Rotate the coupler to align with the calculated angle direction
                    couplerPosition = Matrix.CreateRotationY(Car.AdjustedWagonFrontCouplerAngle) * couplerPosition;

                    // If the car ahead does not have an animated coupler then location values will be zero for car ahaead, and no coupler will display. Hence do not correct coupler location 
                    if (Car.CarAhead.RearCouplerLocation != Vector3.Zero)
                    {
                        // Next section tests front coupler against rear coupler on previous car. If they are not located at the same position, then location is set the same as previous car.
                        // For some reason flipped cars have a small location error, and hence couplers do not align.
                        float absXc = Math.Abs(couplerPosition.Translation.X - Car.CarAhead.RearCouplerLocation.X);
                        float absYc = Math.Abs(couplerPosition.Translation.Y - Car.CarAhead.RearCouplerLocation.Y);
                        float absZc = Math.Abs(couplerPosition.Translation.Z - Car.CarAhead.RearCouplerLocation.Z);

                        if (absXc > 0.005 || absYc > 0.005 || absZc > 0.005)
                        {
                            couplerPosition.Translation = Car.CarAhead.RearCouplerLocation; // Set coupler to same location as previous car coupler
                            tileX = Car.CarAhead.WorldPosition.TileX;
                            tileZ = Car.CarAhead.WorldPosition.TileZ;
                        }
                    }
                    couplerShape = FrontCouplerShape;
                }
                else if (FrontCouplerOpenShape != null && Car.FrontCouplerOpenFitted && Car.FrontCouplerOpen) // Display open coupler if no car in front of car, and an open coupler shape is present
                {
                    couplerShape = FrontCouplerOpenShape;
                }
                else //Display closed static coupler by default if other conditions not met
                {
                    couplerShape = FrontCouplerShape;
                }
                couplerShape.PrepareFrame(frame, elapsedTime, new WorldPosition(tileX, tileZ, couplerPosition));
            }

            // Display rear coupler in sim if open coupler shape is configured, otherwise skip to next section, and just display closed (default) coupler if configured
            if (RearCouplerShape != null && !(Viewer.Camera.AttachedCar == MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D))
            {
                // Get the movement that would be needed to locate the coupler on the car if they were pointing in the default direction.
                Vector3 displacement = new Vector3
                {
                    X = Car.RearCouplerAnimation.Width,
                    Y = Car.RearCouplerAnimation.Height,
                    Z = -(Car.RearCouplerAnimation.Length + (Car.CarLengthM / 2.0f) + Car.RearCouplerSlackM - Car.WagonRearCouplerCurveExtM)  // Reversed as this is the rear coupler of the wagon
                };

                Vector3 placement = PositionCoupler(Car, displacement);

                couplerPosition = MatrixExtension.ChangeTranslation(Car.WorldPosition.XNAMatrix, placement);
                couplerPosition = AlignCouplerWithCar(couplerPosition, Car.Flipped);

                if (Car.CarBehind != null) // Display animated coupler if there is a car behind this car
                {
                    // Rotate the coupler to align with the calculated angle direction
                    couplerPosition = Matrix.CreateRotationY(Car.AdjustedWagonFrontCouplerAngle) * couplerPosition;

                    couplerShape = RearCouplerShape;
                    Car.RearCouplerLocation = couplerPosition.Translation;

                }
                else if (RearCouplerOpenShape != null && Car.RearCouplerOpenFitted && Car.RearCouplerOpen) // Display open coupler if no car is behind car, and an open coupler shape is present
                {
                    couplerShape = RearCouplerOpenShape;
                }
                else //Display closed static coupler by default if other conditions not met
                {
                    couplerShape = RearCouplerShape;
                }
                couplerShape.PrepareFrame(frame, elapsedTime, new WorldPosition(Car.WorldPosition.TileX, Car.WorldPosition.TileZ, couplerPosition));
            }

            if (FrontAirHoseShape != null && !(Viewer.Camera.AttachedCar == MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D))
            {
                // Get the movement that would be needed to locate the coupler on the car if they were pointing in the default direction.
                Vector3 displacement = new Vector3
                {
                    X = Car.FrontAirHoseAnimation.Width,
                    Y = Car.FrontAirHoseAnimation.Height,
                    Z = (Car.FrontCouplerAnimation.Length + (Car.CarLengthM / 2.0f) + Car.FrontCouplerSlackM)
                };

                if (Car.CarAhead != null) // Display animated coupler if there is a car behind this car
                {
                    displacement.Y += Car.FrontAirHoseHeightAdjustmentM;

                    Vector3 placement = PositionCoupler(Car, displacement);

                    airhosePosition = MatrixExtension.ChangeTranslation(Car.WorldPosition.XNAMatrix, placement);
                    airhosePosition = AlignCouplerWithCar(airhosePosition, Car.Flipped);

                    // Rotate the airhose to align with the calculated angle direction
                    airhosePosition = Matrix.CreateRotationZ(Car.FrontAirHoseZAngleAdjustmentRad) * airhosePosition;
                    airhosePosition = Matrix.CreateRotationY(Car.FrontAirHoseYAngleAdjustmentRad) * airhosePosition;

                    airhoseShape = FrontAirHoseShape;
                }
                else
                {
                    Vector3 placement = PositionCoupler(Car, displacement);

                    airhosePosition = MatrixExtension.ChangeTranslation(Car.WorldPosition.XNAMatrix, placement);
                    airhosePosition = AlignCouplerWithCar(airhosePosition, Car.Flipped);

                    if (FrontAirHoseDisconnectedShape != null && Car.RearCouplerOpenFitted && Car.RearCouplerOpen) // Display open coupler if no car is behind car, and an open coupler shape is present
                    {
                        airhoseShape = FrontAirHoseDisconnectedShape;
                    }
                    else //Display closed static coupler by default if other conditions not met
                    {
                        airhoseShape = FrontAirHoseShape;
                    }
                }
                airhoseShape.PrepareFrame(frame, elapsedTime, new WorldPosition(Car.WorldPosition.TileX, Car.WorldPosition.TileZ, airhosePosition));
            }


            // Display rear airhose in sim if open coupler shape is configured, otherwise skip to next section, and just display closed (default) coupler if configured
            if (RearAirHoseShape != null && !(Viewer.Camera.AttachedCar == MSTSWagon && Viewer.Camera.Style == CameraStyle.Cab3D))
            {
                // Get the movement that would be needed to locate the coupler on the car if they were pointing in the default direction.
                Vector3 displacement = new Vector3
                {
                    X = Car.RearAirHoseAnimation.Width,
                    Y = Car.RearAirHoseAnimation.Height,
                    Z = -(Car.RearCouplerAnimation.Length + (Car.CarLengthM / 2.0f) + Car.RearCouplerSlackM)  // Reversed as this is the rear coupler of the wagon
                };

                if (Car.CarBehind != null) // Display animated coupler if there is a car behind this car
                {
                    displacement.Y += Car.FrontAirHoseHeightAdjustmentM;

                    Vector3 placement = PositionCoupler(Car, displacement);

                    // Get the movement that would be needed to locate the air hose on the car if they were pointing in the default direction.
                    airhosePosition = MatrixExtension.ChangeTranslation(Car.WorldPosition.XNAMatrix, placement);
                    airhosePosition = AlignCouplerWithCar(airhosePosition, Car.Flipped);

                    // Rotate the airhose to align with the calculated angle direction
                    airhosePosition = Matrix.CreateRotationZ(Car.FrontAirHoseZAngleAdjustmentRad) * airhosePosition;
                    airhosePosition = Matrix.CreateRotationY(Car.FrontAirHoseYAngleAdjustmentRad) * airhosePosition;

                    airhoseShape = RearAirHoseShape;
                }
                else
                {
                    Vector3 placement = PositionCoupler(Car, displacement);

                    airhosePosition = MatrixExtension.ChangeTranslation(Car.WorldPosition.XNAMatrix, placement);
                    airhosePosition = AlignCouplerWithCar(airhosePosition, Car.Flipped);

                    if (RearAirHoseDisconnectedShape != null && Car.RearCouplerOpenFitted && Car.RearCouplerOpen) // Display open coupler if no car is behind car, and an open coupler shape is present
                    {
                        airhoseShape = RearAirHoseDisconnectedShape;
                    }
                    else //Display closed static coupler by default if other conditions not met
                    {
                        airhoseShape = RearAirHoseShape;
                    }
                }
                airhoseShape.PrepareFrame(frame, elapsedTime, new WorldPosition(Car.WorldPosition.TileX, Car.WorldPosition.TileZ, airhosePosition));
            }


        }

        /// <summary>
        /// Positions the coupler at the at the centre of the car (world position), and then rotates it to the end of the car.
        /// Returns a quaternion for the car.
        /// </summary>
        /// <param name="car"></param>
        /// <param name="couplerShape"></param>
        /// <param name="displacement"></param>
        /// <returns></returns>
        private static Vector3 PositionCoupler(TrainCar car, in Vector3 displacement)
        {
            // ToDO - For some reason aligning the coupler with a flipped car introduces a small error in the coupler position such that the couplers between a normal and flipped 
            // car will not align correctly.
            // To correct this "somewhat" a test has been introduced to align coupler location with the previous car. See code above in front coupler.

            // Place the coupler in the centre of the car
            Matrix matrix = car.WorldPosition.XNAMatrix;
            if (car.Flipped)
            {
                matrix.M11 *= -1;
                matrix.M13 *= -1;
                matrix.M21 *= -1;
                matrix.M23 *= -1;
                matrix.M31 *= -1;
                matrix.M33 *= -1;
            }

            // Get the orientation of the car as a quaternion
            matrix.Decompose(out _, out Quaternion quaternion, out _);

            // Reverse the y axis (plan view) component - perhaps because XNA is opposite to MSTS
            quaternion.X *= -1;
            quaternion.Y *= -1;


            // Rotate the displacement to match the orientation of the car
            Vector3 rotatedDisplacement = Vector3.Transform(displacement, quaternion);

            rotatedDisplacement.Z *= -1;
            //// Apply the rotation to the coupler's displacement to swing it round to the end of the wagon
            return rotatedDisplacement;
        }

        /// <summary>
        /// Rotate the coupler to align with the direction (attitude) of the car.
        /// </summary>
        /// <param name="car"></param>
        /// <param name="couplerShape"></param>
        private static Matrix AlignCouplerWithCar(Matrix position, bool flipped)
        {
            if (flipped)
            {
                position.M11 *= -1;
                position.M13 *= -1;
                position.M21 *= -1;
                position.M23 *= -1;
                position.M31 *= -1;
                position.M33 *= -1;
            }
            return position;
        }

        /// <summary>
        /// Unload and release the car - its not longer being displayed
        /// </summary>
        public override void Unload()
        {
            // Removing sound sources from sound update thread
            Viewer.SoundProcess.RemoveSoundSources(this);

            base.Unload();
        }


        /// <summary>
        /// Load the various car sounds
        /// </summary>
        /// <param name="wagonFolderSlash"></param>
        private void LoadCarSounds(string wagonFolderSlash)
        {
            if (MSTSWagon.MainSoundFileName != null) LoadCarSound(wagonFolderSlash, MSTSWagon.MainSoundFileName);
            if (MSTSWagon.InteriorSoundFileName != null) LoadCarSound(wagonFolderSlash, MSTSWagon.InteriorSoundFileName);
            if (MSTSWagon.Cab3DSoundFileName != null) LoadCarSound(wagonFolderSlash, MSTSWagon.InteriorSoundFileName);
        }


        /// <summary>
        /// Load the car sound, attach it to the car
        /// check first in the wagon folder, then the global folder for the sound.
        /// If not found, report a warning.
        /// </summary>
        /// <param name="wagonFolder"></param>
        /// <param name="filename"></param>
        protected void LoadCarSound(string wagonFolder, string filename)
        {
            if (filename == null)
                return;
            string smsFilePath = Path.GetFullPath(Path.Combine(wagonFolder, "sound", filename));
            if (!File.Exists(smsFilePath))
                smsFilePath = Path.GetFullPath(Viewer.Simulator.RouteFolder.ContentFolder.SoundFile(filename));
            if (!File.Exists(smsFilePath))
            {
                Trace.TraceWarning("Cannot find {1} car sound file {0}", filename, wagonFolder);
                return;
            }

            try
            {
                Viewer.SoundProcess.AddSoundSource(this, new SoundSource(MSTSWagon, this, smsFilePath));
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException(smsFilePath, error));
            }
        }

        /// <summary>
        /// Load the inside and outside sounds for the default level 0 track type.
        /// </summary>
        private void LoadTrackSounds()
        {
            if (Viewer.TrackTypes.Count > 0)  // TODO, still have to figure out if this should be part of the car, or train, or track
            {
                if (!string.IsNullOrEmpty(MSTSWagon.InteriorSoundFileName))
                    LoadTrackSound(Viewer.TrackTypes[0].InsideSound);

                LoadTrackSound(Viewer.TrackTypes[0].OutsideSound);
            }
        }

        /// <summary>
        /// Load the sound source, attach it to the car.
        /// Check first in route\SOUND folder, then in base\SOUND folder.
        /// </summary>
        /// <param name="filename"></param>
        private void LoadTrackSound(string filename)
        {
            if (filename == null)
                return;
            string path = Viewer.Simulator.RouteFolder.SoundFile(filename);
            if (!File.Exists(path))
                path = Viewer.Simulator.RouteFolder.ContentFolder.SoundFile(filename);
            if (!File.Exists(path))
            {
                Trace.TraceWarning("Cannot find track sound file {0}", filename);
                return;
            }
            Viewer.SoundProcess.AddSoundSource(this, new SoundSource(MSTSWagon, this, path));
        }

        internal override void Mark()
        {
            TrainCarShape.Mark();
            FreightShape?.Mark();
            InteriorShape?.Mark();
            FreightAnimations?.Mark();
            FrontCouplerShape?.Mark();
            FrontCouplerOpenShape?.Mark();
            RearCouplerShape?.Mark();
            RearCouplerOpenShape?.Mark();
            FrontAirHoseShape?.Mark();
            FrontAirHoseDisconnectedShape?.Mark();
            RearAirHoseShape?.Mark();
            RearAirHoseDisconnectedShape?.Mark();

            foreach (var pdl in ParticleDrawers.Values)
            {
                foreach (var pd in pdl)
                {
                    pd.Mark();
                }
            }
        }
    }
}
