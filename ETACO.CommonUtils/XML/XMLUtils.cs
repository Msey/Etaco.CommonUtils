using System;
using System.IO;
using System.Text;
using System.Xml;

namespace ETACO.CommonUtils
{
    /// <summary> Работа с xml</summary>
    public static class XMLUtils
    {
        /// <summary> Форматирование текста xml </summary>
        public static string Format(string xml)
        {
            if (xml.IsEmpty()) return "";
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml.ReplaceAny("", Environment.NewLine, "\t", "\n").Trim());
            using (var ms = new MemoryStream())
            {
                var decl = xmlDoc.FirstChild;  //XmlDeclaration либо первый, либо его нет (формат такой)
                var encoding = (decl.NodeType == XmlNodeType.XmlDeclaration && !((XmlDeclaration)decl).Encoding.IsEmpty()) ? Encoding.GetEncoding(((XmlDeclaration)decl).Encoding) : ms.GetBuffer().GetEncoding();
                using (var xtw = new XmlTextWriter(ms, encoding) { Formatting = Formatting.Indented })
                {
                    xmlDoc.Save(xtw);
                    return encoding.GetString(ms.GetBuffer()).Trim();
                }
            }
        }

        public static string Decode(string xml)
        {
            return xml.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
            //var xmlDoc = new XmlDocument(); xmlDoc.LoadXml("<x>" + xml + "</x>"); return xmlDoc.InnerText;
        }

        public static string Encode(string xml)
        {
            return xml.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        /// <summary> может ли данный текст храниться в качестве xml атрибута </summary>
        public static bool IsAttributeValue(string value)
        {
            return value == null || (!IsCDATAValue(value)) && (!IsNodeValue(value));
        }

        /// <summary> нужно ли данный текст хранить в качестве xml элемента типа CDATA </summary>
        public static bool IsCDATAValue(string value)
        {
            return value != null && value.ContainsAny(StringComparison.InvariantCulture, "<", "&", ">");
        }

        /// <summary> нужно ли данный текст хранить в качестве xml узла </summary>
        public static bool IsNodeValue(string value)
        {
            return value != null && ((!IsCDATAValue(value)) && (value.ContainsAny(StringComparison.InvariantCulture, "\"", Environment.NewLine) || (value.Length > 256)));
        }
    }
}