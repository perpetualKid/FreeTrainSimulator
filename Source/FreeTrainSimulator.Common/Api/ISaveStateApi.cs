using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTrainSimulator.Common.Api
{
    public interface ISaveStateApi<T> where T : SaveStateBase
    {
        public ValueTask<T> Snapshot();
        public ValueTask Restore(T saveState);
        public virtual T CreateRuntimeTarget() => default(T);
    }

    public interface ISaveStateRestoreApi<TSaveState, TRuntime>
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
    {
        public virtual TRuntime CreateRuntimeTarget(TSaveState saveState) => default(TRuntime);
    }

    public static class SaveStateCollectionExtension
    {
        private static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> source)
        {
            foreach (T item in source)
                collection.Add(item);
        }

        #region Collection
        public static async ValueTask<Collection<TSaveState>> SnapshotCollection<TSaveState, TRuntime>(this IEnumerable<TRuntime> source)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));

            return new Collection<TSaveState>(await Task.WhenAll(source.Select(async item =>
            {
                return await ValueTask.FromResult(item == null ? null : await item.Snapshot().ConfigureAwait(false)).ConfigureAwait(false);
            })).ConfigureAwait(false));
        }

        public static async ValueTask RestoreCollectionCreateNewItems<TSaveState, TRuntime>(this ICollection<TRuntime> target, ICollection<TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>, new()
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            TRuntime[] results = await Task.WhenAll(saveStates.Select(async saveState =>
            {
                TRuntime targetItem = default;
                if (saveState != null)
                {
                    targetItem = new TRuntime();
                    await targetItem.Restore(saveState).ConfigureAwait(false);
                }
                return targetItem;
            })).ConfigureAwait(false);

            target.AddAll(results);
        }

        public static async ValueTask RestoreCollectionCreateNewItems<TSaveState, TRuntime, TActivator>(this ICollection<TRuntime> target, ICollection<TSaveState> saveStates, TActivator activator)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
            where TActivator : ISaveStateRestoreApi<TSaveState, TRuntime>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));
            ArgumentNullException.ThrowIfNull(activator, nameof(activator));

            TRuntime[] results = await Task.WhenAll(saveStates.Select(async saveState =>
            {
                TRuntime targetItem = default;
                if (saveState != null)
                {
                    targetItem = activator.CreateRuntimeTarget(saveState);
                    if (null != targetItem)
                        await targetItem.Restore(saveState).ConfigureAwait(false);
                }
                return targetItem;
            })).ConfigureAwait(false);

            target.AddAll(results);
        }

        public static async ValueTask RestoreCollectionOnExistingInstances<TSaveState, TRuntime>(this IEnumerable<TRuntime> target, IEnumerable<TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            await Parallel.ForEachAsync(target.Zip(saveStates), async (targetInstance, cancellationToken) => {
                if (null != targetInstance.First && null != targetInstance.Second)
                    await targetInstance.First.Restore(targetInstance.Second).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        #endregion

        #region Dictionary
        public static async ValueTask<Dictionary<TKey, TSaveState>> SnapshotDictionary<TSaveState, TRuntime, TKey>(this IDictionary<TKey, TRuntime> source)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));

            ConcurrentDictionary<TKey, TSaveState> saveStates = new ConcurrentDictionary<TKey, TSaveState>();
            await Parallel.ForEachAsync(source, async (sourceItem, cancellationToken) =>
            {
                _ = saveStates.TryAdd(sourceItem.Key, sourceItem.Value == null ? null : await sourceItem.Value.Snapshot().ConfigureAwait(false));
            }).ConfigureAwait(false);

            return saveStates.ToDictionary();
        }

        public static async ValueTask RestoreDictionaryCreateNewInstances<TSaveState, TRuntime, TKey>(this IDictionary<TKey, TRuntime> target, IDictionary<TKey, TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>, new()
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            ConcurrentDictionary<TKey, TRuntime> restoreItems = new ConcurrentDictionary<TKey, TRuntime>();
            await Parallel.ForEachAsync(saveStates, async (saveState, cancellationToken) =>
            {
                TRuntime targetInstance = default;
                if (saveState.Value != null)
                {
                    targetInstance = new TRuntime();
                    await targetInstance.Restore(saveState.Value).ConfigureAwait(false);
                }
                _ = restoreItems.TryAdd(saveState.Key, targetInstance);
            }).ConfigureAwait(false);

            foreach (KeyValuePair<TKey, TRuntime> item in restoreItems)
                _ = target.TryAdd(item.Key, item.Value);
        }

        public static async ValueTask RestoreDictionaryCreateNewInstances<TSaveState, TRuntime, TActivator, TKey>(this IDictionary<TKey, TRuntime> target, IDictionary<TKey, TSaveState> saveStates, TActivator activator)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
            where TActivator : ISaveStateRestoreApi<TSaveState, TRuntime>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));
            ArgumentNullException.ThrowIfNull(activator, nameof(activator));

            ConcurrentDictionary<TKey, TRuntime> restoreItems = new ConcurrentDictionary<TKey, TRuntime>();
            await Parallel.ForEachAsync(saveStates, async (saveState, cancellationToken) =>
            {
                TRuntime targetInstance = default(TRuntime);
                if (saveState.Value != null)
                {
                    targetInstance = activator.CreateRuntimeTarget(saveState.Value);
                    await targetInstance.Restore(saveState.Value).ConfigureAwait(false);
                }
                _ = restoreItems.TryAdd(saveState.Key, targetInstance);
            }).ConfigureAwait(false);

            foreach (KeyValuePair<TKey, TRuntime> item in restoreItems)
                _ = target.TryAdd(item.Key, item.Value);
        }

        public static async ValueTask RestoreDictionaryOnExistingItems<TSaveState, TRuntime, TKey>(this IDictionary<TKey, TRuntime> target, IDictionary<TKey, TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            await Parallel.ForEachAsync(target, async (targetItem, cancellationToken) =>
            {
                await targetItem.Value.Restore(saveStates[targetItem.Key]).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        #endregion

        #region Dictionary of Collections
        public static ValueTask<Dictionary<TKey, Collection<TRuntime>>> SnapshotListDictionary<TRuntime, TKey>(this IDictionary<TKey, List<TRuntime>> source)
            where TRuntime : struct
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));

            ConcurrentDictionary<TKey, Collection<TRuntime>> saveStates = new ConcurrentDictionary<TKey, Collection<TRuntime>>();
            Parallel.ForEach(source, (sourceItem, cancellationToken) =>
            {
                _ = saveStates.TryAdd(sourceItem.Key, sourceItem.Value == null ? null : new Collection<TRuntime>(sourceItem.Value));
            });

            return ValueTask.FromResult(saveStates.ToDictionary());
        }

        public static ValueTask RestoreListDictionary<TRuntime, TKey>(this IDictionary<TKey, List<TRuntime>> target, IDictionary<TKey, Collection<TRuntime>> saveStates)
            where TRuntime : struct
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            ConcurrentDictionary<TKey, List<TRuntime>> restoreItems = new ConcurrentDictionary<TKey, List<TRuntime>>();
            Parallel.ForEach(saveStates, (saveState, cancellationToken) =>
            {
                List<TRuntime> targetItem = new List<TRuntime>(saveState.Value);
            });

            target.AddAll(restoreItems);
            return ValueTask.CompletedTask;
        }


        public static async ValueTask<Dictionary<TKey, Collection<TSaveState>>> SnapshotListDictionary<TSaveState, TRuntime, TKey>(this IDictionary<TKey, List<TRuntime>> source)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));

            ConcurrentDictionary<TKey, Collection<TSaveState>> saveStates = new ConcurrentDictionary<TKey, Collection<TSaveState>>();
            await Parallel.ForEachAsync(source, async (sourceItem, cancellationToken) =>
            {
                _ = saveStates.TryAdd(sourceItem.Key, sourceItem.Value == null ? null : await sourceItem.Value.SnapshotCollection<TSaveState, TRuntime>().ConfigureAwait(false));
            }).ConfigureAwait(false);

            return saveStates.ToDictionary();
            #endregion
        }

        public static async ValueTask RestoreListDictionaryCreateNewItem<TSaveState, TRuntime, TKey>(this IDictionary<TKey, List<TRuntime>> target, IDictionary<TKey, Collection<TSaveState>> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>, new()
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            ConcurrentDictionary<TKey, List<TRuntime>> restoreItems = new ConcurrentDictionary<TKey, List<TRuntime>>();
            await Parallel.ForEachAsync(saveStates, async (saveState, cancellationToken) =>
            {
                List<TRuntime> targetItem = default;
                if (saveState.Value != null)
                {
                    targetItem = new List<TRuntime>();
                    await targetItem.RestoreCollectionCreateNewItems(saveState.Value).ConfigureAwait(false);
                }
                _ = restoreItems.TryAdd(saveState.Key, targetItem);
            }).ConfigureAwait(false);

            target.AddAll(restoreItems);
        }
    }
}
