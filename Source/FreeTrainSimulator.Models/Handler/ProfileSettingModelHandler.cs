using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Settings;

using MemoryPack;

namespace FreeTrainSimulator.Models.Handler
{
    public class ProfileSettingModelHandler<TSettingsModel> where TSettingsModel : ProfileSettingsModelBase, IFileResolve
    {
        public const string SaveStateExtension = FileNameExtensions.SaveFile;
        //private static readonly Dictionary<string, PropertyInfo> properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).Distinct(p => p.n).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        //public static object GetValueByName(string propertyName)
        //{
        //    //return properties[propertyName];
        //}

        public static void SetValueByName(TSettingsModel instance, string propertyName, object value)
        {
            IOrderedEnumerable<PropertyInfo> properties = typeof(TSettingsModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name);
            properties.Where(p => p.Name == propertyName).FirstOrDefault()?.SetValue(instance, value);
        }

        internal protected static async Task<TSettingsModel> FromFile(TSettingsModel instance, CancellationToken cancellationToken)
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

        internal protected static async Task<TSettingsModel> ToFile(TSettingsModel model, CancellationToken cancellationToken)
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
