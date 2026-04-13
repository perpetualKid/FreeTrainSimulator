// COPYRIGHT 2013, 2014, 2015, 2016 by the Open Rails project.
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
using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace Orts.Simulation
{
    public class SuperElevation
    {
        public ICollection<List<SectionGeometry>> Curves { get; }
        public Dictionary<int, List<SectionGeometry>> Sections { get; }
        public float MaximumAllowedM { get; }

        //check TDB for long curves and determine each section's position/elev in the curve
        public SuperElevation(Simulator simulator)
        {
            ArgumentNullException.ThrowIfNull(simulator);

            Curves = new List<List<SectionGeometry>>();
            Sections = new Dictionary<int, List<SectionGeometry>>();

            MaximumAllowedM = 0.07f + simulator.UserSettings.SuperElevationLevel / 100f;//max allowed elevation controlled by user setting

            TrackWorld trackWorld = TrackWorld.Instance;
            TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackModel.TrackDatabase;
            ImmutableDictionary<int, TrackSection> trackSections = RuntimeDataResolver.Instance.TrackSections.TrackSections;

            if (trackDatabase == null || trackWorld == null)
                return;

            List<SectionGeometry> SectionList = new List<SectionGeometry>();
            foreach (VectorNode vectorNode in trackDatabase.VectorNodes)
            {
                bool StartCurve = false;
                int CurveDir = 0;
                float Len = 0.0f;
                SectionList.Clear();
                int i = 0;
                int count = vectorNode.VectorSections.Length;
                foreach (VectorSectionNode vectorSection in vectorNode.VectorSections)//loop all curves
                {
                    i++;
                    if (!trackWorld.SectionGeometry.TryGetValue(vectorSection, out SectionGeometry sectionGeometry) || !sectionGeometry.HasGeometry)
                        continue;
                    if (!trackSections.TryGetValue(vectorSection.NodeIndex, out TrackSection sec))
                        continue;
                    if (Math.Abs(sec.Gauge - (simulator.UserSettings.TrackGauge / 1000f)) > 0.2)
                        continue;//the main route has a gauge different than mine
                    float angle = sec.Angle;
                    if (sectionGeometry.Curved && !angle.AlmostEqual(0f, 0.01f)) //a good curve
                    {
                        if (i == 1 || i == count)
                        {
                            //if (theCurve.Radius * (float)Math.Abs(theCurve.Angle * 0.0174) < 15f) continue; 
                        } //do not want the first and last piece of short curved track to be in the curve (they connected to switches)
                        if (!StartCurve) //we are beginning a curve
                        {
                            StartCurve = true;
                            CurveDir = Math.Sign(sec.Angle);
                            Len = 0f;
                        }
                        else if (CurveDir != Math.Sign(sec.Angle)) //we are in curve, but bending different dir
                        {
                            MarkSections(simulator, SectionList, Len); //treat the sections encountered so far, then restart with other dir
                            CurveDir = Math.Sign(sec.Angle);
                            SectionList.Clear();
                            Len = 0f; //StartCurve remains true as we are still in a curve
                        }
                        Len += (float)sectionGeometry.Length;
                        SectionList.Add(sectionGeometry);
                    }
                    else //meet a straight line
                    {
                        if (StartCurve) //we are in a curve, need to finish
                        {
                            MarkSections(simulator, SectionList, Len);
                            Len = 0f;
                            SectionList.Clear();
                        }
                        StartCurve = false;
                    }
                }
                if (StartCurve) // we are in a curve after looking at every section
                {
                    MarkSections(simulator, SectionList, Len);
                }
                SectionList.Clear();
            }
        }

        private void MarkSections(Simulator simulator, List<SectionGeometry> SectionList, float Len)
        {
            ArgumentNullException.ThrowIfNull(simulator);

            //if (Len < simulator.Settings.SuperElevationMinLen || SectionList.Count == 0) 
            if (SectionList.Count == 0)
                return;//too short a curve or the list is empty
            //loop all section to determine the max elevation for the whole track
            float sectionAngle = MathHelper.ToDegrees((float)SectionList[0].ArcAngle);
            double Curvature = sectionAngle * SectionList.Count * 33 / Len;//average radius in degree/100feet
            float Max = (float)(Math.Pow(simulator.RouteModel.SpeedRestrictions[SpeedRestrictionType.Route] * 2.25, 2) * 0.0007 * Math.Abs(Curvature) - 3); //in inch
            Max *= 2.5f;//change to cm
            Max = (float)Math.Round(Max * 2, MidpointRounding.AwayFromZero) / 200f;//closest to 5 mm increase;
            if (Max < 0.01f)
                return;
            if (Max > MaximumAllowedM)
                Max = MaximumAllowedM;//max
            Max = (float)Math.Atan(Max / 1.44f); //now change to rotation in radius by quick estimation as the angle is small

            Curves.Add(new List<SectionGeometry>(SectionList)); //add the curve
            MapWFiles2Sections(SectionList);//map these sections to tiles, so we can compute it quicker later
            if (SectionList.Count == 1)//only one section in the curve
            {
                SectionList[0].SetElevation(0f, 0f, Max);
            }
            else//more than one section in the curve
            {
                int count = 0;

                foreach (SectionGeometry section in SectionList)
                {
                    if (count == 0)
                    {
                        section.SetElevation(0f, Max, Max);
                    }
                    else if (count == SectionList.Count - 1)
                    {
                        section.SetElevation(Max, 0f, Max);
                    }
                    else
                    {
                        section.SetElevation(Max, Max, Max);
                    }
                    count++;
                }
            }
        }

        //find all sections in a tile, save the info to a look-up table
        private void MapWFiles2Sections(List<SectionGeometry> sections)
        {
            foreach (SectionGeometry sg in sections)
            {
                VectorSectionNode vsn = sg.Node.VectorSections[sg.SectionIndex];
                int key = Math.Abs(vsn.Location.TileX) + Math.Abs(vsn.Location.TileZ);
                if (Sections.TryGetValue(key, out List<SectionGeometry> value))
                    value.Add(sg);
                else
                {
                    List<SectionGeometry> tmpSections = new List<SectionGeometry>
                    {
                        sg
                    };
                    Sections.Add(key, tmpSections);
                }
            }
        }
    }
}
