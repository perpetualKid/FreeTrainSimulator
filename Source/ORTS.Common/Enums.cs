// COPYRIGHT 2009, 2011 by the Open Rails project.
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ORTS.Common
{
    public static class EnumExtension
    {
        private static class EnumCache<T> where T : struct
        {
            internal static readonly IList<string> Names;
            internal static readonly IList<T> Values;
            internal static readonly Dictionary<T, string> ValueToDescriptionMap;

            static EnumCache()
            {
                Values = new ReadOnlyCollection<T>((T[])Enum.GetValues(typeof(T)));
                Names = new ReadOnlyCollection<string>(Enum.GetNames(typeof(T)));
                ValueToDescriptionMap = new Dictionary<T, string>();

                foreach (T value in (T[])Enum.GetValues(typeof(T)))
                {
                    ValueToDescriptionMap[value] = GetDescription(value);
                }
            }

            private static string GetDescription(T value)
            {
                FieldInfo field = typeof(T).GetField(value.ToString());
                return field.GetCustomAttributes(typeof(DescriptionAttribute), false)
                            .Cast<DescriptionAttribute>()
                            .Select(x => x.Description)
                            .FirstOrDefault();
            }
        }

        public static string GetDescription<T>(this T item) where T : struct
        {
            if (EnumCache<T>.ValueToDescriptionMap.TryGetValue(item, out string description))
            {
                return description;
            }
            throw new ArgumentOutOfRangeException("item");
        }

        public static IList<string> GetNames<T>() where T : struct
        {
            return EnumCache<T>.Names;
        }

        public static IList<T> GetValues<T>() where T : struct
        {
            return EnumCache<T>.Values;
        }

        public static int GetLength<T>() where T: struct
        {
            return EnumCache<T>.Values.Count;
        }
    }

    public enum Direction
    {
        [GetParticularString("Reverser", "Forward")] Forward,
        [GetParticularString("Reverser", "Reverse")] Reverse,
        [GetParticularString("Reverser", "N")] N
    }

    public class DirectionControl
    {
        public static Direction Flip(Direction direction)
        {
            //return direction == Direction.Forward ? Direction.Reverse : Direction.Forward;
            if (direction == Direction.N)
                return Direction.N;
            if (direction == Direction.Forward)
                return Direction.Reverse;
            else
                return Direction.Forward;
        }
    }
}
