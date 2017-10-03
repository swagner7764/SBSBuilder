﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SBSBuilder.Config
{
    public class Properties
    {
        private string _filename;
        private Dictionary<string, string> _list;

        public Properties(string file)
        {
            Reload(file);
        }

        public string Get(string field, string defValue)
        {
            return (Get(field) == null) ? (defValue) : (Get(field));
        }

        public string Get(string field)
        {
            return (_list.ContainsKey(field)) ? (_list[field]) : (null);
        }

        public void Set(string field, object value)
        {
            if (!_list.ContainsKey(field))
                _list.Add(field, value.ToString());
            else
                _list[field] = value.ToString();
        }

        public void Save()
        {
            Save(_filename);
        }

        public void Save(string filename)
        {
            _filename = filename;

            if (!File.Exists(filename))
                File.Create(filename);

            var attr = File.GetAttributes(filename);

            // unset read-only
            Console.WriteLine(new FileInfo(filename).FullName);
            attr = attr & ~FileAttributes.ReadOnly;
            File.SetAttributes(filename, attr);
            using (var file = new StreamWriter(filename))
            {
                foreach (var prop in _list.Keys.ToArray())
                    if (!string.IsNullOrWhiteSpace(_list[prop]))
                        file.WriteLine(prop + "=" + _list[prop]);
            }
        }

        public void Reload()
        {
            Reload(_filename);
        }

        public void Reload(string filename)
        {
            _filename = filename;
            _list = new Dictionary<string, string>();

            if (File.Exists(filename))
                LoadFromFile(filename);
            else
                File.Create(filename);
        }

        private void LoadFromFile(string file)
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if ((!string.IsNullOrEmpty(line)) &&
                    (!line.StartsWith(";")) &&
                    (!line.StartsWith("#")) &&
                    (!line.StartsWith("'")) &&
                    (line.Contains('=')))
                {
                    var index = line.IndexOf('=');
                    var key = line.Substring(0, index).Trim();
                    var value = line.Substring(index + 1).Trim();

                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    try
                    {
                        //ignore dublicates
                        _list.Add(key, value);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }
}