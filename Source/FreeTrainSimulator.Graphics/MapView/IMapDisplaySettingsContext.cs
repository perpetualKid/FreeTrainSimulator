using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapDisplaySettingsContext
    {
        void UpdateColor(ColorSetting setting, Microsoft.Xna.Framework.Color color, bool fontOutlining);

        void UpdateTrackWidthSettings(bool limitTrackWidth);
    }
}
