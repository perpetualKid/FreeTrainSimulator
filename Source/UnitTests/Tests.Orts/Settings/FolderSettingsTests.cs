using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Settings;
using Orts.Settings.Store;
using Tests.Orts.Shared;

namespace Tests.Orts.Settings
{
    [TestClass]
    public class FolderSettingsTests
    {
        [TestMethod]
        public void OpenFolderSettingsTest()
        {
            using (TestFile testFile = new TestFile(string.Empty))
            {
                FolderSettings folders = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsNotNull(folders.Folders);
            }
        }

        [TestMethod]
        public void ReadSingleFolderTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings folders = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(folders.Folders.Count == 1);
            }
        }

        [TestMethod]
        public void SaveAllTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Folders.Add("Another", "Folder");
                source.Save();
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 2);
            }
        }

        [TestMethod]
        public void SavedDedicatdTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Folders.Add("Another", "Folder");
                source.Save("Another");
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 2);
            }
        }

        [TestMethod]
        public void UpdateValueSaveAllTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Folders.Add("Another", "Folder");
                source.Folders["Name"] = "NewValue";
                source.Save();
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 2);
                Assert.IsTrue(target.Folders["Name"] == "NewValue");
            }
        }

        [TestMethod]
        public void UpdateValueSaveDedicatedTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Folders.Add("Another", "Folder");
                source.Folders["Name"] = "NewValue";
                source.Save("Another");
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 2);
                Assert.IsTrue(target.Folders["Name"] == "Value");
                Assert.IsTrue(target.Folders["Another"] == "Folder");
            }
        }

        [TestMethod]
        public void RemoveValueButNotSaveTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Folders.Add("Another", "Folder");
                source.Folders["Name"] = null;
                source.Save("Another");
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 2);
                Assert.IsTrue(target.Folders["Name"] == "Value");
                Assert.IsTrue(target.Folders["Another"] == "Folder");
            }
        }

        [TestMethod]
        public void RemoveValueTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Folders.Add("Another", "Folder");
                source.Folders.Remove("Name");
                source.Save();
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 1);
                Assert.IsTrue(target.Folders["Another"] == "Folder");
            }
        }

        [TestMethod]
        public void ResetTest()
        {
            using (TestFile testFile = new TestFile("[Folders]\r\nName=string:Value"))
            {
                FolderSettings source = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                source.Reset();
                source.Save();
                FolderSettings target = new FolderSettings(null, SettingsStore.GetSettingsStore(StoreType.Ini, testFile.FileName, null));
                Assert.IsTrue(target.Folders.Count == 0);
            }
        }
    }
}
