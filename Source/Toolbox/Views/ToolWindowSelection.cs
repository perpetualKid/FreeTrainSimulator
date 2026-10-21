using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Extracts explicit selection intent for tool windows that use SelectionChanged handlers.
    /// The route menu instead binds immutable session entries through its guarded viewmodel setters.
    /// This helper makes no assumptions about docking or the cause of binding/identity failures.
    /// </summary>
    internal static class ToolWindowSelection
    {
        /// <summary>
        /// Extracts the newly selected item of type <typeparamref name="T"/> from a
        /// <see cref="SelectionChangedEventArgs"/>. Returns <see langword="false"/> when the selection was
        /// cleared or the added item is not a <typeparamref name="T"/>, so callers can ignore the change.
        /// </summary>
        public static bool TryGetAddedItem<T>(SelectionChangedEventArgs e, out T item) where T : class
        {
            if (e != null && e.AddedItems.Count > 0 && e.AddedItems[0] is T added)
            {
                item = added;
                return true;
            }

            item = null;
            return false;
        }
    }
}
