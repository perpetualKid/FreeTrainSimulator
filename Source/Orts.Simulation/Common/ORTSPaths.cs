// COPYRIGHT 2009, 2013 by the Open Rails project.
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

using System.Diagnostics;
using System.IO;


namespace Orts.Common
{
    public static class ORTSPaths
    {
        //<CJComment> Cleaner to use GetFileFromFolders() instead, but not sure how to test this. </CJComment>
        public static string FindTrainCarPlugin( string initialFolder, string filename )
        {
            string dllPath = initialFolder + "\\" + filename;  // search in trainset folder
            if (File.Exists(dllPath))
                return dllPath;
            string rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(initialFolder)))+ "\\OpenRails";
            if( Directory.Exists( rootFolder ) )
            {
                dllPath = rootFolder + "\\" + filename;
                if (File.Exists(dllPath))
                    return dllPath;
            }
            return filename;   // then search in OpenRails program folder
        }

        /// <summary>
        /// Static variables to reduce occurrence of duplicate warning messages.
        /// </summary>
        static string badBranch = "";
        static string badPath = "";
        static readonly System.Collections.Generic.Dictionary<string, System.Collections.Specialized.StringDictionary> filesFound = new System.Collections.Generic.Dictionary<string, System.Collections.Specialized.StringDictionary>();

        /// <summary>
        /// Search an array of paths for a file. Paths must be in search sequence.
        /// No need for trailing "\" on path or leading "\" on branch parameter.
        /// </summary>
        /// <param name="pathArray">2 or more folders, e.g. "D:\MSTS", E:\OR"</param>
        /// <param name="branch">a filename possibly prefixed by a folder, e.g. "folder\file.ext"</param>
        /// <returns>null or the full file path of the first file found</returns>
        public static string GetFileFromFolders(string[] paths, string fileRelative)
        {
            if (string.IsNullOrEmpty(fileRelative)) return string.Empty;

            System.Collections.Specialized.StringDictionary existingFiles;
            if (filesFound.TryGetValue(fileRelative, out existingFiles))
            {
                foreach (string path in paths)
                {
                    if (existingFiles.ContainsKey(path))
                        return existingFiles[path];
                }
            }
            foreach (string path in paths)
            {
                string fullPath = Path.Combine(path, fileRelative);
                if (File.Exists(fullPath))
                {
                    if (null != existingFiles)
                        existingFiles.Add(path, fullPath);
                    else
                        filesFound.Add(fileRelative, new System.Collections.Specialized.StringDictionary
                                {
                                    { path, fullPath }
                                });
                    return fullPath;
                }
            }

            var firstPath = paths[0];
            if (fileRelative != badBranch || firstPath != badPath)
            {
                Trace.TraceWarning("File {0} missing from {1}", fileRelative, firstPath);
                badBranch = fileRelative;
                badPath = firstPath;
            }
            return null;
        }
    }
}
