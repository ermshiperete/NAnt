// NAnt - A .NET build tool
// Copyright (C) 2001-2004 Gerry Shaw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// Gerry Shaw (gerry_shaw@yahoo.com)
// Ian MacLean (ian_maclean@another.com)

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using NAnt.Core.Attributes;
using NAnt.Core.Types;
using NAnt.Core.Util;

using NAnt.Core.Filters;

namespace NAnt.Core.Tasks {
    /// <summary>
    /// Copies a file or set of files to a new file or directory.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   Files are only copied if the source file is newer than the destination 
    ///   file, or if the destination file does not exist.  However, you can 
    ///   explicitly overwrite files with the <see cref="Overwrite" /> attribute.
    ///   </para>
    ///   <para>
    ///   When a <see cref="FileSet" /> is used to select files to copy, the 
    ///   <see cref="ToDirectory" /> attribute must be set. Files that are 
    ///   located under the base directory of the <see cref="FileSet" /> will
    ///   be copied to a directory under the destination directory matching the
    ///   path relative to the base directory of the <see cref="FileSet" />,
    ///   unless the <see cref="Flatten" /> attribute is set to
    ///   <see langword="true" />.
    ///   </para>
    ///   <para>
    ///   Files that are not located under the the base directory of the
    ///   <see cref="FileSet" /> will be copied directly under to the destination 
    ///   directory, regardless of the value of the <see cref="Flatten" />
    ///   attribute.
    ///   </para>
    ///   <h4>Encoding</h4>
    ///   <para>
    ///   Unless an encoding is specified, the encoding associated with the 
    ///   system's current ANSI code page is used.
    ///   </para>
    ///   <para>
    ///   An UTF-8, little-endian Unicode, and big-endian Unicode encoded text 
    ///   file is automatically recognized, if the file starts with the 
    ///   appropriate byte order marks.
    ///   </para>
    ///   <note>
    ///   If you employ filters in your copy operation, you should limit the copy 
    ///   to text files. Binary files will be corrupted by the copy operation.
    ///   </note>
    /// </remarks>
    /// <example>
    ///   <para>
    ///   Copy a single file while changing its encoding from "latin1" to 
    ///   "utf-8".
    ///   </para>
    ///   <code>
    ///     <![CDATA[
    /// <copy 
    ///     file="myfile.txt"
    ///     tofile="mycopy.txt"
    ///     inputencoding="latin1"
    ///     outputencoding="utf-8" />
    ///     ]]>
    ///   </code>
    /// </example>
    /// <example>
    ///   <para>Copy a set of files to a new directory.</para>
    ///   <code>
    ///     <![CDATA[
    /// <copy todir="${build.dir}">
    ///     <fileset basedir="bin">
    ///         <includes name="*.dll" />
    ///     </fileset>
    /// </copy>
    ///     ]]>
    ///   </code>
    /// </example>
    /// <example>
    ///   <para>
    ///   Copy a set of files to a directory, replacing <c>@TITLE@</c> with 
    ///   "Foo Bar" in all files.
    ///   </para>
    ///   <code>
    ///     <![CDATA[
    /// <copy todir="../backup/dir">
    ///     <fileset basedir="src_dir">
    ///         <include name="**/*" />
    ///     </fileset>
    ///     <filterchain>
    ///         <replacetokens>
    ///             <token key="TITLE" value="Foo Bar" />
    ///         </replacetokens>
    ///     </filterchain>
    /// </copy>
    ///     ]]>
    ///   </code>
    /// </example>
    [TaskName("copy")]
    public class CopyTask : Task {
        #region Private Instance Fields

        private FileInfo _sourceFile;
        private FileInfo _toFile;
        private DirectoryInfo _toDirectory;
        private bool _overwrite;
        private bool _flatten;
        private FileSet _fileset = new FileSet();
        private Hashtable _fileCopyMap = new Hashtable();
        private bool _includeEmptyDirs = true;
        private FilterChain _filters;
        private Encoding _inputEncoding;
        private Encoding _outputEncoding;

