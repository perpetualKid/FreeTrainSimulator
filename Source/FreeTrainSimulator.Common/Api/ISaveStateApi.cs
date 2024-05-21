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
    }

    public interface ICollectionSaveStateApi<TSaveState, TRuntime> where TSaveState : SaveStateBase where TRuntime : ISaveStateApi<TSaveState>, new()
    {
        public async ValueTask<Collection<TSaveState>> SnapshotCollection(IEnumerable<TRuntime> source) 
        {
            return new Collection<TSaveState>(await Task.WhenAll(source.Select(async item =>
            {
                return await ValueTask.FromResult(await item.Snapshot().ConfigureAwait(false));
            })).ConfigureAwait(false));
        }

        public async ValueTask RestoreCollection(ICollection<TSaveState> saveStates, ICollection<TRuntime> target, bool append = false)
        {
            ArgumentNullException.ThrowIfNull(saveStates, nameof(saveStates));
            ArgumentNullException.ThrowIfNull(target, nameof(target));

            if (!append)
                target.Clear();

            await Task.WhenAll(saveStates.Select(async saveState => 
            {
                TRuntime runtimeTarget = new TRuntime();
                InitializeSaveStateSource(runtimeTarget);
                await runtimeTarget.Restore(saveState).ConfigureAwait(false);
                target.Add(runtimeTarget);
            })).ConfigureAwait(false);
        }

        public virtual void InitializeSaveStateSource(TRuntime target)
        { }
    }
}
