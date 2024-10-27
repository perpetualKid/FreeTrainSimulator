using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal class WagonModelHandler: ContentHandlerBase<WagonModelCore>
    {
        private static Task<WagonModelCore> Convert(string filePath, WagonSetModel traincarSet, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(traincarSet, nameof(traincarSet));

            if (File.Exists(filePath))
            {
                return Task.FromResult(new WagonModelCore());
            }
            else
            {
                Trace.TraceWarning($"Consist file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
