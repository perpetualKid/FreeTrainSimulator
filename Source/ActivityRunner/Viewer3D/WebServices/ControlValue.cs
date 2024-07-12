// COPYRIGHT 2021 by the Open Rails project.
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

using System.Collections.Generic;

using Orts.ActivityRunner.Viewer3D.RollingStock;
using Orts.ActivityRunner.Viewer3D.RollingStock.CabView;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    /// <summary>
    /// Contains information about a single cab control.
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct ControlValue
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public string TypeName { get; internal set; }
        public double MinValue { get; internal set; }
        public double MaxValue { get; internal set; }
        public double RangeFraction { get; internal set; }
    }

    public static class LocomotiveViewerExtensions
    {
        /// <summary>
        /// Get a list of all cab controls.
        /// </summary>
        /// <param name="viewer">The locomotive viewer instance.</param>
        /// <returns></returns>
        public static IList<ControlValue> GetWebControlValueList(this MSTSLocomotiveViewer viewer)
        {
            List<ControlValue> controlValueList = new List<ControlValue>();
            foreach (CabViewControlRenderer controlRenderer in viewer.CabRenderer.ControlMap.Values)
            {
                controlValueList.Add(new ControlValue
                { 
                    TypeName = controlRenderer.GetControlType().ToString(), 
                    MinValue = controlRenderer.control.ScaleRangeMin, 
                    MaxValue = controlRenderer.control.ScaleRangeMax, 
                    RangeFraction = controlRenderer.GetRangeFraction()
                    });
            }
            return controlValueList;
        }
    }
}
