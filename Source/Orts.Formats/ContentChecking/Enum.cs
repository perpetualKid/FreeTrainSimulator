namespace Orts.Formats.ContentChecking
{
    /// <summary>Selects the existing legacy-content traversal. Referenced and all-related checks remain partial.</summary>
    public enum AdditionType
    {
        /// <summary>Check only the requested files.</summary>
        None,
        /// <summary>Also check files that require context supplied by a requested file.</summary>
        Dependent,
        /// <summary>Also check directly referenced files, including dependent files.</summary>
        Referenced,
        /// <summary>Also check the route's additional related files.</summary>
        All,
    }
}