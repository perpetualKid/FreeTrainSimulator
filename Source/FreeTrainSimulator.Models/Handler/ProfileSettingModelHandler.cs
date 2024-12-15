using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Settings;

using MemoryPack;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class ProfileSettingModelHandler<TSettingsModel> where TSettingsModel : ProfileSettingsModelBase
    {
        public const string SaveStateExtension = FileNameExtensions.SaveFile;

        //public static void SetValueByName(TSettingsModel instance, string propertyName, object value)
        //{
        //    IOrderedEnumerable<PropertyInfo> properties = typeof(TSettingsModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name);
        //    properties.Where(p => p.Name == propertyName).FirstOrDefault()?.SetValue(instance, value);
        //}

        internal static async Task<TSettingsModel> FromFile(TSettingsModel instance, CancellationToken cancellationToken)
        {
            string targetFileName = ModelFileResolver<TSettingsModel>.FilePath(instance) + SaveStateExtension;

            if (File.Exists(targetFileName))
            {
                try
                {
                    using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                    {
                        int bufferSize = 65536; //assume settings are no larger than 64k
                        if (saveFile.Length > 0 && saveFile.Length < short.MaxValue)
                        {
                            bufferSize = (int)saveFile.Length;
                        }

                        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                        try
                        {
                            _ = await saveFile.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                            MemoryPackSerializer.Deserialize(buffer, ref instance, null);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                }
                catch (MemoryPackSerializationException) { }
            }
            return instance;
        }

        internal static async Task<TSettingsModel> ToFile(TSettingsModel model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            string targetFileName = ModelFileResolver<TSettingsModel>.FilePath(model) + SaveStateExtension;

            model.RefreshModel();

            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(targetFileName));

                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Create, FileAccess.Write))
                {
                    await MemoryPackSerializer.SerializeAsync(saveFile, model, null, cancellationToken).ConfigureAwait(false);
                    await saveFile.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                throw;
            }
            return model;
        }
    }
}
