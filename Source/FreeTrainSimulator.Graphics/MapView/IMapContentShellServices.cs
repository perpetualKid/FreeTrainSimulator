using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapContentShellServices
    {
        void Initialize();

        void UpdateColor(ColorSetting setting, Color color, bool fontOutlining);

        void UpdateTrackWidthSettings(bool limitTrackWidth);

        void RequestRedraw();
    }
}
