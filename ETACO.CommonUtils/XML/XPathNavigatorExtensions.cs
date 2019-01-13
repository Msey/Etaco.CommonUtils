using System.Collections.Generic;
using System.Xml.XPath;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с XPathNavigator </summary>
    public static class XPathNavigatorExtensions
    {
        /// <summary> Получить значение атрибута данного узла </summary>
        public static string GetAttribute(this XPathNavigator navigator, string name, string namespaceURI = "", string defaultValue = "")
        {
            return navigator == null ? defaultValue : navigator.GetAttribute(name, namespaceURI).IfEmpty(defaultValue);
        }

        /// <summary> Получить значение всех атрибутов данного узла </summary>
        public static Dictionary<string, string> GetAllAttributes(this XPathNavigator navigator)
        {
            var result = new Dictionary<string, string>();
            for (var v = navigator.Select("@*"); v.MoveNext(); ) result.Add(v.Current.Name, v.Current.Value);
            return result;
        }

        /// <summary> Получить значение узла </summary>
        public static string GetNodeValue(this XPathNavigator navigator)
        {
            var val = "";
            for (var v = navigator.SelectChildren(XPathNodeType.Text); v.MoveNext(); ) val += v.Current.Value;
            return val;
        }

        /// <summary> Получить значение дочернего узла </summary>
        public static string GetChildValue(this XPathNavigator navigator, string name, string defaultValue)
        {
            for (var v = navigator.SelectChildren(XPathNodeType.Element); v.MoveNext(); ) if (v.Current.Name == name) return v.Current.GetNodeValue();
            return defaultValue;
        }

        /// <summary> Получить значения всех дочерних узлов </summary>
        public static Dictionary<string, string> GetAllChildValues(this XPathNavigator navigator)
        {
            var result = new Dictionary<string, string>();
            for (var v = navigator.SelectChildren(XPathNodeType.Element); v.MoveNext(); ) result.Add(v.Current.Name, v.Current.GetNodeValue());
            return result;
        }

        /// <summary> Получить значения всех атрибутов всех дочерних узлов </summary>
        public static Dictionary<string, Dictionary<string, string>> GetAllChildAttributes(this XPathNavigator navigator)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            for (var v = navigator.SelectChildren(XPathNodeType.Element); v.MoveNext(); ) result.Add(v.Current.Name, v.Current.GetAllAttributes());
            return result;
        }

        /// <summary> Получить значения всех атрибутов дочерних узла </summary>
        public static List<Dictionary<string, string>> GetAllChildAttributes(this XPathNavigator navigator, string name)
        {
            var result = new List<Dictionary<string, string>>();
            for (var v = navigator.Select(name); v.MoveNext(); ) result.Add(v.Current.GetAllAttributes());
            return result;
        }

        /// <summary> Добавить дочерний узел </summary>
        /// <returns> Навигатор для добавленного элемента </returns>
        public static XPathNavigator AddChildNode(this XPathNavigator navigator, string name, bool addIfExist = true)
        {
            var result = navigator;
            foreach (var v in name.Split("/"))
            {
                if (!addIfExist) { var node = result.SelectSingleNode(v); if (node != null) { result = node; continue; } }
                result.AppendChildElement(null, v, null, null);
                result = result.SelectSingleNode(v + "[last()]");
            }
            return result;
        }

        /// <summary> Добавить атрибут </summary>
        /// <returns> Навигатор для добавленного элемента </returns>
        public static XPathNavigator AddAttribute(this XPathNavigator navigator, string name, string val = "")
        {
            navigator.CreateAttribute("", name, "", val);
            return navigator.SelectSingleNode("@" + name);
        }
    }
}