// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.Collections.Generic;

namespace Orts.Formats.Msts
{

    /// <summary>
    /// Represents the hiearchical structure of the SMS File
    /// </summary>
    public class SoundManagmentFile
	{
        public List<ScalabiltyGroup> ScalabiltyGroups { get; } = new List<ScalabiltyGroup>();

        public SoundManagmentFile( string filePath )
		{
            ReadFile(filePath);  
        }

        private void ReadFile(string filePath)
        {
            using (STFReader stf = new STFReader(filePath, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tr_sms", ()=>{ ParseTrackSms(stf); }),
                });
        }

        private void ParseTrackSms(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("scalabiltygroup", ()=>{ ScalabiltyGroups.Add(new ScalabiltyGroup(stf)); }),
            });
        }
    }
} 
