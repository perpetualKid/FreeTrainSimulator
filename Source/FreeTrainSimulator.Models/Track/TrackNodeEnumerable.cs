using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace FreeTrainSimulator.Models.Track
{
    public readonly struct TrackNodeEnumerable<T> : IEnumerable<T>, IEquatable<TrackNodeEnumerable<T>> where T : TrackNodeBase
    {
        private readonly ImmutableArray<TrackNodeBase> nodes;
        private readonly ImmutableArray<int> indices;

        public TrackNodeEnumerable(ImmutableArray<TrackNodeBase> nodes, ImmutableArray<int> indices)
        {
            this.nodes = nodes;
            this.indices = indices;
        }

        public int Count => indices.Length;

        /// <summary>
        /// Returns the track node at the specified index in the enumerable. 
        /// Note that this is not necessarily the track node at the same index in the underlying track node array
        /// as the enumerable may be a filtered view of the track nodes.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public T this[int i] => (T)nodes[i];
        //        public T this[int i] => (T)nodes[indices[i]];

        /// <summary>Duck-typed overload: used by <see langword="foreach"/> — returns the struct enumerator directly, avoiding boxing.</summary>
        public TrackNodeEnumerator<T> GetEnumerator() => new(nodes, indices);
        /// <summary>Interface implementation: boxes the struct enumerator for LINQ and other <see cref="IEnumerable{T}"/> consumers.</summary>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new TrackNodeEnumerator<T>(nodes, indices);
        IEnumerator IEnumerable.GetEnumerator() => new TrackNodeEnumerator<T>(nodes, indices);

        public bool Equals(TrackNodeEnumerable<T> other) => nodes == other.nodes && indices == other.indices;
        public override bool Equals(object obj) => obj is TrackNodeEnumerable<T> other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(nodes, indices);

        public static bool operator ==(TrackNodeEnumerable<T> left, TrackNodeEnumerable<T> right) => left.Equals(right);

        public static bool operator !=(TrackNodeEnumerable<T> left, TrackNodeEnumerable<T> right) =>!left.Equals(right);
    }

    public struct TrackNodeEnumerator<T> : IEnumerator<T>, IEquatable<TrackNodeEnumerator<T>> where T : TrackNodeBase
    {
        private readonly ImmutableArray<TrackNodeBase> nodes;
        private readonly ImmutableArray<int> indices;
        private int position;

        internal TrackNodeEnumerator(ImmutableArray<TrackNodeBase> nodes, ImmutableArray<int> indices)
        {
            this.nodes = nodes;
            this.indices = indices;
            position = -1;
        }

        public readonly T Current => (T)nodes[indices[position]];
        readonly object IEnumerator.Current => Current;

        public bool MoveNext() => ++position < indices.Length;
        public void Reset() => position = -1;
        public readonly void Dispose() { }

        public readonly bool Equals(TrackNodeEnumerator<T> other) => nodes == other.nodes && indices == other.indices && position == other.position;
        public override readonly bool Equals(object obj) => obj is TrackNodeEnumerator<T> other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(nodes, indices, position);

        public static bool operator ==(TrackNodeEnumerator<T> left, TrackNodeEnumerator<T> right) => left.Equals(right);

        public static bool operator !=(TrackNodeEnumerator<T> left, TrackNodeEnumerator<T> right) => !left.Equals(right);
    }

}
