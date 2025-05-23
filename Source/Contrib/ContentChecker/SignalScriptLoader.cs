﻿// COPYRIGHT 2018 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace Orts.ContentChecker
{
    /// <summary>
    /// Loader class for .eng files
    /// </summary>
    internal sealed class SignalScriptLoader : Loader
    {
        /// <summary> The signal configuration file needed for loading a script file </summary>
        private readonly SignalConfigurationFile _sigcfg;

        /// <summary>
        /// default constructor when not enough information is available
        /// </summary>
        public SignalScriptLoader() : base()
        {
            IsDependent = true;
        }

        /// <summary>
        /// Constructor giving the information this loaded depends on
        /// </summary>
        /// <param name="sigcfg">The Signal Configuration object</param>
        public SignalScriptLoader(SignalConfigurationFile sigcfg) : this()
        {
            _sigcfg = sigcfg;
        }

        /// <summary>
        /// Try to load the file.
        /// Possibly this might raise an exception. That exception is not caught here
        /// </summary>
        /// <param name="file">The file that needs to be loaded</param>
        public override void TryLoading(string file)
        {

            if (_sigcfg == null)
            {
                FilesLoaded = 0;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Console.WriteLine("signal script files can not be loaded independently. Try the option /d");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            else {
                // we want to load the signal scripts one by one, not as a group
                List<string> scriptFiles = new List<string>() { Path.GetFileName(file) };
                _ = new SignalScripts(_sigcfg.ScriptPath, scriptFiles, _sigcfg.SignalTypes);
            }
        }
    }
}
