namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Persisted validation state of a train path against the route track network. Decoupled from the runtime
    /// resolver's diagnostic severity so model persistence owns its own vocabulary. New states may be appended
    /// (for example a future <c>Stale</c> state after a route changes); <see cref="NotValidated"/> must remain
    /// the default (0) value so path files written before this field existed read back as "not yet validated".
    /// </summary>
    public enum PathValidationState
    {
        /// <summary>The path has not been validated against the track yet.</summary>
        NotValidated = 0,

        /// <summary>The path resolves without error or fatal diagnostics (warnings are still considered valid).</summary>
        Valid,

        /// <summary>The path has error or fatal diagnostics and does not resolve against the current track.</summary>
        Invalid,
    }
}
