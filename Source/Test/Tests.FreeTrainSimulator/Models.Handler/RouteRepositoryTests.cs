using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler.OpenRails;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Handler
{
    [TestClass]
    public class RouteRepositoryTests
    {
        //    [TestMethod]
        //    public async ValueTask SaveRoute()
        //    {
        //        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
        //            return;
        //        Trace.WriteLine(VersionInfo.FullVersion);

        //        //ProfileModel profileModel = await ProfileModel.None.Get(CancellationToken.None).ConfigureAwait(false);
        //        //FolderModel folder = (await profileModel.GetFolders(CancellationToken.None).ConfigureAwait(false)).GetByName("Demo Model 1");
        //        ////            RouteModelCore route = (await folder.GetRoutes(CancellationToken.None).ConfigureAwait(false)).GetByName("SCE");

        //        //FrozenSet<RouteModelCore> routes = await RouteModelHandler.ExpandRouteModels(folder, CancellationToken.None).ConfigureAwait(false);

        //    }

        //    [TestMethod]
        //    public async ValueTask ExpandRoute()
        //    {
        //        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
        //            return;
        //        //Trace.WriteLine(VersionInfo.FullVersion);

        //        //ProfileModel profileModel = await ProfileModel.None.Get(CancellationToken.None).ConfigureAwait(false);
        //        //FolderModel folder = (await profileModel.GetFolders(CancellationToken.None).ConfigureAwait(false)).GetByName("OR Linia 202");
        //        ////            RouteModelCore route = (await folder.GetRoutes(CancellationToken.None).ConfigureAwait(false)).GetByName("SCE");

        //        //FrozenSet<RouteModelCore> routes = await folder.GetRoutes(CancellationToken.None).ConfigureAwait(false);
        //        //RouteModelCore route = routes.GetByName("Linia202_80s");

        //        //FrozenSet<TimetableModel> timetables = await TimetableModelHandler.ExpandTimetableModels(route, CancellationToken.None).ConfigureAwait(false);

        //    }

        //}

        [TestMethod]
        public async ValueTask ImportTSection()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
                return;

            ContentModel contentModel = await ContentModel.None.Get(CancellationToken.None).ConfigureAwait(false);
            FolderModel folder = contentModel.ContentFolders.GetByName("Demo");

//            System.Collections.Immutable.ImmutableArray<GlobalTrackSectionModel> trackSections = await contentModel.GetTrackSectionModels(CancellationToken.None);
            bool contains = await contentModel.ContainsTrackSectionVersion(32).ConfigureAwait(false);
            contains = await contentModel.ContainsTrackSectionVersion(38).ConfigureAwait(false);

            GlobalTrackSectionModel globalTrackSection = await GlobalTrackSectionModelHandler.GetGlobal(CancellationToken.None).ConfigureAwait(false);

            await GlobalTrackSectionModelHandler.GetCore(37, CancellationToken.None).ConfigureAwait(false);

            System.Collections.Immutable.ImmutableArray<GlobalTrackSectionModel> result = await GlobalTrackSectionModelHandler.GetTrackSectionModels(CancellationToken.None).ConfigureAwait(false);

            GlobalTrackSectionModel globalTrackSectionModel = await GlobalTrackSectionModelImportHandler.ConvertGlobal(folder, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
