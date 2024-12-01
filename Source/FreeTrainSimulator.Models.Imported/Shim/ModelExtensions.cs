using System.IO;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler;
using FreeTrainSimulator.Models.Imported.ImportHandler.OpenRails;
using FreeTrainSimulator.Models.Settings;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class ModelExtensions
    {
        public static string SourceFile(this WeatherModelCore weatherModel) => weatherModel != null ? Path.Combine(weatherModel.Parent.MstsRouteFolder().WeatherFolder, weatherModel.Tags[WeatherModelHandler.SourceNameKey]) : null;
        public static string SourceFile(this TimetableModel timetableModel) => timetableModel != null ? Path.Combine(timetableModel.Parent.MstsRouteFolder().OpenRailsActivitiesFolder, timetableModel.Tags[TimetableModelHandler.SourceNameKey]) : null;
        public static string SourceFile(this SavePointModel savePointModel) => savePointModel?.Tags[SavePointModelHandler.SourceNameKey];

    }
}
