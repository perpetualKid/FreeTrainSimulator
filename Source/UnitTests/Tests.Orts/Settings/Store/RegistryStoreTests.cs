using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Settings.Store;

namespace Tests.Orts.Settings.Store
{
    [TestClass]
    public class RegistryStoreTests
    {
        [TestMethod]
        public void OpenRegistryStoreTest()
        {
            string path = "SOFTWARE\\OpenRails";
            SettingsStore store = SettingsStore.GetSettingsStore(StoreType.Registry, path, null);
            Assert.IsInstanceOfType(store, typeof(SettingsStoreRegistry));
        }

        [TestMethod]
        public void GetSectionNamesTest()
        {
            string path = "SOFTWARE\\OpenRails";
            SettingsStore store = SettingsStore.GetSettingsStore(StoreType.Registry, path, null);
            var sections = store.GetSectionNames();
            Assert.IsNotNull(sections);
        }
    }
}
