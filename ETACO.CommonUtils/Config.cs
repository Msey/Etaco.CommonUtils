using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace ETACO.CommonUtils
{
    /// <summary> Хранение параметров конфигурации </summary>
    /// <remarks> можно использовать параметр командной строки -cfg:file для указания имени конфигурационного файла</remarks>
    public class Config : MarshalByRefObject
    {
        private readonly object _lock = new object();
        private readonly StringCrypter stringCrypter = new StringCrypter();
        private readonly XmlDocument xmlDoc = new XmlDocument();
        private XPathNavigator xmlNav = null;
        private readonly Dictionary<string, string> parameters = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public readonly Dictionary<string, Config> SubConfigs = new Dictionary<string, Config>();
        public static string DefaultConfigFileName { get { return AppContext.AppFullFileName + ".cfg"; } } //для получения App.AppFullFileName может не хватить прав (например при использовании в web приложении)
        public string ConfigFileName { get; private set; }

        /// <summary> Событие добавления, изменения, удаления параметра (module, name, isDeleted, value)</summary>
        public event Action<string, string, bool, string> OnValueChanged;

        public Config()
        {
            xmlNav = GetXmlNav();
            ParseCommandLine();
            OnValueChanged += OnConfigParameterChanged;
        }

        private XPathNavigator GetXmlNav()
        {
            if (xmlDoc.DocumentElement == null)
            {
                xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", ""));
                xmlDoc.AppendChild(xmlDoc.CreateNode(XmlNodeType.Element, "cfg", "")).AppendChild(xmlDoc.CreateNode(XmlNodeType.Element, "parameters", ""));
            }
            return xmlDoc.DocumentElement.CreateNavigator();
        }

        /// <summary> Загрузить параметры из командной страны </summary>
        private void ParseCommandLine()
        {
            Array.ForEach(Environment.GetCommandLineArgs(), v =>
            {
                var m = Regex.Match(v, @"^-db:(.*)\/(.*)@(.*)$");
                if (m.Success)
                {
                    parameters["db/login"] = m.Groups[1].Value;
                    parameters["db/password"] = m.Groups[2].Value;
                    parameters["db/server"] = m.Groups[3].Value;
                }
                else
                {
                    m = Regex.Match(v, @"^-(.*):(.*)=(.*)$");
                    if (m.Success) parameters[m.Groups[1].Value + "/" + m.Groups[2].Value] = m.Groups[3].Value;
                }
            });
        }

        /// <summary> Возвращает параметр конфигурации (без каких либо преобразований) </summary> //path=cfg/log
        public string GetValue(string path, string defaultValue = "", bool decrypt = false)
        {
            lock (_lock)
            {
                var v = parameters.GetValue(path, null);
                if (v == null && SubConfigs.Count == 0) return defaultValue;
                //subConfig
                if (v == null)
                {
                    foreach (var sc in SubConfigs.Values) { v = sc.parameters.GetValue(path, null); if (v != null) break; }
                    if (v == null) return defaultValue;
                }
                //
                if (!decrypt || v.IsEmpty()) return v;
                try
                {
                    return stringCrypter.DecryptFromBase64(v);
                }
                catch (Exception ex) //FormatException
                {
                    throw new Exception("Illegal format of encrypted param {0}.".FormatStr(path), ex);
                }
            }
        }

        /// <summary> Возвращает параметр конфигурации (без каких либо преобразований) </summary>
        /// <param name="module"> имя модуля </param>
        /// <param name="name"> имя параметра </param>
        /// <param name="defaultValue"> значение по умолчанию </param>
        /// <param name="decrypt"> нужно ли расшифровывать значение </param>
        // Set делать не надо, ибо тип значения string и сокращённая запись нужна чаще при получении значения, а не его установке. 
        public string this[string module, string name, string defaultValue = "", bool decrypt = false]
        {
            get { return GetValue(module + "/" + name, defaultValue, decrypt); }
        }

        /// <summary> Возвращает  параметр конфигурации (при необходимости - вычисляет)</summary>
        /// <typeparam name="T">тип возвращаемого значения </typeparam>
        /// <param name="module">имя модуля </param>
        /// <param name="name">имя параметра </param>
        /// <param name="defaultValue">значение по умолчанию </param>
        /// <param name="decrypt">нужно ли расшифровывать значение </param>
        /// <remarks> Для хранения flags используется запись : Info|Error
        /// для массивов rank = 1 можно указывать произвольный тип, для rank > 1 тип - object[] - строка конфига  42,[19, true],[56,'test']"</remarks>
        public T GetParameter<T>(string module, string name, T defaultValue = default(T), bool decrypt = false)
        {
            var result = this[module, name, null, decrypt];
            if (result == null) return defaultValue;

            try
            {
                return result.GetValue<T>("|", this);
            }
            catch
            {
                throw new Exception("Can't convert value '{0}' to type '{1}'. ({2}:{3})".FormatStr(result, typeof(T), module, name));
            }
        }

        /// <summary> Устанавливает значение парамета конфигурации </summary>
        /// <param name="module">имя модуля </param>
        /// <param name="name">имя параметра </param>
        /// <param name="newValue">новое значение </param>
        /// <param name="encrypt">нужно ли шифровать </param>
        /// <param name="replaceIfExist">заменять существующее значение </param>
        /// <returns>предыдущее значение параметра конфигурации </returns>
        public string SetParameter(string module, string name, object newValue, bool encrypt = false, bool replaceIfExist = true)
        {
            lock (_lock)
            {
                var result = this[module, name, null];
                if (result == null || replaceIfExist)
                {
                    Type t = newValue == null ? null : newValue.GetType();
                    var v = "";
                    if (t != null && t.IsEnum) v = ("" + newValue).Replace(",", "|");
                    else if (t != null && t.IsArray && t.GetArrayRank() == 1) { foreach (var i in newValue as IEnumerable) v += (v.IsEmpty() ? "" : ", ") + i; }
                    else v = newValue + "";

                    v = encrypt ? stringCrypter.EncryptToBase64(v) : v;
                    parameters[module + "/" + name] = v;
                    OnValueChanged?.Invoke(module, name, false, v);
                }
                return result + "";
            }
        }

        /// <summary> Удаление парамета конфигурации </summary>
        public bool RemoveParameter(string module, string name)
        {
            lock (_lock)
            {
                var result = parameters.Remove(module + "/" + name);
                if (result && OnValueChanged != null) OnValueChanged(module, name, true, null);
                return result;
            }
        }

        /// <summary>Возвращает список дочерних модулей</summary>
        /// <param name="module">Название родительского модуля (root="")</param>
        /// <param name="deep">Глубина вложенности (по умолчанию 0=все дочерние)</param>
        /// <returns>Массив строк с названиями модулей</returns>
        public string[] GetModules(string module = "", int deep = 0)
        {
            lock (_lock)
            {
                var v = module.IsEmpty() ? 0 : module.Count((c) => c == '/') + 1;
                var r = (from key in parameters.Keys
                         where key.StartsWith(module + "/", StringComparison.InvariantCultureIgnoreCase)
                         && key.Count((c) => c == '/') > v
                         && (key.Count((c) => c == '/') <= v + deep || deep == 0)
                         select key.Substring(0, key.LastIndexOf('/'))).Distinct().ToArray();
                /*subConfig*/
                if (r.Length == 0) foreach (var sc in SubConfigs.Values) { r = sc.GetModules(module, deep); if (r.Length > 0) break; }
                return r;
            }
        }

        /// <summary>Возвращает список параметров модуля</summary>
        /// <param name="module">Название  модуля</param>
        public string[] GetModuleParameters(string module)
        {
            lock (_lock)
            {
                var v = module.Length;
                var r = (from key in parameters.Keys
                         where key.StartsWith(module + "/", StringComparison.InvariantCultureIgnoreCase)
                         && key.LastIndexOf('/') == v
                         select key.Substring(v + 1)).ToArray();
                /*subConfig*/
                if (r.Length == 0) foreach (var sc in SubConfigs.Values) { r = sc.GetModuleParameters(module); if (r.Length > 0) break; }
                return r;
            }
        }

        /// <summary> Удалить модуль, его параметры и дочерние модули </summary>
        /// <param name="module">Имя модуля для удаления (пустая строка - все модули)</param>
        public void RemoveModule(string module)
        {
            lock (_lock)
            {
                Array.ForEach((from key in parameters.Keys where key.StartsWith(module + "/", StringComparison.InvariantCultureIgnoreCase) select key).ToArray(),
                     (v) => { int i = v.LastIndexOf('/'); RemoveParameter(v.Substring(0, i), v.Substring(i + 1)); });
            }
        }

        /// <summary> Возвращает секцию конфигурационного файла </summary>
        public XPathNavigator GetSection(string sectionName, bool throwException = false)
        {
            lock (_lock)
            {
                var r = xmlNav.SelectSingleNode(sectionName);
                /*subConfig*/
                if (r == null) foreach (var sc in SubConfigs.Values) { r = sc.GetSection(sectionName); if (r != null) break; }
                if (r == null && throwException) throw new Exception("Config section '{0}' not exist.".FormatStr(sectionName));
                return r;
            }
        }

        /// <summary> Загрузить параметры из конфигурационного файла </summary>
        public Config LoadConfigFile(string fileName = null, bool replaceIfExist = false)
        {
            lock (_lock)
            {
                fileName = !string.IsNullOrEmpty(fileName) ? fileName : (this["cfg", "file", DefaultConfigFileName]);
                if (!File.Exists(fileName)) throw new Exception("Can't find config file: " + fileName);

                try
                {
                    OnValueChanged -= OnConfigParameterChanged;

                    xmlDoc.Load(fileName);
                    xmlNav = GetXmlNav();
                    ConfigFileName = fileName;
                    ParseConfigFileParams("", GetSection("parameters", true), replaceIfExist);

                    return this;
                }
                catch (Exception e)
                {
                    throw new Exception("Can't load config file: " + fileName, e);
                }
                finally
                {
                    OnValueChanged += OnConfigParameterChanged;
                }
            }
        }

        private void ParseConfigFileParams(string prefix, XPathNavigator navigator, bool replaceIfExist = false)
        {
            for (var childNodes = navigator.SelectChildren(XPathNodeType.Element); childNodes.MoveNext(); )
            {
                var module = prefix + childNodes.Current.Name;

                foreach (var attr in childNodes.Current.GetAllAttributes()) SetParameter(module, attr.Key, attr.Value, false, replaceIfExist);

                foreach (var attr in childNodes.Current.GetAllChildValues()) if (attr.Value.Trim() != "") SetParameter(module, attr.Key, attr.Value, false, replaceIfExist);

                ParseConfigFileParams(module + "/", childNodes.Current, replaceIfExist);  // обработка вложенных параметров
            }
        }

        /// <summary> Сохранить параметры в файл </summary>
        /// <param name="fileName"> имя файла (по умолчанию сохраняется в тот же файл, из готорого были загруженны параметры)</param>
        public void SaveConfigFile(string fileName = null)
        {
            lock (_lock)
            {
                fileName = fileName.IfEmpty(ConfigFileName);
                if (fileName.IsEmpty()) throw new Exception("Config file name is empty.");
                xmlDoc.Save(fileName);
            }
        }

        private XPathNavigator Find(string module, string name)
        {
            lock (_lock)
            {
                return xmlNav.SelectSingleNode("parameters/{0}/@{1}".FormatStr(module, name)) ?? xmlNav.SelectSingleNode("parameters/{0}/{1}".FormatStr(module, name));
            }
        }

        private void OnConfigParameterChanged(string module, string name, bool isDeleted, string value)
        {
            if (isDeleted)
            {
                var v = Find(module, name);
                v.DeleteSelf(); //после удаления автоматически указывает на родительский элемент
                while (!v.HasChildren && !v.HasAttributes && !v.Matches("parameters")) v.DeleteSelf();
            }
            else
            {
                var v = xmlNav.SelectSingleNode("parameters").AddChildNode(module, false);
                var attr = v.SelectSingleNode("@" + name);
                var node = v.SelectSingleNode(name);

                if (XMLUtils.IsAttributeValue(value))
                {
                    if (attr != null) attr.SetValue(value); else v.CreateAttribute(null, name, null, value);
                    if (node != null) node.DeleteSelf();
                }
                else
                {
                    if (node == null) v.AppendChildElement(null, name, null, XMLUtils.IsCDATAValue(value) ? "" : value);
                    if (XMLUtils.IsCDATAValue(value)) using (var writer = v.SelectSingleNode(name).AppendChild()) writer.WriteCData(value);
                    if (attr != null) attr.DeleteSelf();
                }
            }
        }
    }
}