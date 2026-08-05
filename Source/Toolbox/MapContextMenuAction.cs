namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Node-related editing actions offered by the map surface context menu.
    /// </summary>
    internal enum MapContextMenuAction
    {
        MoveNode,
        CancelMoveNode,
        AddViaPoint,
        RemoveViaPoint,
        SetWaitPoint,
        ClearWaitPoint,
        SetReversalPoint,
        ClearReversalPoint,
        RepairNode,
        RemoveRestOfPath,
    }
}
