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

using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using System.Diagnostics;

namespace Orts.Formats.Msts.Files
{
    // GLOBAL TSECTION DAT

    public class TrackSectionsFile
	{
        public TrackSections TrackSections { get; private set; }
        public TrackShapes TrackShapes { get; private set; }
        public TrackPaths TrackSectionIndex { get; private set; } //route's tsection.dat

        public void AddRouteTSectionDatFile( string fileName )
		{
            using (STFReader stf = new STFReader(fileName, false))
            {
                if (stf.SimisSignature != "SIMISA@@@@@@@@@@JINX0T0t______")
                {
                    Trace.TraceWarning("Skipped invalid TSECTION.DAT in route folder");
                    return;
                }
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tracksections", ()=>{ TrackSections.AddRouteTrackSections(stf); }),
                    new STFReader.TokenProcessor("sectionidx", ()=>{ TrackSectionIndex = new TrackPaths(stf); }),
                    // todo read in SectionIdx part of RouteTSectionDat
                });
            }
		}

        public TrackSectionsFile(string fileName)
        {
            using (STFReader stf = new STFReader(fileName, false))
            {
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tracksections", ()=>{ 
                        if (TrackSections == null)
                            TrackSections = new TrackSections(stf);
                        else
                            TrackSections.AddRouteStandardTrackSections(stf);}),
                    new STFReader.TokenProcessor("trackshapes", ()=>{ 
                        if (TrackShapes == null) 
                            TrackShapes = new TrackShapes(stf);
                        else
                            TrackShapes.AddRouteTrackShapes(stf);}),
                });
                //TODO This should be changed to STFException.TraceError() with defaults values created
                if (TrackSections == null) throw new STFException(stf, "Missing TrackSections");
                if (TrackShapes == null) throw new STFException(stf, "Missing TrackShapes");
            }
        }
	}
}
