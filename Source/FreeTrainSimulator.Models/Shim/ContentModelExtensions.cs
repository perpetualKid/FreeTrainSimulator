using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ContentModelExtensions
    {
        #region Content Model
        public static async Task<ContentModel> Get(this ContentModel _, CancellationToken cancellationToken) => await ContentModelHandler.GetCore(cancellationToken).ConfigureAwait(false) ?? await Setup(_, null, cancellationToken).ConfigureAwait(false);
        public static Task<ContentModel> Setup(this ContentModel _, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) => ContentModelHandler.Setup(folders, cancellationToken);
        public static bool RefreshRequired(this ContentModel contentModel) => contentModel?.Version.Compare(ContentModel.MinimumVersion) < 0;
        #endregion

        #region common extensions
        public static T GetByName<T>(this ImmutableArray<T> models, string name) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static T GetByNameOrFirstByName<T>(this ImmutableArray<T> models, string name) where T : ModelBase
        {
            return models.GetByName(name) ?? models.OrderBy(m => m.Name).FirstOrDefault();
        }

        public static T GetById<T>(this ImmutableArray<T> models, string id) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }
        #endregion
    }
}
