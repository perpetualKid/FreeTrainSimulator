﻿using System.Collections.Frozen;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record PathModel: PathModelCore
    {
        public FrozenDictionary<int, PathNode> PathNodes { get; init; } // order of nodes is import on import, so using index as key since FrozenSet is not indexed
    }
}
