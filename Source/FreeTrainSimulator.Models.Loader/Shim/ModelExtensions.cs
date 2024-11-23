using System.IO;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ModelExtensions
    {
        public static string SourceFile(this WeatherModelCore weatherModel) => weatherModel != null ? Path.Combine(weatherModel.Parent.MstsRouteFolder().WeatherFolder, weatherModel.Tags[WeatherModelHandler.SourceNameKey]) : null;

    }
}
