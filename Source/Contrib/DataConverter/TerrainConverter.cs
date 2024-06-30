// COPYRIGHT 2016 by the Open Rails project.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.DataConverter
{
    internal class TerrainConverter : IDataConverter
    {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
        public TerrainConverter()
        {
        }

        public void ShowConversions()
        {
            //                "1234567890123456789012345678901234567890123456789012345678901234567890123456789"
            //                "    Input   Output  Description"
            Console.WriteLine("    t       dae     Creates a set of COLLADA files for the tile's terrain.");
            Console.WriteLine("    w       dae     Creates a set of COLLADA files for the tile's terrain.");
        }

        public bool DoConversion(DataConversion conversion)
        {
            // We can convert from .t or .w files.
            if (Path.GetExtension(conversion.Input) != ".t" && Path.GetExtension(conversion.Input) != ".w")
            {
                return false;
            }
            // We can convert to .dae files.
            if (conversion.Output.Any(output => Path.GetExtension(output) != ".dae"))
            {
                return false;
            }
            if (!File.Exists(conversion.Input))
            {
                throw new FileNotFoundException("", conversion.Input);
            }

            if (Path.GetExtension(conversion.Input) == ".w")
            {
                // Convert from world file to tile file, by parsing the X, Z coordinates from filename.
                string filename = Path.GetFileNameWithoutExtension(conversion.Input);
                if (filename.Length != 15 ||
                    filename[0] != 'w' ||
                    (filename[1] != '+' && filename[1] != '-') ||
                    (filename[8] != '+' && filename[8] != '-') ||
                    !int.TryParse(filename.AsSpan(1, 7), out int tileX) ||
                    !int.TryParse(filename.AsSpan(8, 7), out int tileZ))
                {
                    throw new InvalidCommandLineException("Unable to parse tile coordinates from world filename: " + filename);
                }
                string tilesDirectory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(conversion.Input)), "Tiles");
                string tileName = TileHelper.FromTileXZ(tileX, tileZ, TileHelper.Zoom.Small);
                conversion.SetInput(Path.Combine(tilesDirectory, tileName + ".t"));
            }

            string baseFileName = Path.Combine(Path.GetDirectoryName(conversion.Input), Path.GetFileNameWithoutExtension(conversion.Input));

            Terrain terrain = TerrainFile.LoadTerrainFile(baseFileName + ".t");

            int sampleCount = terrain.Samples.SampleCount;
            TerrainAltitudeFile yFile = new TerrainAltitudeFile(baseFileName + "_y.raw", sampleCount);
            if (File.Exists(baseFileName + "_f.raw"))
            {
                _ = TerrainFlagsFile.LoadTerrainFlagsFile(baseFileName + "_f.raw", sampleCount);
            }

            int patchCount = terrain.Patchsets[0].PatchSize;
            for (int x = 0; x < patchCount; x++)
            {
                for (int z = 0; z < patchCount; z++)
                {
                    TerrainConverterPatch patch = new TerrainConverterPatch(terrain, yFile, x, z);

                    XNamespace cNS = "http://www.collada.org/2005/11/COLLADASchema";
                    XDocument colladaDocument = new XDocument(
                        new XDeclaration("1.0", "UTF-8", "false"),
                        new XElement(cNS + "COLLADA",
                            new XAttribute("version", "1.4.1"),
                            new XElement(cNS + "asset",
                                new XElement(cNS + "created", DateTime.UtcNow.ToString("o")),
                                new XElement(cNS + "modified", DateTime.UtcNow.ToString("o"))
                            ),
                            new XElement(cNS + "library_effects",
                                new XElement(cNS + "effect",
                                    new XAttribute("id", "Library-Effect-GreenPhong"),
                                    new XElement(cNS + "profile_COMMON",
                                        new XElement(cNS + "technique",
                                            new XAttribute("sid", "phong"),
                                            new XElement(cNS + "phong",
                                                new XElement(cNS + "emission",
                                                    new XElement(cNS + "color", "1.0 1.0 1.0 1.0")
                                                ),
                                                new XElement(cNS + "ambient",
                                                    new XElement(cNS + "color", "1.0 1.0 1.0 1.0")
                                                ),
                                                new XElement(cNS + "diffuse",
                                                    new XElement(cNS + "color", "0.0 1.0 0.0 1.0")
                                                ),
                                                new XElement(cNS + "specular",
                                                    new XElement(cNS + "color", "1.0 1.0 1.0 1.0")
                                                ),
                                                new XElement(cNS + "shininess",
                                                    new XElement(cNS + "float", "20.0")
                                                ),
                                                new XElement(cNS + "reflective",
                                                    new XElement(cNS + "color", "1.0 1.0 1.0 1.0")
                                                ),
                                                new XElement(cNS + "reflectivity",
                                                    new XElement(cNS + "float", "0.5")
                                                ),
                                                new XElement(cNS + "transparent",
                                                    new XElement(cNS + "color", "1.0 1.0 1.0 1.0")
                                                ),
                                                new XElement(cNS + "transparency",
                                                    new XElement(cNS + "float", "1.0")
                                                )
                                            )
                                        )
                                    )
                                )
                            ),
                            new XElement(cNS + "library_materials",
                                new XElement(cNS + "material",
                                    new XAttribute("id", "Library-Material-Terrain"),
                                    new XAttribute("name", "Terrain material"),
                                    new XElement(cNS + "instance_effect",
                                        new XAttribute("url", "#Library-Effect-GreenPhong")
                                    )
                                )
                            ),
                            new XElement(cNS + "library_geometries",
                                new XElement(cNS + "geometry",
                                    new XAttribute("id", "Library-Geometry-Terrain"),
                                    new XAttribute("name", "Terrain geometry"),
                                    new XElement(cNS + "mesh",
                                        new XElement(cNS + "source",
                                            new XAttribute("id", "Library-Geometry-Terrain-Position"),
                                            new XElement(cNS + "float_array",
                                                new XAttribute("id", "Library-Geometry-Terrain-Position-Array"),
                                                new XAttribute("count", patch.GetVertexArrayLength()),
                                                patch.GetVertexArray()
                                            ),
                                            new XElement(cNS + "technique_common",
                                                new XElement(cNS + "accessor",
                                                    new XAttribute("source", "#Library-Geometry-Terrain-Position-Array"),
                                                    new XAttribute("count", patch.GetVertexLength()),
                                                    new XAttribute("stride", 3),
                                                    new XElement(cNS + "param",
                                                        new XAttribute("name", "X"),
                                                        new XAttribute("type", "float")
                                                    ),
                                                    new XElement(cNS + "param",
                                                        new XAttribute("name", "Y"),
                                                        new XAttribute("type", "float")
                                                    ),
                                                    new XElement(cNS + "param",
                                                        new XAttribute("name", "Z"),
                                                        new XAttribute("type", "float")
                                                    )
                                                )
                                            )
                                        ),
                                        new XElement(cNS + "vertices",
                                            new XAttribute("id", "Library-Geometry-Terrain-Vertex"),
                                            new XElement(cNS + "input",
                                                new XAttribute("semantic", "POSITION"),
                                                new XAttribute("source", "#Library-Geometry-Terrain-Position")
                                            )
                                        ),
                                        new XElement(cNS + "polygons",
                                            new XObject[] {
                                                new XAttribute("count", patch.GetPolygonLength()),
                                                new XAttribute("material", "MATERIAL"),
                                                new XElement(cNS + "input",
                                                    new XAttribute("semantic", "VERTEX"),
                                                    new XAttribute("source", "#Library-Geometry-Terrain-Vertex"),
                                                    new XAttribute("offset", 0)
                                                )
                                            }.Concat(patch.GetPolygonArray().Select(polygon => (XObject)new XElement(cNS + "p", polygon)))
                                        )
                                    )
                                )
                            ),
                            // Move nodes into <library_nodes/> to make them individual components in SketchUp.
                            //new XElement(cNS + "library_nodes",
                            //),
                            new XElement(cNS + "library_visual_scenes",
                                new XElement(cNS + "visual_scene",
                                    new XAttribute("id", "VisualScene-Default"),
                                    new XElement(cNS + "node",
                                        new XAttribute("id", "Node-Terrain"),
                                        new XAttribute("name", "Terrain"),
                                        new XElement(cNS + "instance_geometry",
                                            new XAttribute("url", "#Library-Geometry-Terrain"),
                                            new XElement(cNS + "bind_material",
                                                new XElement(cNS + "technique_common",
                                                    new XElement(cNS + "instance_material",
                                                        new XAttribute("symbol", "MATERIAL"),
                                                        new XAttribute("target", "#Library-Material-Terrain")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            ),
                            new XElement(cNS + "scene",
                                new XElement(cNS + "instance_visual_scene",
                                    new XAttribute("url", "#VisualScene-Default")
                                )
                            )
                        )
                    );

                    foreach (string output in conversion.Output)
                    {
                        string fileName = Path.ChangeExtension(output, $"{x:00}-{z:00}.dae");
                        colladaDocument.Save(fileName);
                    }
                }
            }

            return true;
        }
    }

    internal class TerrainConverterPatch
    {
        private readonly Terrain terrain;
        private readonly TerrainAltitudeFile YFile;
        private readonly int PatchX;
        private readonly int PatchZ;
        private readonly int PatchSize;

        public TerrainConverterPatch(Terrain terrain, TerrainAltitudeFile yFile, int patchX, int patchZ)
        {
            this.terrain = terrain;
            YFile = yFile;
            PatchX = patchX;
            PatchZ = patchZ;
            PatchSize = terrain.Samples.SampleCount / terrain.Patchsets[0].PatchSize;
        }

        private int GetElevation(int x, int z)
        {
            return YFile.ElevationAt(
                Math.Min(PatchX * PatchSize + x, terrain.Samples.SampleCount - 1),
                Math.Min(PatchZ * PatchSize + z, terrain.Samples.SampleCount - 1)
            );
        }

        public int GetVertexLength()
        {
            return (PatchSize + 1) * (PatchSize + 1);
        }

        public int GetVertexArrayLength()
        {
            return GetVertexLength() * 3;
        }

        public string GetVertexArray()
        {
            StringBuilder output = new StringBuilder();

            for (int x = 0; x <= PatchSize; x++)
            {
                for (int z = 0; z <= PatchSize; z++)
                {
                    output.AppendFormat(CultureInfo.InvariantCulture, $"{x * terrain.Samples.SampleSize} {GetElevation(x, z) * terrain.Samples.SampleScale} {z * terrain.Samples.SampleSize} ");
                }
            }

            return output.ToString();
        }

        public int GetPolygonLength()
        {
            return PatchSize * PatchSize * 2;
        }

        public List<string> GetPolygonArray()
        {
            List<string> output = new List<string>();
            int stride = PatchSize + 1;

            for (int x = 0; x < PatchSize; x++)
            {
                for (int z = 0; z < PatchSize; z++)
                {
                    int nw = (x + 0) + stride * (z + 0);
                    int ne = (x + 1) + stride * (z + 0);
                    int sw = (x + 0) + stride * (z + 1);
                    int se = (x + 1) + stride * (z + 1);
                    if ((z & 1) == (x & 1))
                    {
                        output.Add($"{nw} {se} {sw}");
                        output.Add($"{nw} {ne} {se}");
                    }
                    else
                    {
                        output.Add($"{ne} {se} {sw}");
                        output.Add($"{nw} {ne} {sw}");
                    }
                }
            }

            return output;
        }
#pragma warning restore CA1303 // Do not pass literals as localized parameters
    }
}
