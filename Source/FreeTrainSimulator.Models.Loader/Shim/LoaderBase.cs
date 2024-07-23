using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent;

using MemoryPack;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public abstract class LoaderBase
    {
        private const string SaveStateExtension = ".save";
        private static readonly string CurrentVersion = VersionInfo.Version;

        public static async ValueTask<T> FromFile<T>(string fileName, CancellationToken cancellationToken) where T : ModelBase
        {
            fileName += SaveStateExtension;
            if (File.Exists(fileName))
            {
                using (FileStream saveFile = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    T model = await MemoryPackSerializer.DeserializeAsync<T>(saveFile, null, cancellationToken).ConfigureAwait(false);
                    
                    if (VersionInfo.Compare(model.Version) > 0)
                        model = null;
                    return model;
                }
            }
            return null;
        }

        public static async ValueTask ToFile<T>(string fileName, T model, CancellationToken cancellationToken) where T : ModelBase
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            fileName += SaveStateExtension;
            model.Version = CurrentVersion;
            using (FileStream saveFile = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                await MemoryPackSerializer.SerializeAsync(saveFile, model, null, cancellationToken).ConfigureAwait(false);
                await saveFile.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
