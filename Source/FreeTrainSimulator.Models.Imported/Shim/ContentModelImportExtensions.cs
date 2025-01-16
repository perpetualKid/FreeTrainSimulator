using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class ContentModelImportExtensions
    {
        public static Task<ContentModel> Setup(this ContentModel contentModel, IProgress<int> progressClient, CancellationToken cancellationToken)
        {
            return ContentModelConverter.SetupContent(contentModel, true, progressClient, cancellationToken);
        }

        public static Task<ContentModel> Convert(this ContentModel contentModel, bool force, CancellationToken cancellationToken)
        {
            return ContentModelConverter.ConvertContent(contentModel, force, cancellationToken);
        }

    }
}
