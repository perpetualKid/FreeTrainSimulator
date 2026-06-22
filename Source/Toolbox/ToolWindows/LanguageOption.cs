namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// A selectable UI language for the settings tool window. <see cref="Code"/> is the culture code persisted
    /// to user settings (empty for the system default); <see cref="DisplayName"/> is the human-readable name
    /// shown in the language picker.
    /// </summary>
    internal sealed record LanguageOption
    {
        public static LanguageOption SystemDefault { get; } = new LanguageOption
        {
            Code = string.Empty,
            DisplayName = "System default"
        };

        public string Code { get; init; }
        public string DisplayName { get; init; }
    }
}
