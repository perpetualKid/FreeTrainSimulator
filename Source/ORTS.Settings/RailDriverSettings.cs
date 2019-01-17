using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ORTS.Common;

namespace ORTS.Settings
{
    public class RailDriverSettings : SettingsBase
    {
        /// <summary>
        /// Initializes a new instances of the <see cref="InputSettings"/> class with the specified options.
        /// </summary>
        /// <param name="options">The list of one-time options to override persisted settings, if any.</param>
        public RailDriverSettings(IEnumerable<string> options)
        : base(SettingsStore.GetSettingStore(UserSettings.SettingsFilePath, UserSettings.RegistryKey, "RailDriver"))
        {
//            InitializeCommands(Commands);
            Load(options);
        }

        public override object GetDefaultValue(string name)
        {
            throw new NotImplementedException();
        }

        public override void Reset()
        {
            throw new NotImplementedException();
        }

        public override void Save()
        {
            throw new NotImplementedException();
        }

        public override void Save(string name)
        {
            throw new NotImplementedException();
        }

        protected override object GetValue(string name)
        {
            throw new NotImplementedException();
        }

        protected override void Load(bool allowUserSettings, Dictionary<string, string> optionsDictionary)
        {
            //foreach (var command in GetCommands())
            //    Load(allowUserSettings, optionsDictionary, command.ToString(), typeof(string));
        }

        protected override void SetValue(string name, object value)
        {
            throw new NotImplementedException();
        }
    }
}
