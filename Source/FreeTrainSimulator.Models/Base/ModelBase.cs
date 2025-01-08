using System.Collections.Generic;
using System.Text;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Base
{

    /// <summary>
    /// Abstract base class for all content models
    /// </summary>
    public abstract record ModelBase
    {
        private const char HierarchySeparator = '/';
        private string _hierarchy;
        private string _version;
        private protected ModelBase _parent;

        #region internal handling
        public void RefreshModel()
        {
            _version = VersionInfo.Version;
        }

        public virtual void Initialize(ModelBase parent)
        {
            _parent = parent;
        }
        #endregion

        /// <summary>
        /// strongly typed reference to parent model
        /// </summary>
        [MemoryPackIgnore]
        public abstract ModelBase Parent { get; }
        /// <summary>
        /// Unique Id of this instance within the parent entity, also need to be file-system compatible
        /// </summary>
        public string Id { get; init; }
        /// <summary>
        /// Name of this instance within the parent entity, may not be unique nor file-system compatible
        /// </summary>
        public string Name { get; init; }
        /// <summary>
        /// Application Version when this instance was last time updated
        /// </summary>
        public string Version { get => _version; init { _version = value; } }
        /// <summary>
        /// Tag property to persist arbitrary additional information
        /// </summary>
        public Dictionary<string, string> Tags { get; init; }

        protected ModelBase()
        {
        }

        protected ModelBase(string name, ModelBase parent)
        {
            Id = name;
            Name = name;
            _parent = parent;
        }

        #region Hierarchy
        public string Hierarchy()
        {
            if (string.IsNullOrEmpty(_hierarchy))
            {
                StringBuilder builder = new StringBuilder();
                BuildHiearchy(this, builder);
                _hierarchy = builder.ToString();
            }
            return _hierarchy;
        }

        public string Hierarchy(string modelName)
        {
            return string.Concat(Hierarchy(), HierarchySeparator, modelName);
        }

        private static void BuildHiearchy(ModelBase model, StringBuilder builder)
        {
            if (model.Parent is ModelBase parent)
            {
                BuildHiearchy(parent, builder);
                _ = builder.Append(HierarchySeparator);
            }
            _ = builder.Append(model.Id);
        }
        #endregion
    }
}
