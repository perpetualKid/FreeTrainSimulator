// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common;

using Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// 3D CabViewer
    /// </summary>
    public class CabViewer3D : TrainCarViewer
    {
        private readonly MSTSLocomotive locomotive;

        public PoseableShape TrainCarShape { get; }
        public Dictionary<(ControlType, int), AnimatedPartMultiState> AnimateParts { get; }
        private readonly Dictionary<(ControlType, int), CabGaugeNative3D> Gauges;
        private readonly Dictionary<(ControlType, int), AnimatedPart> onDemandAnimateParts; //like external wipers, and other parts that will be switched on by mouse in the future
                                                                                           //Dictionary<int, DigitalDisplay> DigitParts = null;
        private readonly Dictionary<(ControlType, int), CabDigit3D> digitParts3D;
        private readonly Dictionary<(ControlType, int), ThreeDimCabDPI> dpiDisplays3D;
        private readonly AnimatedPart externalWipers; // setting to zero to prevent a warning. Probably this will be used later. TODO
        private protected MSTSLocomotive MSTSLocomotive => (MSTSLocomotive)Car;

        private readonly MSTSLocomotiveViewer locomotiveViewer;
        private readonly SpriteBatchMaterial sprite2DCabView;
        private bool[] matrixVisible;

        public CabViewer3D(Viewer viewer, MSTSLocomotive car, MSTSLocomotiveViewer locoViewer)
            : base(viewer, car)
        {
            locomotive = car;
            sprite2DCabView = (SpriteBatchMaterial)viewer.MaterialManager.Load("SpriteBatch");
            locomotiveViewer = locoViewer;
            if (car.CabView3D != null)
            {
                var shapePath = car.CabView3D.ShapeFilePath;
                TrainCarShape = new PoseableShape(shapePath + '\0' + Path.GetDirectoryName(shapePath), car, ShapeFlags.ShadowCaster | ShapeFlags.Interior);
                locoViewer.CabRenderer3D = new CabRenderer(viewer, car, car.CabView3D.CVFFile);
            }
            else
                locoViewer.CabRenderer3D = locoViewer.CabRenderer;

            AnimateParts = new Dictionary<(ControlType, int), AnimatedPartMultiState>();
            //DigitParts = new Dictionary<int, DigitalDisplay>();
            digitParts3D = new Dictionary<(ControlType, int), CabDigit3D>();
            Gauges = new Dictionary<(ControlType, int), CabGaugeNative3D>();
            dpiDisplays3D = new Dictionary<(ControlType, int), ThreeDimCabDPI>();
            onDemandAnimateParts = new Dictionary<(ControlType, int), AnimatedPart>();
            // Find the animated parts
            if (TrainCarShape != null && TrainCarShape.SharedShape.Animations != null)
            {
                matrixVisible = new bool[TrainCarShape.SharedShape.MatrixNames.Count + 1];
                for (int i = 0; i < matrixVisible.Length; i++)
                    matrixVisible[i] = true;
                string typeName = "";
                AnimatedPartMultiState tmpPart = null;
                for (int iMatrix = 0; iMatrix < TrainCarShape.SharedShape.MatrixNames.Count; ++iMatrix)
                {
                    string matrixName = TrainCarShape.SharedShape.MatrixNames[iMatrix].ToUpper();
                    //Name convention
                    //TYPE:Order:Parameter-PartN
                    //e.g. ASPECT_SIGNAL:0:0-1: first ASPECT_SIGNAL, parameter is 0, this component is part 1 of this cab control
                    //     ASPECT_SIGNAL:0:0-2: first ASPECT_SIGNAL, parameter is 0, this component is part 2 of this cab control
                    //     ASPECT_SIGNAL:1:0  second ASPECT_SIGNAL, parameter is 0, this component is the only one for this cab control
                    typeName = matrixName.Split('-')[0]; //a part may have several sub-parts, like ASPECT_SIGNAL:0:0-1, ASPECT_SIGNAL:0:0-2
                    tmpPart = null;
                    int order = 0;
                    string parameter1 = "0", parameter2 = "";
                    CabViewControlRenderer style = null;
                    //ASPECT_SIGNAL:0:0
                    var tmp = typeName.Split(':');
                    if (tmp.Length > 1 && int.TryParse(tmp[1].Trim(), out order))
                    {
                        if (tmp.Length > 2)
                        {
                            parameter1 = tmp[2].Trim();
                            if (tmp.Length == 4) //we can get max two parameters per part
                                parameter2 = tmp[3].Trim();
                        }
                    }
                    else
                        continue;

                    ControlType cvcType = new ControlType(tmp[0].Trim());
                    var key = (cvcType, order);
                    switch (cvcType.CabViewControlType)
                    {
                        case CabViewControlType.ExternalWipers:
                        case CabViewControlType.Mirrors:
                        case CabViewControlType.LeftDoor:
                        case CabViewControlType.RightDoor:
                        case CabViewControlType.Orts_Item1Continuous:
                        case CabViewControlType.Orts_Item2Continuous:
                        case CabViewControlType.Orts_Item1TwoState:
                        case CabViewControlType.Orts_Item2TwoState:
                            //cvf file has no external wipers, left door, right door and mirrors key word
                            break;
                        default:
                            //cvf file has no external wipers, left door, right door and mirrors key word
                            if (!locoViewer.CabRenderer3D.ControlMap.TryGetValue(key, out style))
                            {
                                var cvfBasePath = Path.Combine(Path.GetDirectoryName(locomotive.WagFilePath), "CABVIEW");
                                var cvfFilePath = Path.Combine(cvfBasePath, locomotive.CVFFileName);
                                Trace.TraceWarning($"Cabview control {tmp[0].Trim()} has not been defined in CVF file {cvfFilePath}");
                            }
                            break;
                    }

                    if (style != null && style is CabViewDigitalRenderer)//digits?
                    {
                        //DigitParts.Add(key, new DigitalDisplay(viewer, TrainCarShape, iMatrix, parameter, locoViewer.ThreeDimentionCabRenderer.ControlMap[key]));
                        digitParts3D.Add(key, new CabDigit3D(viewer, iMatrix, parameter1, parameter2, TrainCarShape, locoViewer.CabRenderer3D.ControlMap[key], locomotive));
                    }
                    else if (style != null && style is CabViewGaugeRenderer)
                    {
                        var CVFR = (CabViewGaugeRenderer)style;

                        if (CVFR.GetGauge().ControlStyle != CabViewControlStyle.Pointer) //pointer will be animated, others will be drawn dynamicaly
                        {
                            Gauges.Add(key, new CabGaugeNative3D(viewer, iMatrix, parameter1, parameter2, TrainCarShape, locoViewer.CabRenderer3D.ControlMap[key]));
                        }
                        else
                        {//for pointer animation
                         //if there is a part already, will insert this into it, otherwise, create a new
                            if (!AnimateParts.TryGetValue(key, out tmpPart))
                            {
                                tmpPart = new AnimatedPartMultiState(TrainCarShape, key);
                                AnimateParts.Add(key, tmpPart);
                            }
                            tmpPart.AddMatrix(iMatrix); //tmpPart.SetPosition(false);
                        }
                    }
                    else if (style != null && style is DistributedPowerInterfaceRenderer)
                    {
                        dpiDisplays3D.Add(key, new ThreeDimCabDPI(viewer, iMatrix, parameter1, parameter2, TrainCarShape, locoViewer.CabRenderer3D.ControlMap[key]));
                    }
                    else
                    {
                        //if there is a part already, will insert this into it, otherwise, create a new
                        if (!AnimateParts.TryGetValue(key, out tmpPart))
                        {
                            tmpPart = new AnimatedPartMultiState(TrainCarShape, key);
                            AnimateParts.Add(key, tmpPart);
                        }
                        tmpPart.AddMatrix(iMatrix); //tmpPart.SetPosition(false);
                    }
                }
            }
        }

        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            CabViewControlRenderer cabRenderer;
            foreach (var p in AnimateParts)
            {
                if (p.Value.Type >= CabViewControlType.ExternalWipers) //for wipers, doors and mirrors
                {
                    switch (p.Value.Type)
                    {
                        case CabViewControlType.ExternalWipers:
                            p.Value.UpdateLoop(locomotive.Wiper, elapsedTime);
                            break;
                        case CabViewControlType.LeftDoor:
                        case CabViewControlType.RightDoor:
                            {
                                bool right = p.Value.Type == CabViewControlType.RightDoor ^ locomotive.Flipped ^ locomotive.GetCabFlipped();
                                DoorState state = (right ? locomotive.Doors[DoorSide.Right] : locomotive.Doors[DoorSide.Left]).State;
                                p.Value.UpdateState(state >= DoorState.Opening, elapsedTime);
                            }
                            break;
                        case CabViewControlType.Mirrors:
                            p.Value.UpdateState(locomotive.MirrorOpen, elapsedTime);
                            break;
                        case CabViewControlType.Orts_Item1Continuous:
                            p.Value.UpdateLoop(locomotive.GenericItem1, elapsedTime);
                            break;
                        case CabViewControlType.Orts_Item2Continuous:
                            p.Value.UpdateLoop(locomotive.GenericItem2, elapsedTime);
                            break;
                        case CabViewControlType.Orts_Item1TwoState:
                            p.Value.UpdateState(locomotive.GenericItem1, elapsedTime);
                            break;
                        case CabViewControlType.Orts_Item2TwoState:
                            p.Value.UpdateState(locomotive.GenericItem2, elapsedTime);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    bool doShow = true;
                    if (locomotiveViewer.CabRenderer3D.ControlMap.TryGetValue(p.Key, out cabRenderer))
                    {
                        if (!cabRenderer.IsPowered && cabRenderer.control.HideIfDisabled)
                        {
                            doShow = false;
                        }
                        else if (cabRenderer is CabViewDiscreteRenderer)
                        {
                            var control = cabRenderer.control;
                            if (control.Screens != null && control.Screens[0] != "all")
                            {
                                doShow = control.Screens.Any(screen =>
                                    locomotiveViewer.CabRenderer3D.ActiveScreen[control.Display] == screen);
                            }
                        }
                    }

                    foreach (int matrixIndex in p.Value.MatrixIndexes)
                        matrixVisible[matrixIndex] = doShow;

                    p.Value.Update(locomotiveViewer, elapsedTime); //for all other instruments with animations
                }
            }
            foreach (var p in digitParts3D)
            {
                var digital = p.Value.GaugeRenderer.control;
                if (digital.Screens != null && digital.Screens[0] != "all")
                {
                    foreach (var screen in digital.Screens)
                    {
                        if (locomotiveViewer.CabRenderer3D.ActiveScreen[digital.Display] == screen)
                        {
                            p.Value.PrepareFrame(frame, elapsedTime);
                            break;
                        }
                    }
                    continue;
                }
                p.Value.PrepareFrame(frame, elapsedTime);
            }
            foreach (var p in dpiDisplays3D)
            {
                var dpdisplay = p.Value.CVFR.control;
                if (dpdisplay.Screens != null && dpdisplay.Screens[0] != "all")
                {
                    foreach (var screen in dpdisplay.Screens)
                    {
                        if (locomotiveViewer.CabRenderer3D.ActiveScreen[dpdisplay.Display] == screen)
                        {
                            p.Value.PrepareFrame(frame, elapsedTime);
                            break;
                        }
                    }
                    continue;
                }
                p.Value.PrepareFrame(frame, elapsedTime);
            }
            foreach (var p in Gauges)
            {
                var gauge = p.Value.GaugeRenderer.control;
                if (gauge.Screens != null && gauge.Screens[0] != "all")
                {
                    foreach (var screen in gauge.Screens)
                    {
                        if (locomotiveViewer.CabRenderer3D.ActiveScreen[gauge.Display] == screen)
                        {
                            p.Value.PrepareFrame(frame, elapsedTime);
                            break;
                        }
                    }
                    continue;
                }
                p.Value.PrepareFrame(frame, elapsedTime);
            }

            if (externalWipers != null)
                externalWipers.UpdateLoop(locomotive.Wiper, elapsedTime);
            /*
            foreach (var p in DigitParts)
            {
                p.Value.PrepareFrame(frame, elapsedTime);
            }*/ //removed with 3D digits

            if (TrainCarShape != null)
                TrainCarShape.ConditionallyPrepareFrame(frame, elapsedTime, matrixVisible);
        }

        internal override void Mark()
        {
            TrainCarShape?.Mark();
            foreach (CabDigit3D threeDimCabDigit in digitParts3D.Values)
            {
                threeDimCabDigit.Mark();
            }
            foreach (ThreeDimCabDPI threeDimCabDPI in dpiDisplays3D.Values)
            {
                threeDimCabDPI.Mark();
            }
        }

        public override void HandleUserInput(in ElapsedTime elapsedTime)
        {
        }

        public override void RegisterUserCommandHandling()
        {
        }

        public override void UnregisterUserCommandHandling()
        {
        }

    } // Class ThreeDimentionCabViewer
}
