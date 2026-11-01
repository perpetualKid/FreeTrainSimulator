namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>A saved native path that can be selected independently of the active editor.</summary>
    internal sealed record TrainPathCompositionSourceRow
    {
        /// <summary>Saved path identity within the owning route.</summary>
        public string Id { get; init; }

        /// <summary>Display name of the saved path.</summary>
        public string Name { get; init; }

        /// <summary>Route-content generation that owns this source row.</summary>
        public long RouteContentVersion { get; init; }

        public TrainPathCompositionSourceRow(string id, string name, long routeContentVersion)
        {
            Id = id;
            Name = name;
            RouteContentVersion = routeContentVersion;
        }
    }
}
