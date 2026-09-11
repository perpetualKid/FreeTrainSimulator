using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Shared helper for the selection-binding pattern used by the dockable Toolbox tool windows.
    /// <para>
    /// PROBLEM: These views are hosted inside AvalonDock panes. When a pane is re-parented (docked, floated,
    /// activated, or its content otherwise re-hosted), WPF cycles the view's DataContext (null -&gt; view model
    /// -&gt; null). A <see cref="Selector"/> with a <c>TwoWay</c> <see cref="Selector.SelectedItem"/> binding can
    /// silently lose its target-to-source write-back after that cycle, so the second and subsequent user picks
    /// stop updating the bound view-model property. The model types are also records with value-based equality,
    /// which compounds the issue: rebuilding an <c>ObservableCollection</c> (Clear + Add) while a
    /// <see cref="Selector.SelectedItem"/> is held drives WPF's selection tracker to insert a duplicate key and
    /// throw <see cref="System.ArgumentException"/>.
    /// </para>
    /// <para>
    /// PATTERN (proven in <see cref="RouteToolView"/> / <c>ToolboxMenuViewModel</c>):
    /// <list type="number">
    /// <item>Bind <see cref="Selector.SelectedItem"/> <c>OneWay</c> (source -&gt; target only).</item>
    /// <item>Handle <see cref="Selector.SelectionChanged"/> in code-behind and forward the user's pick to a
    /// dedicated <c>UserSelectX</c> method on the view model via <see cref="TryGetAddedItem{T}"/>.
    /// <see cref="Selector.SelectionChanged"/> fires reliably even after the DataContext cycles, so it is the
    /// source of truth for user intent.</item>
    /// <item>The view model is the single authoritative point that assigns the selection back to the bound
    /// property (raising <c>PropertyChanged</c>); its setter is private so only the view model drives the
    /// displayed value.</item>
    /// <item>When rebuilding the bound collection, clear the selection first, replace the items, then restore
    /// the selection, so the <see cref="Selector"/> never tracks an item across a Clear + Add.</item>
    /// </list>
    /// </para>
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
