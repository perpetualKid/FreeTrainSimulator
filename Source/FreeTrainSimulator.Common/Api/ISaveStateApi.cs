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

        public static async ValueTask RestoreCollectionCreateNewInstances<TSaveState, TRuntime>(this ICollection<TRuntime> target, ICollection<TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>, new()
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            await Task.WhenAll(saveStates.Select(async saveState =>
            {
                TRuntime targetInstance = default;
                if (saveState != null)
                {
                    targetInstance = new TRuntime();
                    await targetInstance.Restore(saveState).ConfigureAwait(false);
                }
                target.Add(targetInstance);
            })).ConfigureAwait(false);
        }

        public static async ValueTask RestoreCollectionCreateNewInstances<TSaveState, TRuntime, TActivator>(this ICollection<TRuntime> target, ICollection<TSaveState> saveStates, TActivator activator)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
            where TActivator : ISaveStateRestoreApi<TSaveState, TRuntime>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));
            ArgumentNullException.ThrowIfNull(activator, nameof(activator));

            await Task.WhenAll(saveStates.Select(async saveState =>
            {
                TRuntime targetInstance = default;
                if (saveState != null)
                {
                    targetInstance = activator.CreateRuntimeTarget(saveState);
                    if (null != targetInstance)
                        await targetInstance.Restore(saveState).ConfigureAwait(false);
                }
                if (null != targetInstance)
                    target.Add(targetInstance);
            })).ConfigureAwait(false);
        }

        public static async ValueTask RestoreCollectionOnExistingInstances<TSaveState, TRuntime>(this IEnumerable<TRuntime> target, IEnumerable<TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            foreach ((TRuntime targetInstance, TSaveState saveState) in target.Zip(saveStates))
            {
                if (null != targetInstance && null != saveState)
                    await targetInstance.Restore(saveState).ConfigureAwait(false);
            }
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

            foreach(KeyValuePair<TKey, TRuntime> item in restoreItems)
                _ = target.TryAdd(item.Key, item.Value);
        }

        public static async ValueTask RestoreDictionaryOnExistingInstances<TSaveState, TRuntime, TKey>(this IDictionary<TKey, TRuntime> target, IDictionary<TKey, TSaveState> saveStates)
            where TSaveState: SaveStateBase
            where TRuntime: ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            await Parallel.ForEachAsync(target, async (targetInstance, cancellationToken) =>
            {
                await targetInstance.Value.Restore(saveStates[targetInstance.Key]).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        #endregion
    }
}
