using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml;

namespace SBSBuilder.Config
{
    public class BuildDefaultsSection : Dictionary<string, string>, IConfigurationSectionHandler
    {
        private readonly Dictionary<string, BuildTarget> _targets =
            new Dictionary<string, BuildTarget>(StringComparer.OrdinalIgnoreCase);

        public BuildDefaultsSection() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public new BuildTarget this[string type]
        {
            get { return _targets.ContainsKey(type) ? _targets[type] : null; }
        }

        public ICollection<BuildTarget> Targets
        {
            get { return _targets.Values; }
        }

        public object Create(object parent, object configContext, XmlNode section)
        {
            var nodes = section.SelectNodes("add");
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node.Attributes == null) continue;
                    var key = node.Attributes["key"].Value;
                    var value = node.Attributes["value"].Value;
                    Add(key, value);
                }
            }

            nodes = section.SelectNodes("target");
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var target = new BuildTarget(nodes[i]);
                    _targets.Add(target.Type, target);
                }
            }
            return this;
        }

        public Dictionary<string, string> PropertiesForTarget(string target)
        {
            if (!_targets.ContainsKey(target))
                return this;
            return this.Concat(_targets[target].Where(kvp => !ContainsKey(kvp.Key)))
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }


    public class BuildTarget : Dictionary<string, string>
    {
        internal BuildTarget(XmlNode section)
        {
            if (section.Attributes == null) return;

            Type = section.Attributes["type"].Value;
            var nodes = section.SelectNodes("add");
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node.Attributes == null) continue;
                    var key = node.Attributes["key"].Value;
                    var value = node.Attributes["value"].Value;
                    Add(key, value);
                }
            }
        }

        public string Type { get; internal set; }
    }
}