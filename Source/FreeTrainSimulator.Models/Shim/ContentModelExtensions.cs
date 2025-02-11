using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using System.Collections.Generic;
using FreeTrainSimulator.Common.Info;

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
        public static T GetByName<T>(this FrozenSet<T> models, string name) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static T GetByNameOrFirstByName<T>(this FrozenSet<T> models, string name) where T : ModelBase
        {
            return models.GetByName(name) ?? models.OrderBy(m => m.Name).FirstOrDefault();
        }

        public static T GetById<T>(this FrozenSet<T> models, string id) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }
        #endregion
    }
}
