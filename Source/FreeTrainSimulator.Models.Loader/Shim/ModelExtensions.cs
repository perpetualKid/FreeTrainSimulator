using System.IO;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ModelExtensions
    {
        public static string SourceFile(this WeatherModelCore weatherModel) => weatherModel != null ? Path.Combine(weatherModel.Parent.MstsRouteFolder().WeatherFolder, weatherModel.Tags[WeatherModelHandler.SourceNameKey]) : null;
        public static string SourceFile(this TimetableModel timetableModel) => timetableModel != null ? Path.Combine(timetableModel.Parent.MstsRouteFolder().OpenRailsActivitiesFolder, timetableModel.Tags[TimetableModelHandler.SourceNameKey]) : null;
        public static string SourceFile(this SavePointModel savePointModel) => savePointModel?.Tags[SavePointModelHandler.SourceNameKey];

    }
}