        #endregion Private Instance Fields

        #region Public Instance Properties

        /// <summary>
        /// The file to copy.
        /// </summary>
        [TaskAttribute("file")]
        public virtual FileInfo SourceFile {
            get { return _sourceFile; }
            set { _sourceFile = value; }
        }

        /// <summary>
        /// The file to copy to.
        /// </summary>
        [TaskAttribute("tofile")]
        public virtual FileInfo ToFile {
            get { return _toFile; }
            set { _toFile = value; }
        }

        /// <summary>
        /// The directory to copy to.
        /// </summary>
        [TaskAttribute("todir")]
        public virtual DirectoryInfo ToDirectory {
            get { return _toDirectory; }
            set { _toDirectory = value; }
        }

        /// <summary>
        /// Overwrite existing files even if the destination files are newer. 
        /// The default is <see langword="false" />.
        /// </summary>
        [TaskAttribute("overwrite")]
        [BooleanValidator()]
        public bool Overwrite {
            get { return _overwrite; }
            set { _overwrite = value; }
        }
        
        /// <summary>
        /// Ignore directory structure of source directory, copy all files into 
        /// a single directory, specified by the <see cref="ToDirectory" /> 
        /// attribute. The default is <see langword="false" />.
        /// </summary>
        [TaskAttribute("flatten")]
        [BooleanValidator()]
        public virtual bool Flatten {
            get { return _flatten; }
            set { _flatten = value; }
        }

        /// <summary>
        /// Copy any empty directories included in the <see cref="FileSet" />. 
        /// The default is <see langword="true" />.
        /// </summary>
        [TaskAttribute("includeemptydirs")]
        [BooleanValidator()]
        public bool IncludeEmptyDirs {
            get { return _includeEmptyDirs; }
            set { _includeEmptyDirs = value; }
        }

        /// <summary>
        /// Used to select the files to copy. To use a <see cref="FileSet" />, 
        /// the <see cref="ToDirectory" /> attribute must be set.
        /// </summary>
        [BuildElement("fileset")]
        public virtual FileSet CopyFileSet {
            get { return _fileset; }
            set { _fileset = value; }
        }

        /// <summary>
        /// Chain of filters used to alter the file's content as it is copied.
        /// </summary>
        [BuildElement("filterchain")]
        public virtual FilterChain Filters {
            get { return _filters; }
            set { _filters = value; }
        }

        /// <summary>
        /// The encoding to use when reading files. The default is the system's
        /// current ANSI code page.
        /// </summary>
        [TaskAttribute("inputencoding")]
        public Encoding InputEncoding {
            get { return _inputEncoding; }
            set { _inputEncoding = value; }
        }

        /// <summary>
        /// The encoding to use when writing the files. The default is
        /// the encoding of the input file.
        /// </summary>
        [TaskAttribute("outputencoding")]
        public Encoding OutputEncoding {
            get { return _outputEncoding; }
            set { _outputEncoding = value; }
        }

        #endregion Public Instance Properties

        #region Protected Instance Properties

        protected Hashtable FileCopyMap {
            get { return _fileCopyMap; }
        }

        #endregion Protected Instance Properties

        #region Override implementation of Task

        /// <summary>
        /// Checks whether the task is initialized with valid attributes.
        /// </summary>
        /// <param name="taskNode">The <see cref="XmlNode" /> used to initialize the task.</param>
        protected override void InitializeTask(XmlNode taskNode) {
            if (Flatten && ToDirectory == null) {
                throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                    "'flatten' attribute requires that 'todir' has been set."), 
                    Location);
            }

