using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Settings.Store;
using Tests.Orts.Shared;

namespace Tests.Orts.Settings.Store
{
    [TestClass]
    public class LocalIniStoreTests
    {

        [TestMethod]
        public void GetLocalIniStoreFromExistingFile()
        {
            string path = Path.GetTempFileName();
            SettingsStore store = SettingsStore.GetSettingsStore(StoreType.Ini, path, null);
            Assert.IsInstanceOfType(store, typeof(SettingsStoreLocalIni));
        }

        [TestMethod]
        public void GetLocalIniStoreFromNewFile()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            SettingsStore store = SettingsStore.GetSettingsStore(StoreType.Ini, path, null);
            Assert.IsInstanceOfType(store, typeof(SettingsStoreLocalIni));
        }

        [TestMethod]
        public void LoadSectionsEmptyTest()
        {
            using (TestFile file = new TestFile(string.Empty))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, null) as SettingsStoreLocalIni;
                var sections = store.GetSectionNames();

                CollectionAssert.AreEqual(new string[0], sections);
            }
        }

        [TestMethod]
        public void LoadSectionsSimpleTest()
        {
            string[] contentArray = new string[] { "[ORTS]", "[End]", "#[End]" };
            string content = string.Join(Environment.NewLine, contentArray);
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, null) as SettingsStoreLocalIni;
                var sections = store.GetSectionNames();

                CollectionAssert.AreEqual(contentArray.Where((s) => !s.StartsWith("#")).Select((s) => s.Trim('[', ']')).ToArray(), sections);
            }
        }

        [TestMethod]
        public void LoadSectionsExtendedTest()
        {
            string[] contentArray = new string[] { "[ORTS]", "name=value", "[End]", "name=value", "name=value", "#[End]" };
            string content = string.Join(Environment.NewLine, contentArray);
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, null) as SettingsStoreLocalIni;
                var sections = store.GetSectionNames();

                CollectionAssert.AreEqual(contentArray.Where((s) => s.StartsWith("[")).Select((s) => s.Trim('[', ']')).ToArray(), sections);
            }
        }

        [TestMethod]
        public void LoadSectionsExcessiveTest()
        {
            int count = 100;
            string[] contentArray = new string[count * 6];
            for (int i = 0; i < 100; i++)
            {
                contentArray[i * 6 + 0] = "[ORTS]";
                contentArray[i * 6 + 1] = "name=value";
                contentArray[i * 6 + 2] = "[End]";
                contentArray[i * 6 + 3] = "name=value";
                contentArray[i * 6 + 4] = "name=value";
                contentArray[i * 6 + 5] = "#[End]";
            }
            string content = string.Join(Environment.NewLine, contentArray);

            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, null) as SettingsStoreLocalIni;
                var sections = store.GetSectionNames();

                CollectionAssert.AreEqual(contentArray.Where((s) => s.StartsWith("[")).Select((s) => s.Trim('[', ']')).ToArray(), sections);
            }
        }

        [TestMethod]
        public void GetValueNamesTest()
        {
            string[] contentArray = new string[] { "[ORTS]", "name=value", "name1=value", "name2=value", "#[End]", "name=value", "[End]", "name=value" };
            string content = string.Join(Environment.NewLine, contentArray);
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                var names = store.GetSettingNames();

                CollectionAssert.AreEqual(new string[] {"name", "name1", "name2", "name" }, names);
            }
        }

        [TestMethod]
        public void GetValueNamesSectionNotExistingTest()
        {
            string[] contentArray = new string[] { "[ORTS]", "name=value", "name1=value", "name2=value", "#[End]", "name=value", "[End]", "name=value" };
            string content = string.Join(Environment.NewLine, contentArray);
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "STRO") as SettingsStoreLocalIni;
                var names = store.GetSettingNames();

                CollectionAssert.AreEqual(new string[0], names);
            }
        }

        [TestMethod]
        public void GetSettingValueNotExisting()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", "name=bool:value" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                Assert.IsNull(store.GetSettingValue("name1", (string)null));
            }
        }

        [TestMethod]
        public void GenericGetSettingValueNotExisting()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", "name=bool:value" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                Assert.IsTrue(store.GetSettingValue("name1", true));
            }
        }

        [TestMethod]
        public void GenericGetSettingValue()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", "name=bool:false" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                Assert.IsFalse(store.GetSettingValue("name", true));
            }
        }

        [TestMethod]
        public void GetSettingValueBool()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", "name=bool:true" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                Assert.IsTrue(store.GetSettingValue("name", false));
            }
        }

        [TestMethod]
        public void GetSettingValueInt()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", "name=int:7" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                Assert.AreEqual(7, store.GetSettingValue("name", 0));
            }
        }

        [TestMethod]
        public void GetSettingValueTimeSpan()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name={nameof(TimeSpan)}:1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                Assert.AreEqual(TimeSpan.FromSeconds(1000), store.GetSettingValue("name", TimeSpan.Zero));
            }
        }

        [TestMethod]
        public void GetSettingValueIntArray()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=int[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                CollectionAssert.AreEqual(new int[]{0, 1, 10, 100, 1000}, store.GetSettingValue("name", new int[0]));
            }
        }

        [TestMethod]
        public void GetSettingValueIntArrayGeneric()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=int[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                CollectionAssert.AreEqual(new int[] { 0, 1, 10, 100, 1000 }, store.GetSettingValue("name", new int[0]));
            }
        }

        [TestMethod]
        public void GetSettingValueStringArray()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                CollectionAssert.AreEqual(new string[] { "0", "1", "10", "100", "1000" }, store.GetSettingValue("name", new string[0]));
            }
        }

        [TestMethod]
        public void SetSettingValueBool()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                store.SetSettingValue("boolValue", true);
                Assert.IsTrue(store.GetSettingValue("boolValue", false));
            }
        }

        [TestMethod]
        public void SetSettingValueInt()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                store.SetSettingValue("intValue", -17);
                Assert.AreEqual(-17, store.GetSettingValue("intValue", 0));
            }
        }

        [TestMethod]
        public void SetSettingValueString()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                store.SetSettingValue("stringValue", "abcdef");
                Assert.AreEqual("abcdef", store.GetSettingValue("stringValue", string.Empty));
            }
        }

        [TestMethod]
        public void SetSettingValueByte()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                store.SetSettingValue("byteValue", 127);
                Assert.AreEqual(127, store.GetSettingValue("byteValue", byte.MaxValue));
            }
        }

        [TestMethod]
        public void SetSettingValueIntArray()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                store.SetSettingValue("intArrayValue", new int[] { 0, 1, 10, 100, 1000 });
                CollectionAssert.AreEqual(new int[] { 0, 1, 10, 100, 1000 }, store.GetSettingValue("intArrayValue", new int[0]));
            }
        }

        [TestMethod]
        public void SetSettingValueStringArray()
        {
            string content = string.Join(Environment.NewLine, new string[] { "[ORTS]", $"name=string[]:0,1,10,100,1000" });
            using (TestFile file = new TestFile(content))
            {
                SettingsStoreLocalIni store = SettingsStore.GetSettingsStore(StoreType.Ini, file.FileName, "ORTS") as SettingsStoreLocalIni;
                store.SetSettingValue("stringArrayValue", new string[] { "0", "1", "10", "100", "1000" });
                CollectionAssert.AreEqual(new string[] { "0", "1", "10", "100", "1000" }, store.GetSettingValue("stringArrayValue", new string[0]));
            }
        }
    }
}
