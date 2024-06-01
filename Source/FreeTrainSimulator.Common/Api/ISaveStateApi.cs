using System;
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
        public static async ValueTask<Collection<TSaveState>> SnapshotCollection<TSaveState, TRuntime>(this IEnumerable<TRuntime> source)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            return new Collection<TSaveState>(await Task.WhenAll(source.Select(async item =>
            {
                return await ValueTask.FromResult(await item.Snapshot().ConfigureAwait(false));
            })).ConfigureAwait(false));
        }

        public static async ValueTask RestoreCollectionCreateNewInstances<TSaveState, TRuntime, TActivator>(this ICollection<TRuntime> target, ICollection<TSaveState> saveStates, TActivator activator = default)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
            where TActivator : ISaveStateRestoreApi<TSaveState, TRuntime>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            await Task.WhenAll(saveStates.Select(async saveState =>
            {
                TRuntime runtimeTarget = activator.CreateRuntimeTarget(saveState);
                await runtimeTarget.Restore(saveState).ConfigureAwait(false);
                target.Add(runtimeTarget);
            })).ConfigureAwait(false);
        }

        public static async ValueTask RestoreCollectionOnExistingInstances<TSaveState, TRuntime>(this IEnumerable<TRuntime> target, IEnumerable<TSaveState> saveStates)
            where TSaveState : SaveStateBase
            where TRuntime : ISaveStateApi<TSaveState>
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            foreach ((TRuntime runtimeTarget, TSaveState saveState) in target.Zip(saveStates))
            {
                await runtimeTarget.Restore(saveState).ConfigureAwait(false);
            }
        }

    }
}
