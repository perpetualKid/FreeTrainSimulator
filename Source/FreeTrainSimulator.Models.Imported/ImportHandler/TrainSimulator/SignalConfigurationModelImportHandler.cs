using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Signal;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal class SignalConfigurationModelImportHandler : ContentHandlerBase<SignalConfigurationModel>
    {
        public static Task<SignalConfigurationModel> ExpandSignalConfigurationModel(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            Task<SignalConfigurationModel> modelTask = Convert(routeModel, cancellationToken);
            modelTaskCache[routeModel.Id] = modelTask;
            return modelTask;
        }

        private static async Task<SignalConfigurationModel> Convert(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            FolderStructure.ContentFolder.RouteFolder routeFolder = routeModel.MstsRouteFolder();
            string sigcfgFile = routeFolder.SignalConfigurationFile;
            bool orMode = routeFolder.ORSignalConfigFile;

            if (!System.IO.File.Exists(sigcfgFile))
            {
                Trace.TraceWarning($"Signal Configuration File not found: {sigcfgFile}");
                SignalConfigurationModel emptyModel = new SignalConfigurationModel()
                {
                    Id = routeModel.Id
                };
                await Create(emptyModel, routeModel, cancellationToken).ConfigureAwait(false);
                return emptyModel;
            }

            SignalConfigurationFile signalConfigurationFile = new SignalConfigurationFile(sigcfgFile, orMode);

            SignalConfigurationModel signalConfigModel = new SignalConfigurationModel()
            {
                Id = routeModel.Id,
                LightTextures = ConvertLightTextures(signalConfigurationFile),
                LightColors = ConvertLightColors(signalConfigurationFile),
            };

            await Create(signalConfigModel, routeModel, cancellationToken).ConfigureAwait(false);
            return signalConfigModel;
        }

        private static ImmutableDictionary<string, SignalLightTexture> ConvertLightTextures(SignalConfigurationFile signalConfigurationFile)
        {
            return signalConfigurationFile.LightTextures == null || signalConfigurationFile.LightTextures.Count == 0
                ? ImmutableDictionary<string, SignalLightTexture>.Empty
                : signalConfigurationFile.LightTextures.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => new SignalLightTexture()
                {
                    Name = kvp.Value.Name,
                    TextureFile = kvp.Value.TextureFile,
                    U0 = kvp.Value.TextureCoordinates.M00,
                    V0 = kvp.Value.TextureCoordinates.M01,
                    U1 = kvp.Value.TextureCoordinates.M10,
                    V1 = kvp.Value.TextureCoordinates.M11,
                },
                StringComparer.OrdinalIgnoreCase);
        }

        private static ImmutableDictionary<string, Color> ConvertLightColors(SignalConfigurationFile signalConfigurationFile)
        {
            return signalConfigurationFile.LightsTable == null || signalConfigurationFile.LightsTable.Count == 0
                ? ImmutableDictionary<string, Color>.Empty
                : signalConfigurationFile.LightsTable.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Color,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
