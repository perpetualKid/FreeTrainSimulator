using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class WeatherModelExtension
    {
        public static ValueTask<FrozenSet<WeatherModelCore>> GetWeatherFiles(this RouteModelCore routeModel, CancellationToken cancellationToken) => WeatherModelHandler.GetWeatherFiles(routeModel, cancellationToken);
    }
}
