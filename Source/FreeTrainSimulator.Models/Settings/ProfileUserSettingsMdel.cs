using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ProfileUserSettingsMdel : ModelBase<ProfileUserSettingsMdel>
    {
        public override ProfileModel Parent => (this as IFileResolve).Container as ProfileModel;
    }
}
