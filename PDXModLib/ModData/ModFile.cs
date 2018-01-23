﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ionic.Zip;
using NLog;
using PDXModLib.Interfaces;
using PDXModLib.SCFormat;
using PDXModLib.Utility;

namespace PDXModLib.ModData
{
    public abstract class ModFile 
    {
        private static readonly string[] SCExtensions = { ".gfx", ".gui", ".txt" };

        private static readonly string[] CodeExtensions = { ".lua" };

        private static readonly string[] LocalisationExtensions = { ".yml" };

        public string Path { get; set; }

        public string Directory => System.IO.Path.GetDirectoryName(Path);
        public string Filename => System.IO.Path.GetFileName(Path);

        protected static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public Mod SourceMod { get; }

        protected ModFile(string path, Mod sourceMod)
        {
            Path = path;
            SourceMod = sourceMod;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ModFile;
            return other != null && string.Compare(other.Path, this.Path, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public abstract string RawContents { get; }

        internal static ModFile Load(IModFileLoader loader, string path , Mod sourceMod)
        {
            if (SCExtensions.Contains(System.IO.Path.GetExtension(path).ToLower()))
            {
                return new SCModFile(loader, path, sourceMod);
            }

            if (CodeExtensions.Contains(System.IO.Path.GetExtension(path).ToLower()))
            {
                return new CodeModFile(loader, path, sourceMod);
            }

            if (LocalisationExtensions.Contains(System.IO.Path.GetExtension(path).ToLower()))
            {
                return new LocalisationFile(loader, path, sourceMod);
            }

            return new BinaryModFile(loader, path, sourceMod);
        }

        protected static string NormalizeLineEndings(string source)
        {
            return Regex.Replace(source, @"\r\n|\n\r|\n|\r", Environment.NewLine);
        }

        public virtual void Save(IModFileSaver saver)
        {
            saver.Save(Path, RawContents);
        }
    }

    internal class SCModFile : ModFile
    {
        private readonly IModFileLoader _loader;
        private string _rawContents;
        internal SCObject Contents { get; private set; }

        public override string RawContents
        {
            get
            {
                if (_rawContents == null)
                {
                    _rawContents = LoadSCFileContents(_loader);
                }
                return _rawContents;
            }
        }

        internal bool ParseError { get; private set; }

        public SCModFile(IModFileLoader loader, string path, Mod sourceMod)
            : base(path, sourceMod)
        {
            _loader = loader;
        }

        private string LoadSCFileContents(IModFileLoader loader)
        {
            using (var stream = loader.OpenStream())
            {

                using (var mr = new MemoryStream())
                {
                    byte[] buffer = new byte[1024];
                    int size = 0;
                    while (stream.CanRead && ((size = stream.Read(buffer, 0, 1024)) > 0))
                    {
                        mr.Write(buffer, 0, size);
                    }

                    mr.Seek(0, SeekOrigin.Begin);

                    var parser = new Parser(new Scanner(mr));

                    parser.Parse();

                    mr.Seek(0, SeekOrigin.Begin);

                    using (var sr = new StreamReader(mr))
                    {
                        ParseError = parser.ParseError;
                        Contents = parser.Root;
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }

    internal class CodeModFile : ModFile
    {
        private readonly IModFileLoader _loader;
        public string Contents { get; set; }
        private string _rawContents;

        public override string RawContents
        {
            get
            {
                if (_rawContents == null)
                {
                    using (var stream = _loader.OpenStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            _rawContents = NormalizeLineEndings(sr.ReadToEnd());
                        }
                    }
                }
                return _rawContents;
            }
        }

        public CodeModFile(IModFileLoader loader, string path, Mod sourceMod)
            : base(path, sourceMod)
        {
            _loader = loader;
        }
    }

    internal class LocalisationFile : ModFile
    {
        private readonly IModFileLoader _loader;
        public string Contents { get; set; }
        private string _rawContents;

        public override string RawContents
        {
            get
            {
                if (_rawContents == null)
                {
                    using (var stream = _loader.OpenStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            _rawContents = NormalizeLineEndings(sr.ReadToEnd());
                        }
                    }
                }
                return _rawContents;
            }
        }

        public LocalisationFile(IModFileLoader loader, string path, Mod sourceMod)
            : base(path, sourceMod)
        {
            _loader = loader;
        }
    }

    internal class BinaryModFile : ModFile
    {
        private readonly IModFileLoader _loader;
        public string Contents => "BinaryModFile";
        public override string RawContents => Contents;

        public BinaryModFile(IModFileLoader loader, string path, Mod sourceMod)
            : base(path, sourceMod)
        {
            _loader = loader;
        }

        public override void Save(IModFileSaver saver)
        {
            using (var stream = _loader.OpenStream())
            {
                saver.Save(Path, stream);
            }
        }
    }

    public class MergedModFile : ModFile
    {
        string contents;

        public override string RawContents => contents;

        public List<ModFile> SourceFiles
        {
            get;
        }

        public int SourceFileCount => SourceFiles.Count;

        public bool Resolved { get; set; }

        public MergedModFile(string path, IEnumerable<ModFile> source, Mod sourceMod)
            :base(path, sourceMod)
        {
            SourceFiles = source.ToList();
        }

        public void SaveResult(string toSave)
        {
            contents = toSave;
        }

        public override void Save(IModFileSaver saver)
        {
            if (Resolved)
                base.Save(saver);
            else
            {
                var stream = CreateMergeZip();
                var extension = System.IO.Path.GetExtension(Path);
                var newPath = System.IO.Path.ChangeExtension(Path, $"{extension}.mzip");
                saver.Save(newPath, stream);
            }
        }

        private MemoryStream CreateMergeZip()
        {
            var result = new MemoryStream();
            using (var saver = new MergeZipFileSaver())
            {
                foreach (var sourceFile in SourceFiles)
                {
                    sourceFile.Save(saver);
                }

                saver.ZipFile.Save(result);
            }

            return result;
        }

        private class MergeZipFileSaver : IModFileSaver
        {
            private int _index;
            public ZipFile ZipFile { get; }

            public MergeZipFileSaver()
            {
                ZipFile = new ZipFile();
            }

            public void Save(string path, Stream stream)
            {
                ZipFile.AddEntry(GetPath(path), stream);
            }

            public void Save(string path, string text)
            {
                ZipFile.AddEntry(GetPath(path), text);
            }

            public void Dispose()
            {
                ZipFile?.Dispose();
            }

            private string GetPath(string path)
            {
                var filename = System.IO.Path.GetFileName(path);
                var i = _index++;
                return $"{i:00}/{filename}";
            }
        }
    }
}
