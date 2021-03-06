﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CommandLine;
using Xml.Schema.Linq.Extensions;

namespace LinqToXsd
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal abstract class OptionsAbstract
    {
        internal const string OutputHelpText = "Output file name or folder. When specifying multiple input XSDs or input folders, and this value is a file, all output is merged into a single file. If this value is a folder, multiple output files are output to this folder.";
        internal const string FilesOrFoldersHelpText = "One or more schema files or folders containing schema files. Separate multiple files using a comma (,). If folders are given, then the files referenced in xs:include or xs:import directives are not imported twice. Usage: 'LinqToXsd [verb] <file1.xsd>,<file2.xsd>' or 'LinqToXsd [verb] <folder1>,<folder2>'.";

        /// <summary>
        /// Determines if at least one folder path was given in <see cref="FilesOrFolders"/>.
        /// </summary>
        /// <returns></returns>
        public bool FoldersWereGiven => FileSystemUtilities.HasFolderPaths(FilesOrFolders);

        /// <summary>
        /// Determines if at least one file path were given in <see cref="FilesOrFolders"/>.
        /// </summary>
        /// <returns></returns>
        public bool FilesWereGiven => FileSystemUtilities.HasFilePaths(FilesOrFolders);

        private List<string> filesOrFolders = new List<string>();

        /// <summary>
        /// CLI argument: The file or folder paths given at the CL.
        /// </summary>
        [Value(1, HelpText = FilesOrFoldersHelpText, Required = true)]
        public IEnumerable<string> FilesOrFolders
        {
            get => filesOrFolders;
            set
            {
                // this fixes a curious issue in the CommandLine parser that sometimes pops up
                var possibleUnparsedCommas = value
                                             .Select(v => v.Replace("\\", @"\"))
                                             .SelectMany(pf => pf.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                                             .Select(v => v.Trim('\\', '/')); // removes trailing slashes for directories
                filesOrFolders = possibleUnparsedCommas.ToList();
            }
        }

        /// <summary>
        /// Resolves the file or folder paths in <see cref="FilesOrFolders"/> property as just files, filtering to only include *.xsd files under
        /// any folder paths present.
        /// </summary>
        public IEnumerable<string> SchemaFiles
        {
            get
            {
                var files = FileSystemUtilities.ResolveFileAndFolderPathsToJustFiles(FilesOrFolders, "*.xsd");
                // convert files to XDocuments and check if they are proper W3C schemas
                var xDocs = files.Select(f => new KeyValuePair<string, XDocument>(f, XDocument.Load(f)))
                                 .Where(kvp => kvp.Value.IsAnXmlSchema())
                                 .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var filteredIncludeAndImportRefs = xDocs.FilterOutSchemasThatAreIncludedOrImported().Select(kvp => kvp.Key);

                return files.Except(filteredIncludeAndImportRefs).Distinct();
            }
        }

        /// <summary>
        /// Returns <see cref="XmlReader"/> instances of every file specified in <see cref="SchemaFiles"/>.
        /// </summary>
        public virtual Dictionary<string, XmlReader> SchemaReaders
        {
            get
            {
                var schemasFiles = SchemaFiles.ToArray(); // save a reference otherwise it gets enumerated twice
                if (!schemasFiles.Any()) return new Dictionary<string, XmlReader>();

                var xmlReaderSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse
                };
                return schemasFiles.ToDictionary(f => f, f => XmlReader.Create(f, xmlReaderSettings));
            }
        }

        /// <summary>
        /// CLI argument: output file name or folder.
        /// </summary>
        [Option('o', nameof(Output), HelpText = OutputHelpText)]
        public virtual string Output { get; set; }
    }
}
