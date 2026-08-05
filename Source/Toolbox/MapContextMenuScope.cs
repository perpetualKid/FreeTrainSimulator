namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Scope a map context menu action operates on.
    /// </summary>
    internal enum MapContextMenuScope
    {
        /// <summary>Action targets the path node under the pointer.</summary>
        Node,

        /// <summary>Action targets the path span under the pointer.</summary>
        Span,

        /// <summary>Action targets the current path or the editor as a whole.</summary>
        Path,
    }
}
