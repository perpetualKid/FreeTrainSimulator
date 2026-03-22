using System;
using System.Collections.Immutable;

namespace FreeTrainSimulator.Models.Track
{
    public readonly struct TrackNodeEnumerable<T> : IEquatable<TrackNodeEnumerable<T>> where T : TrackNodeBase
    {
        private readonly ImmutableArray<TrackNodeBase> nodes;
        private readonly ImmutableArray<int> indices;

        public TrackNodeEnumerable(ImmutableArray<TrackNodeBase> nodes, ImmutableArray<int> indices)
        {
            this.nodes = nodes;
            this.indices = indices;
        }

        public int Count => indices.Length;

        public T this[int i] => (T)nodes[indices[i]];

        public TrackNodeEnumerator<T> GetEnumerator() => new(nodes, indices);

        public bool Equals(TrackNodeEnumerable<T> other) => nodes == other.nodes && indices == other.indices;

        public override bool Equals(object obj) => obj is TrackNodeEnumerable<T> other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(nodes, indices);

        public static bool operator ==(TrackNodeEnumerable<T> left, TrackNodeEnumerable<T> right) => left.Equals(right);

        public static bool operator !=(TrackNodeEnumerable<T> left, TrackNodeEnumerable<T> right) =>!left.Equals(right);
    }

    public struct TrackNodeEnumerator<T> : IEquatable<TrackNodeEnumerator<T>> where T : TrackNodeBase
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

        public bool MoveNext() => ++position < indices.Length;

        public readonly bool Equals(TrackNodeEnumerator<T> other) => nodes == other.nodes && indices == other.indices && position == other.position;

        public override readonly bool Equals(object obj) => obj is TrackNodeEnumerator<T> other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(nodes, indices, position);

        public static bool operator ==(TrackNodeEnumerator<T> left, TrackNodeEnumerator<T> right) => left.Equals(right);

        public static bool operator !=(TrackNodeEnumerator<T> left, TrackNodeEnumerator<T> right) => !left.Equals(right);
    }

}
