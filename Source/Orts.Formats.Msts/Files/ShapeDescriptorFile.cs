// COPYRIGHT 2009, 2010, 2012 by the Open Rails project.
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

namespace Orts.Formats.Msts.Files
{
    public class ShapeDescriptorFile
    {
        public ShapeDescriptor Shape { get; private set; }

        public ShapeDescriptorFile()  // use for files with no SD file
        {
            Shape = new ShapeDescriptor();
        }

        public ShapeDescriptorFile(string fileName)
        {
            using (STFReader stf = new STFReader(fileName, false))
            {
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("shape", ()=>{ Shape = new ShapeDescriptor(stf); }),
                });
                //TODO This should be changed to STFException.TraceError() with defaults values created
                if (Shape == null)
                    throw new STFException(stf, "Missing shape statement");
            }
        }

    }
}
