using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common
{
    /// <summary>
    /// Provides a dual-mode service accessor: game-scoped via <see cref="Game.Services"/> when a <see cref="Game"/>
    /// is available, falling back to a process-wide singleton when no game is present.
    /// <para>
    /// Use <see cref="Set"/> during initialization and <see cref="Get"/> (or <see cref="Instance"/>) for access.
    /// </para>
    /// </summary>
    public static class GameService<T> where T : class
    {
#pragma warning disable CA1000 // Do not declare static members on generic types
        /// <summary>
        /// The process-wide fallback singleton. Always set by <see cref="Set"/>, regardless of whether a
        /// <see cref="Game"/> was supplied.
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// Returns the game-scoped instance when <paramref name="game"/> has a registered <typeparamref name="T"/>,
        /// otherwise returns the process-wide <see cref="Instance"/>.
        /// </summary>
        public static T Get(Game game) => game?.Services.GetService<T>() ?? Instance;

        /// <summary>
        /// Registers <paramref name="value"/> as the current service instance.
        /// Always updates the process-wide <see cref="Instance"/> for non-game contexts.
        /// When <paramref name="game"/> is not <see langword="null"/>, also replaces any existing
        /// registration in the game's service container.
        /// </summary>
        /// <returns><paramref name="value"/>, for convenient chaining.</returns>
        public static T Set(Game game, T value)
        {
            Instance = value;
            if (game != null)
            {
                game.Services.RemoveService(typeof(T));
                game.Services.AddService(value);
            }
            return value;
        }
#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
