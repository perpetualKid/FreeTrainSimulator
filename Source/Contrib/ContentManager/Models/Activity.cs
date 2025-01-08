// COPYRIGHT 2014 by the Open Rails project.
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
using System.Diagnostics;
using System.Linq;

using Orts.Formats.Msts.Files;
using Orts.Formats.OpenRails.Parsers;

namespace Orts.ContentManager.Models
{
    public class Activity
    {
        public string Name { get; }
        public string Description { get; }
        public string Briefing { get; }

        public IEnumerable<string> PlayerServices { get; }
        public IEnumerable<string> Services { get; }

        public Activity(ContentBase content)
        {
            Debug.Assert(content?.Type == ContentType.Activity);
            if (System.IO.Path.GetExtension(content.PathName).Equals(".act", StringComparison.OrdinalIgnoreCase))
            {
                ActivityFile file = new ActivityFile(content.PathName);
                Name = file.Activity.Header.Name;
                Description = file.Activity.Header.Description;
                Briefing = file.Activity.Header.Briefing;
                PlayerServices = new[] { $"Player|{file.Activity.PlayerServices.Name}" };
                if (file.Activity.Traffic != null)
                    Services = file.Activity.Traffic.Services.Select((service, index) =>
                        $"AI|{service.Name}|{file.Activity.Traffic.Name}|{index}"
                    );
                else
                    Services = Array.Empty<string>();
            }
            else if (System.IO.Path.GetExtension(content.PathName).Equals(".timetable_or", StringComparison.OrdinalIgnoreCase)
                || System.IO.Path.GetExtension(content.PathName).Equals(".timetable-or", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Make common timetable parser.
                TimetableReader file = new TimetableReader(content.PathName);
                Name = content.Name;

                List<string> services = new List<string>();
                for (int column = 0; column < file.Strings[0].Length; column++)
                {
                    if (string.IsNullOrEmpty(file.Strings[0][column]) || file.Strings[0][column][0] == '#')
                        continue;

                    services.Add(file.Strings[0][column]);
                }
                PlayerServices = services;
                Services = Array.Empty<string>();
            }
        }
    }
}
