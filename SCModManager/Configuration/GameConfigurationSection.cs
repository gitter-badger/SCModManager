﻿using PDXModLib.Interfaces;
using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCModManager.Configuration
{
    public class GameConfigurationSection : ConfigurationSection, IGameConfiguration
    {
        public string BasePath
        {
            get { return (string)this["BasePath"]; }
            set { this["BasePath"] = value; }
        }

        public string ModsDir
        {
            get { return (string)this["BasePath"]; }
            set { this["BasePath"] = value; }
        }

        public string SettingsPath
        {
            get { return (string)this["SettingsPath"]; }
            set { this["SettingsPath"] = value; }
        }

        public string BackupPath
        {
            get { return (string)this["BackupPath"]; }
            set { this["BackupPath"] = value; }
        }

        public string SavedSelections
        {
            get { return (string)this["SavedSelections"]; }
            set { this["SavedSelections"] = value; }
        }

        public IReadOnlyCollection<string> WhiteListedFiles => WhiteListedFilesConfigSection;

        [ConfigurationProperty("WhiteListedFiles")]
        public WhiteListedFileCollection WhiteListedFilesConfigSection
        {
            get { return (WhiteListedFileCollection)this[WhiteListedFileCollection.WhiteListedFiles]; }
            set { this[WhiteListedFileCollection.WhiteListedFiles] = value; }
        }
    }

    public class WhiteListedFileCollection : ConfigurationElementCollection, ICollection<string>, IReadOnlyCollection<string>
    {
        public const string WhiteListedFiles = "WhiteListedFiles";
        bool ICollection<string>.IsReadOnly => throw new NotImplementedException();

        public void Add(string item)
        {
            this.BaseAdd(new WhiteListedFileElement(item));
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public bool Contains(string item)
        {
            return BaseGetAllKeys().Any(k => (k as string) == item);
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            this.BaseGetAllKeys().OfType<string>().ToList().CopyTo(array, arrayIndex);
        }

        public bool Remove(string item)
        {
            var result = Contains(item);
            BaseRemove(item);
            return result;
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new WhiteListedFileElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as WhiteListedFileElement)?.FileName;
        }

        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            return BaseGetAllKeys().OfType<string>().GetEnumerator();
        }

        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.BasicMap;

        protected override string ElementName => WhiteListedFileElement.ElementName;
    }

    public class WhiteListedFileElement : ConfigurationElement
    {
        public const string ElementName = "file";
        private const string PropertyName = "name";

        internal WhiteListedFileElement()
        {
        }

        public WhiteListedFileElement(string fileName)
        {
            FileName = fileName;
        }

        [ConfigurationProperty(PropertyName, IsRequired = true, IsKey = true, DefaultValue = "")]
        public string FileName
        {
            get { return (string)this[PropertyName]; }
            set { this[PropertyName] = value; }
        }
    }
}
