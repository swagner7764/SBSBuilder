using System.Configuration;
using System.Xml;

namespace SBSBuilder.Config
{
    public class SfxConfig : IConfigurationSectionHandler
    {
        public string ExeXmlConfig { get; private set; }

        public object Create(object parent, object configContext, XmlNode section)
        {
            ExeXmlConfig = section.OuterXml;
            return this;
        }
    }
}