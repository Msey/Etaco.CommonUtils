using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace ETACO.CommonUtils
{
    /// <summary>Выполнение преобразования xml с использованием xsl</summary>
    public class XslTransformer
    {
        //XslTransform - не поддерживает скрипты C#
        //XslCompiledTransform - требует компиляции, поэтому лучше повторно использовать
        private readonly XslCompiledTransform transformer = new XslCompiledTransform();

        public XslTransformer(string xsl)
        {
            transformer.Load(XmlReader.Create(new StringReader(xsl)), new XsltSettings(true, true), new XmlUrlResolver());
        }

        /// <summary>Выполнить преобразование xml</summary>
        public string Transform(string xml)
        {
            if (xml.IsEmpty()) return "";
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml.Trim());
            using (var sw = new StringWriter())
            {
                transformer.Transform(new XmlNodeReader(xmlDoc), null, sw);
                return sw.ToString();
            }
        }
    }
}