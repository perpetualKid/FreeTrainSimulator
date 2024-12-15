using System;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.OpenRails;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Handler
{
    //[TestClass]
    //public class RouteRepositoryTests
    //{
    //    [TestMethod]
    //    public async ValueTask LoadRoutes()
    //    {
    //        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
    //            return;
    //        Trace.WriteLine(VersionInfo.FullVersion);
    //        //            //FrozenSet<ProfileModel> profiles = await ProfileModelHandler.GetProfiles(CancellationToken.None).ConfigureAwait(false);
    //        //            //ProfileModel profile = profiles.GetByName(ProfileModelHandler.DefaultProfileName);
    //        //            FrozenSet<ProfileModel> profiles = await ProfileModel.None.GetProfiles(CancellationToken.None).ConfigureAwait(false);
    //        //            ProfileModel profile = await ProfileModel.None.Setup("TestProfile", Enumerable.Empty<(string, string)>(), CancellationToken.None).ConfigureAwait(false);
    //        //            profile = await profiles.GetOrCreate("TestProfile", CancellationToken.None).ConfigureAwait(false);
    //        ////            ProfileModel profile = profiles.GetByName("Test");
    //        //            //            ProfileModel profile = await ProfileModel.None.Get(CancellationToken.None).ConfigureAwait(false);


    //        //            //profile = await profile.Convert(true, CancellationToken.None).ConfigureAwait(false);

    //        ////            profile = await ProfileModel.None.Get(null, CancellationToken.None).ConfigureAwait(false);
    //        //            profile = await profiles.GetOrCreate(null, CancellationToken.None).ConfigureAwait(false);
    //        //            profile = await profile.Convert(true, CancellationToken.None).ConfigureAwait(false);

    //        ////            FolderModel folder = profile.ContentFolders.GetByNameOrFirstByName("Demo Model 1");
    //        ////            await WagonReferenceHandler.ExpandWagonModels(folder, CancellationToken.None).ConfigureAwait(false);
    //        //            //FolderModel folderModel = profile.ContentFolders.GetByName("Demo Model 1");
    //        //            //if (null != folderModel)
    //        //            //{
    //        //            //    folderModel = await folderModel.Get(CancellationToken.None).ConfigureAwait(false);

    //        //            //    FrozenSet<WagonSetModel> wagonSets = await WagonSetModelHandler.ExpandWagonSetModels(folderModel, CancellationToken.None).ConfigureAwait(false);
    //        //            //}
    //        //            //ProfileModel profile = profiles.GetByName("Another Profile");
    //        //            //ProfileModel profile = await ProfileModelHandler.Setup("Another Profile", null, CancellationToken.None).ConfigureAwait(false);
    //        //            //profile = await ProfileModelHandler.Setup("Another Profile", new List<(string, string)>() 
    //        //            //{ 
    //        //            //    ("Demo A", "C:\\Storage\\OR\\Demo Model 1")
    //        //            //}, CancellationToken.None).ConfigureAwait(false);
    //        //            //return;
    //        //            //ProfileModel defaultModel = await ProfileModel.None.Get(CancellationToken.None);
    //        //            //if (null != defaultModel)
    //        //            //{
    //        //            //    FrozenSet<FolderModel> folders = await FolderModelHandler.GetFolders(defaultModel, CancellationToken.None).ConfigureAwait(false);
    //        //            //    FolderModel folderModel = folders?.Where(f => f.Name == "Demo Model 1").FirstOrDefault();
    //        //            //    if (folderModel != null)
    //        //            //    {
    //        //            //        folderModel = await FolderModelHandler.GetCore(folderModel, CancellationToken.None).ConfigureAwait(false);
    //        //            //        FrozenSet<RouteModelCore> routes = await RouteModelHandler.GetRoutes(folderModel, CancellationToken.None).ConfigureAwait(false);

    //        //            //        RouteModelCore routeModel = routes.FirstOrDefault();

    //        //            //        if (null != routeModel)
    //        //            //        {
    //        //            //            routeModel = await RouteModelHandler.GetCore(routeModel, CancellationToken.None).ConfigureAwait(false);
    //        //            //            routeModel = await RouteModelHandler.GetCore(routeModel, CancellationToken.None).ConfigureAwait(false);

    //        //            //            //FrozenSet<PathModelCore> paths = await PathModelHandler.ExpandPathModels(routeModel, CancellationToken.None).ConfigureAwait(false);

    //        //            //            FrozenSet<PathModelCore> paths = await routeModel.GetRoutePaths(CancellationToken.None).ConfigureAwait(false);
    //        //            //            PathModelCore pathModel = paths.FirstOrDefault();

    //        //            //            if (null != pathModel)
    //        //            //            {
    //        //            //                pathModel = await PathModelHandler.GetExtended(pathModel.Id, pathModel.Parent, CancellationToken.None);
    //        //            //            }
    //        //            //        }

    //        //                //    //routes = await RouteModelCoreHandler.GetRoutes(folderModel, CancellationToken.None).ConfigureAwait(false);
    //        //                //}
    //        //            //}
    //    }

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
}
