﻿// COPYRIGHT 2017, 2018 by the Open Rails project.
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
using System.IO;

using Microsoft.Xna.Framework;

using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{
    public class ContainerParameters
    {
        private int index;

        public string Name { get; private set; }
        public string ShapeFileName { get; private set; }
        public string ContainerType { get; private set; }
        public Vector3 IntrinsicShapeOffset { get; private set; } = new Vector3(0f, 1.17f, 0f);

        public ContainerParameters(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json);

            json.ReadBlock(TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            // get values
            switch (item.Path)
            {
                case "Container.":
                    break;
                case "Container.Name":
                    Name = item.AsString("");
                    break;
                case "Container.Shape":
                    ShapeFileName = item.AsString(ShapeFileName);
                    break;
                case "Container.ContainerType":
                    ContainerType = item.AsString("40ftHC");
                    break;
                case "Container.IntrinsicShapeOffset[]":
                    switch (index)
                    {
                        case 0:
                            IntrinsicShapeOffset = new Vector3(item.AsFloat(0.0f), IntrinsicShapeOffset.Y, IntrinsicShapeOffset.Z);
                            break;
                        case 1:
                            IntrinsicShapeOffset = new Vector3(IntrinsicShapeOffset.X, item.AsFloat(0.0f), IntrinsicShapeOffset.Z);
                            break;
                        case 2:
                            IntrinsicShapeOffset = new Vector3(IntrinsicShapeOffset.X, IntrinsicShapeOffset.Y, item.AsFloat(0.0f));
                            break;
                        default:
                            return false;
                    }
                    index++;
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
