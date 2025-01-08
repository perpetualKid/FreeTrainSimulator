// COPYRIGHT 2012 by the Open Rails project.
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace FreeTrainSimulator.Menu
{
    public class SortableBindingList<T> : BindingList<T>
    {
        public SortableBindingList()
        {
        }

        public SortableBindingList(IList<T> list)
            : base(list)
        {
        }

        protected override bool SupportsSortingCore => true;

        private ListSortDirection sortDirection = ListSortDirection.Ascending;
        private PropertyDescriptor sortProperty;
        private bool sorted;

        protected override ListSortDirection SortDirectionCore => sortDirection;

        protected override PropertyDescriptor SortPropertyCore => sortProperty;

        protected override bool IsSortedCore => sorted;

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            ArgumentNullException.ThrowIfNull(prop);

            if (PropertyComparer.IsAllowable(prop))
            {
                ((List<T>)Items).Sort(new PropertyComparer(prop, direction));
                sortDirection = direction;
                sortProperty = prop;
                sorted = true;
            }
        }

        protected override void RemoveSortCore()
        {
            sortProperty = null;
            sorted = false;
        }

        private sealed class PropertyComparer : Comparer<T>
        {
            private readonly PropertyDescriptor prop;
            private readonly ListSortDirection direction;
            private readonly IComparer comparer;

            public PropertyComparer(PropertyDescriptor prop, ListSortDirection direction)
            {
                this.prop = prop;
                this.direction = direction;
                comparer = (IComparer)typeof(Comparer<>).MakeGenericType(prop.PropertyType).GetProperty("Default").GetValue(null, null);
            }

            public override int Compare(T x, T y)
            {
                if (direction == ListSortDirection.Ascending)
                    return comparer.Compare(prop.GetValue(x), prop.GetValue(y));
                return comparer.Compare(prop.GetValue(y), prop.GetValue(x));
            }

            public static bool IsAllowable(PropertyDescriptor prop)
            {
                return prop.PropertyType.GetInterface("IComparable") != null;
            }
        }
    }
}
