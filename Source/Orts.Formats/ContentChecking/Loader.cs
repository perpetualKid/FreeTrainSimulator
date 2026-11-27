// COPYRIGHT 2018 by the Open Rails project.
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

// The Loader class is the base class for all kinds of classes that load files
// An instance of a loader (sub)class has the responsibility to
//  * Load a single file. The kind of file that is being loaded depends on the actual subclass.
//    The loading should be done by instantiating a class from ORTS that is used to load such a file
//    in the regular simulator. In that way all messages and possibly errors are handled in the same way
//    as during simulation.
//    Note that since each Loader (sub)class is responsible for loading a single type of file, 
//    for each type of file a separate loader (sub)class is needed.
//  * If so requested, identify additional related files. For all these related files a filename
//    needs to be determined, as well as the Loader subclass that should be used for that kind of file.
//    The actual loading is done at a higher hierarchy level.
//
// In general each instance should load only one file. The reason for this is that it allows,
// at a later stage, to make it easier to do cross-checking of different files.
//
// One of the big differences with the normal ORTS code is that the normal code really starts
// from a single start-point (the .trk file). Here, however, we want to be able to load only 
// a limited set of files: so we need to have a little more independence.
//
// Note that any exception handling during loading is not done in the loader (sub)classes. This is
// done at a higher level, currently.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Formats.ContentChecking
{
    internal abstract class Loader
    {
        /// <summary> The number of files that were actually loaded </summary>
        public int FilesLoaded { get; protected set; }
        /// <summary> The number of files that were not loaded but skipped </summary>
        public int FilesSkipped {get; protected set;}

        /// <summary> The action to take when an additonal file has been identified. This is intended to be set externally </summary>
        protected Action<string, Loader> AddAdditionalFileAction { get; set; }

        /// <summary> The filename of the originally loaded file </summary>
        protected string loadedFile;

        /// <summary> The status of the file that is going to be loaded </summary>
        public bool IsDependent;

        /// <summary>
        /// Constructor
        /// </summary>
        protected Loader()
        {
            FilesLoaded = 1;
            FilesSkipped = 0;
        }

        /// <summary>
        /// Try to load the file.
        /// Possibly this might raise an exception. That exception is not caught here
        /// </summary>
        /// <param name="file">The file that needs to be loaded</param>
        public abstract void TryLoading(string file);

        /// <summary>
        /// Try to load the file.
        /// Possibly this might raise an exception. That exception is not caught here
        /// </summary>
        /// <param name="additionType"> The type of files that need to be added</param>
        /// <param name="additionalFileAction"> The action to take when an additonal file has been identifiedy </param>
        public void AddAdditionalFiles(AdditionType additionType, Action<string, Loader> additionalFileAction)
        {
            if (Debugger.IsAttached)
            {   // a bit if a trick. We want to make sure a loader is used only once.
                if (AddAdditionalFileAction != null)
                {
                    throw new InvalidOperationException("A loader class should be used only once");
                }
            }
            AddAdditionalFileAction = additionalFileAction;
            switch (additionType)
            {
                case AdditionType.None:
                    break;
                case AdditionType.Dependent:
                    AddDependentFiles();
                    break;
                case AdditionType.Referenced:
                    AddDependentFiles();
                    AddReferencedFiles();
                    break;
                case AdditionType.All:
                    AddDependentFiles();
                    AddReferencedFiles();
                    AddAllFiles();
                    break;
            }
        }

        /// <summary>
        /// Identify all the files that directly depend on the file being loaded and add them to
        /// the list of files still to be loaded.
        /// </summary>
        protected virtual void AddDependentFiles() { }

        /// <summary>
        /// Identify all the files that are directly referenced by file being loaded and add them to
        /// the list of files still to be loaded.
        /// </summary>
        protected virtual void AddReferencedFiles() { }

        /// <summary>
        /// Identify all the files that are indirectly related to the file being loaded and add them to
        /// the list of files still to be loaded. This is mainly for the .trk file
        /// </summary>
        protected virtual void AddAllFiles() { }

        /// <summary>
        /// Configure the loader with the output, graphics device, and wave file checker.
        /// </summary>
        /// <param name="output">The output writer.</param>
        /// <param name="graphicsDevice">The graphics device.</param>
        /// <param name="waveFileChecker">The wave file checker action.</param>
        internal void Configure(TextWriter output, GraphicsDevice graphicsDevice, Action<string> waveFileChecker)
        {
            Output = output;
            GraphicsDevice = graphicsDevice;
            WaveFileChecker = waveFileChecker;
        }

        /// <summary> The output writer </summary>
        protected TextWriter Output { get; private set; }
        /// <summary> The graphics device </summary>
        protected GraphicsDevice GraphicsDevice { get; private set; }
        /// <summary> The action to check wave files </summary>
        protected Action<string> WaveFileChecker { get; private set; }
    }
}
