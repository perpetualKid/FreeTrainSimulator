using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Settings
{
    public abstract record ProfileSettingsModelBase : ModelBase
    {
        public override ProfileModel Parent => _parent as ProfileModel;
    }
}
