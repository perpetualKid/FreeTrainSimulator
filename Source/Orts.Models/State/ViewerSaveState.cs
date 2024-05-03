using System.Collections.ObjectModel;
using System.Drawing;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class ViewerSaveState : SaveStateBase
    {
        public int PlayerTrainIndex { get; set; }
        public int PlayerLocomotiveIndex { get; set; }
        public int SelectedTrainIndex { get; set; }
        public int SelectedCameraIndex { get; set; }
        public Point CabOffset { get; set; }
        public bool NightTexturesLoaded { get; set; }
        public bool DayTexturesLoaded { get; set; }
        public Collection<CameraSaveState> CameraStates { get; } = new Collection<CameraSaveState>();
        public CameraSaveState CurrentCamera { get; set; }
        public CabRendererSaveState CabState2D { get; set; }
        public CabRendererSaveState CabState3D { get; set; }
        public WeatherSaveState WeatherState { get; set; }
    }
}
