// COPYRIGHT 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

using Orts.Formats.Msts.Entities;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Files
{
    /// <summary>
    /// RDBFile is a representation of the .rdb file, that contains the road data base.
    /// The database contains the same kind of objects as TDBFile, apart from a few road-specific items.
    /// </summary>
    public class RoadDatabaseFile
	{
        /// <summary>
        /// Contains the Database with all the road tracks.
        /// Warning, the first RoadTrackDB entry is always null.
        /// </summary>
        public RoadTrackDB RoadTrackDB { get; private set; }

        /// <summary>
        /// Constructor from file
        /// </summary>
        /// <param name="filenamewithpath">Full file name of the .rdb file</param>
		public RoadDatabaseFile(string filenamewithpath)
		{
			using (STFReader stf = new STFReader(filenamewithpath, false))
				stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("trackdb", ()=>{ RoadTrackDB = new RoadTrackDB(stf); }),
                });
		}
	}

}
