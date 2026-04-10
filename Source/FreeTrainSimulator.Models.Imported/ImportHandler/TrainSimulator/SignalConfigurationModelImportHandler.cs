using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
            CompatibilityMode compatibilityMode = routeFolder.ORSignalConfigFile;

            if (!System.IO.File.Exists(sigcfgFile))
            {
                Trace.TraceWarning($"Signal Configuration File not found: {sigcfgFile}");
                return null;
            }

            SignalConfigurationFile signalConfigurationFile = new SignalConfigurationFile(sigcfgFile, compatibilityMode);

            SignalConfigurationModel signalConfigModel = new SignalConfigurationModel()
            {
                Id = routeModel.Id,
                LightTextures = ConvertLightTextures(signalConfigurationFile),
                SignalTypes = ConvertSignalTypes(signalConfigurationFile, sigcfgFile),
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

        private static ImmutableDictionary<string, SignalType> ConvertSignalTypes(SignalConfigurationFile signalConfigurationFile, string sigcfgFile)
        {
            return signalConfigurationFile.SignalTypes == null || signalConfigurationFile.SignalTypes.Count == 0
                ? ImmutableDictionary<string, SignalType>.Empty
                : signalConfigurationFile.SignalTypes.ToImmutableDictionary(
                signalType => signalType.Key,
                signalType => new SignalType()
                {
                    Name = signalType.Value.Name,
                    Script = signalType.Value.Script,
                    FunctionType = signalType.Value.SignalFunction,
                    NormalSubType = signalType.Value.NormalSubType,
                    SignalFlags = (signalType.Value.Abs ? SignalFlags.Abs : SignalFlags.None)
                        | (signalType.Value.NoGantry ? SignalFlags.NoGantry : SignalFlags.None) 
                        | (signalType.Value.Semaphore ? SignalFlags.Semaphore : SignalFlags.None),
                    FlashTimeOn = signalType.Value.FlashTimeOn,
                    FlashTimeOff = signalType.Value.FlashTimeOff,
                    TransitionTime = signalType.Value.TransitionTime,
                    LightTexture = signalType.Value.LightTextureName,
                    SemaphoreAnimationnDuration = signalType.Value.SemaphoreInfo,
                    DayGlow = signalType.Value.DayGlow,
                    NightGlow = signalType.Value.NightGlow,
                    DayLight = signalType.Value.DayLight,
                    SignalClearAheadMode = signalType.Value.ClearAheadMode,
                    ClearAheadNumber = signalType.Value.ClearAheadNumber,
                    Lights = signalType.Value.Lights?.Select(light => 
                    {
                        if (!signalConfigurationFile.LightsTable.TryGetValue(light.Name, out Orts.Formats.Msts.Models.LightTableEntry colorEntry))
                        {
                            Trace.TraceWarning($"Missing or invalid signal light {light.Name} for signal type {signalType.Value.Name} in {sigcfgFile}");
                        }
                        return new SignalLight(light.Position, colorEntry?.Color ?? Color.Black)
                        {
                            Name = light.Name,
                            Radius = light.Radius,
                            SemaphoreChange = light.SemaphoreChange,
                        };
                    }).ToImmutableArray() ?? ImmutableArray<SignalLight>.Empty,
                    DrawStates = signalType.Value.DrawStates?.ToImmutableDictionary(
                        kvp => kvp.Key,
                        kvp => new SignalDrawState()
                        {
                            Name = kvp.Value.Name,
                            SemaphorePosition = (int)kvp.Value.SemaphorePosition,
                            DrawStateLights = ToSparseDrawStateLightArray(kvp.Value.DrawLights),
                        },
                        StringComparer.OrdinalIgnoreCase) ?? ImmutableDictionary<string, SignalDrawState>.Empty,
                    //Aspects = signalType.Aspects?.Select(ConvertAspect).ToImmutableArray() ?? ImmutableArray<SignalAspectModel>.Empty,
                    //ApproachControlDetails = signalType.ApproachControlDetails != null
                    //    ? new ApproachControlLimitsModel()
                    //    {
                    //        ApproachControlPositionM = signalType.ApproachControlDetails.ApproachControlPositionM,
                    //        ApproachControlSpeedMpS = signalType.ApproachControlDetails.ApproachControlSpeedMpS,
                    //    }
                    //    : null,
                },
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts a list of items with an Index property into a sparse array.
        /// Missing indices will be null.
        /// </summary>
        private static ImmutableArray<SignalDrawStateLightMode> ToSparseDrawStateLightArray(List<Orts.Formats.Msts.Models.SignalDrawLight> drawLights)
        {
            ArgumentNullException.ThrowIfNull(drawLights);
            if (drawLights.Count == 0)
                return ImmutableArray<SignalDrawStateLightMode>.Empty;

            // Create array with nulls
            SignalDrawStateLightMode[] result = new SignalDrawStateLightMode[drawLights.Max(light => light.Index) + 1];

            // Place items at their index positions
            foreach (Orts.Formats.Msts.Models.SignalDrawLight item in drawLights)
            {
                result[item.Index] = item.Flashing ? SignalDrawStateLightMode.Flashing : SignalDrawStateLightMode.Lit;
            }
            return result.ToImmutableArray();
        }
    }
}