            if (ToDirectory == null && CopyFileSet != null && CopyFileSet.Includes.Count > 0) {
                throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                    "The 'todir' should be set when using the <fileset> element"
                    + " to specify the list of files to be copied."), Location);
            }

            if (SourceFile != null && CopyFileSet != null && CopyFileSet.Includes.Count > 0) {
                throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                    "The 'file' attribute and the <fileset> element" 
                    + " cannot be combined."), Location);
            }

            if (ToFile == null && ToDirectory == null) {
                throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                    "Either the 'tofile' or 'todir' attribute should be set."), 
                    Location);
            }

            if (ToFile != null && ToDirectory != null) {
                throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                    "The 'tofile' and 'todir' attribute cannot both be set."), 
                    Location);
            }
        }

        /// <summary>
        /// Executes the Copy task.
        /// </summary>
        /// <exception cref="BuildException">A file that has to be copied does not exist or could not be copied.</exception>
        protected override void ExecuteTask() {
            // ensure base directory is set, even if fileset was not initialized
            // from XML
            if (CopyFileSet.BaseDirectory == null) {
                CopyFileSet.BaseDirectory = new DirectoryInfo(Project.BaseDirectory);
            }

            // Clear previous copied files
            _fileCopyMap = new Hashtable();

            // copy a single file.
            if (SourceFile != null) {
                if (SourceFile.Exists) {
                    FileInfo dstInfo = null;
                    if (ToFile != null) {
                        dstInfo = ToFile;
                    } else {
                        string dstFilePath = Path.Combine(ToDirectory.FullName, 
                            SourceFile.Name);
                        dstInfo = new FileInfo(dstFilePath);
                    }

                    // do the outdated check
                    bool outdated = (!dstInfo.Exists) || (SourceFile.LastWriteTime > dstInfo.LastWriteTime);

                    if (Overwrite || outdated) {
                        // add to a copy map of absolute verified paths
                        FileCopyMap.Add(SourceFile.FullName, dstInfo.FullName);
                        if (dstInfo.Exists && dstInfo.Attributes != FileAttributes.Normal) {
                            File.SetAttributes(dstInfo.FullName, FileAttributes.Normal);
                        }
                    }
                } else {
                    throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                        "Could not find file '{0}' to copy.", SourceFile.FullName), 
                        Location);
                }
            } else { // copy file set contents.
                // get the complete path of the base directory of the fileset, ie, c:\work\nant\src
                DirectoryInfo srcBaseInfo = CopyFileSet.BaseDirectory;
                
                // if source file not specified use fileset
                foreach (string pathname in CopyFileSet.FileNames) {
                    FileInfo srcInfo = new FileInfo(pathname);
                    if (srcInfo.Exists) {
                        // will holds the full path to the destination file
                        string dstFilePath;

                        if (Flatten) {
                            dstFilePath = Path.Combine(ToDirectory.FullName, 
                                srcInfo.Name);
                        } else {
                            // Gets the relative path and file info from the full 
                            // source filepath
                            // pathname = C:\f2\f3\file1, srcBaseInfo=C:\f2, then 
                            // dstRelFilePath=f3\file1
                            string dstRelFilePath = "";
                            if (srcInfo.FullName.IndexOf(srcBaseInfo.FullName, 0) != -1) {
                                dstRelFilePath = srcInfo.FullName.Substring(
                                    srcBaseInfo.FullName.Length);
                            } else {
                                dstRelFilePath = srcInfo.Name;
                            }
                        
                            if (dstRelFilePath[0] == Path.DirectorySeparatorChar) {
                                dstRelFilePath = dstRelFilePath.Substring(1);
                            }
                        
                            // The full filepath to copy to.
                            dstFilePath = Path.Combine(ToDirectory.FullName, 
                                dstRelFilePath);
                        }
                        
                        // do the outdated check
                        FileInfo dstInfo = new FileInfo(dstFilePath);
                        bool outdated = (!dstInfo.Exists) || (srcInfo.LastWriteTime > dstInfo.LastWriteTime);

                        if (Overwrite || outdated) {
                            FileCopyMap.Add(srcInfo.FullName, dstFilePath);
                            if (dstInfo.Exists && dstInfo.Attributes != FileAttributes.Normal) {
                                File.SetAttributes(dstInfo.FullName, FileAttributes.Normal);
                            }
                        }
                    } else {
                        throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                            "Could not find file '{0}' to copy.", srcInfo.FullName), 
                            Location);
                    }
                }
                
                if (IncludeEmptyDirs && !Flatten) {
                    // create any specified directories that weren't created during the copy (ie: empty directories)
                    foreach (string pathname in CopyFileSet.DirectoryNames) {
                        DirectoryInfo srcInfo = new DirectoryInfo(pathname);
                        // skip directory if not relative to base dir of fileset
                        if (srcInfo.FullName.IndexOf(srcBaseInfo.FullName) == -1) {
                            continue;
                        }
                        string dstRelPath = srcInfo.FullName.Substring(srcBaseInfo.FullName.Length);
                        if (dstRelPath.Length > 0 && dstRelPath[0] == Path.DirectorySeparatorChar) {
                            dstRelPath = dstRelPath.Substring(1);
                        }

                        // The full filepath to copy to.
                        string destinationDirectory = Path.Combine(ToDirectory.FullName, dstRelPath);
                        if (!Directory.Exists(destinationDirectory)) {
                            try {
                                Directory.CreateDirectory(destinationDirectory);
                            } catch (Exception ex) {
                                throw new BuildException(string.Format(CultureInfo.InvariantCulture,
                                "Failed to create directory '{0}'.", destinationDirectory ), 
                                 Location, ex);
                            }
                            Log(Level.Verbose, "Created directory '{0}'.", destinationDirectory);
                        }
                    }
                }
            }

            // do all the actual copy operations now
            DoFileOperations();
        }

        #endregion Override implementation of Task

        #region Protected Instance Methods

        /// <summary>
        /// Actually does the file copies.
        /// </summary>
        protected virtual void DoFileOperations() {
            int fileCount = FileCopyMap.Keys.Count;
            if (fileCount > 0 || Verbose) {
                if (ToFile != null) {
                    Log(Level.Info, "Copying {0} file{1} to '{2}'.", fileCount, (fileCount != 1) ? "s" : "", ToFile);
                } else {
                    Log(Level.Info, "Copying {0} file{1} to '{2}'.", fileCount, (fileCount != 1) ? "s" : "", ToDirectory);
                }

                // loop thru our file list
                foreach (string sourceFile in FileCopyMap.Keys) {
                    string destinationFile = (string) FileCopyMap[sourceFile];
                    if (Flatten) {
                        destinationFile = Path.Combine(ToDirectory.FullName, 
                            Path.GetFileName(destinationFile));
                    }
                    if (sourceFile == destinationFile) {
                        Log(Level.Verbose, "Skipping self-copy of '{0}'.", sourceFile);
                        continue;
                    }

                    try {
                        Log(Level.Verbose, "Copying '{0}' to '{1}'.", sourceFile, destinationFile);
                        
                        // create directory if not present
                        string destinationDirectory = Path.GetDirectoryName(destinationFile);
                        if (!Directory.Exists(destinationDirectory)) {
                            Directory.CreateDirectory(destinationDirectory);
                            Log(Level.Verbose, "Created directory '{0}'.", destinationDirectory);
                        }

                        // copy the file with filters
                        FileUtils.CopyFile(sourceFile, destinationFile, Filters, 
                            InputEncoding, OutputEncoding);
                    } catch (Exception ex) {
                        throw new BuildException(string.Format(CultureInfo.InvariantCulture, 
                            "Cannot copy '{0}' to '{1}'.", sourceFile, destinationFile), 
                            Location, ex);
                    }
                }
            }
        }

        #endregion Protected Instance Methods
    }
}
