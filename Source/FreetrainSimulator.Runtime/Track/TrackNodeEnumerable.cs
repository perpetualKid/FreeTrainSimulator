using System.Collections.Immutable;

using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Runtime.Track
{
    public readonly struct TrackNodeEnumerable<T> where T : TrackNodeBase
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

        public Enumerator GetEnumerator() => new(nodes, indices);

        public struct Enumerator
        {
            private readonly ImmutableArray<TrackNodeBase> _nodes;
            private readonly ImmutableArray<int> _indices;
            private int position;

            internal Enumerator(ImmutableArray<TrackNodeBase> nodes, ImmutableArray<int> indices)
            {
                _nodes = nodes;
                _indices = indices;
                position = -1;
            }

            public readonly T Current => (T)_nodes[_indices[position]];

            public bool MoveNext() => ++position < _indices.Length;
        }
    }
}
