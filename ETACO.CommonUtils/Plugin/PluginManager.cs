using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using ETACO.CommonUtils.Plugin;
using ETACO.CommonUtils.Script;

namespace ETACO.CommonUtils
{
    /// <summary> Управление plugin </summary>
    [Serializable]
    public class PluginManager
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, PluginInfo> _plugins = new Dictionary<string, PluginInfo>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, object> _pluginsInstance = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary> Добавить plugin </summary>    
        public void AddPlugin(string name, Type type, string description = null, bool ignoreIfExist = false)
        {
            var v = _plugins.GetValue(name);
            if (v != null && (v.Type.FullName != type.FullName || v.Description != description + ""))
            {
                if (ignoreIfExist) return;
                throw new ArgumentException("Plugin '" + name + "' already exist.");
            }
            _plugins[name] = new PluginInfo(name, type, description);
        }

        /// <summary> Удалить plugin </summary>    
        public bool RemovePlugin(string name)
        {
            return _plugins.Remove(name);
        }

        /// <summary> Получить список plugin данного типа</summary>    
        public List<PluginInfo> GetPluginsInfo<T>() { return GetPluginsInfo(typeof(T));}
        public List<PluginInfo> GetPluginsInfo(Type type = null)
        {
            return (type == null) ? _plugins.Select(v => v.Value).ToList() : _plugins.Where(v => type.IsAssignableFrom(v.Value.Type)).Select(v => v.Value).ToList();
        }

        /// <summary> Получить plugin </summary>
        public T GetPlugin<T>(string name)
        {
            lock (_lock)
            {
                object plugin = null;
                _pluginsInstance.TryGetValue(name, out plugin);
                if (plugin == null)
                {
                    int i = name.IndexOf(':');
                    plugin = i < 0 ? CreateInstance<T>(name) : CreateInstance<T>(name.Substring(0, i), name.Substring(i + 1));
                    _pluginsInstance.Add(name, plugin);
                    return (T)plugin;
                }
                return (T)plugin;
            }
        }
        /// <summary> Есть ли плагин с типом T и именем name </summary>
        public bool Contains<T>(string name) { return GetPluginsInfo<T>().FirstOrDefault(v => v.Name == name) != null; }

        /// <summary> Создать новый экземпляр plugin </summary>
        public T CreateInstance<T>(string name, params object[] args)
        {
            if (args == null) args = new object[] { null };
            if (!_plugins.ContainsKey(name))
            {
                var type = AppContext.GetType(name);
                if (type == null) throw new ArgumentException("Plugin '" + name + "' not found.");
                _plugins.Add(name, new PluginInfo(name, type));
            }
            return (T)Activator.CreateInstance(_plugins[name].Type, args);
        }

        /// <summary> Загрузить plugin из текущей сборки</summary>
        public void LoadFromExecutingAssembly(bool ignoreIfExist = false)
        {
            Load(Assembly.GetCallingAssembly(), ignoreIfExist);
        }

        /// <summary> Загрузить plugin из сборки</summary>
        public void Load(Assembly assembly, bool ignoreIfExist = false)
        {
            var types = assembly.GetTypes();
            foreach (var t in types)
            {
                if (t.IsClass && !t.IsAbstract)
                {
                    var pa = (PluginAttribute)Attribute.GetCustomAttribute(t, typeof(PluginAttribute), true);
                    var assemblyName = assembly.GetName().Name;
                    if (pa != null)
                    {
                        try
                        {
                            AddPlugin(pa.Name, t, pa.Description, ignoreIfExist);
                            AppContext.Log.Info("Add plugin: {0,-24} [Assembly = {1}] [Description = {2}]".FormatStr(pa.Name, assemblyName, pa.Description));
                        }
                        catch (Exception ex)
                        {
                            AppContext.Log.Error("Add plugin: {0,-24} [Assembly = {1} CodeBase={2}] Exception: {3}".FormatStr(pa.Name, assemblyName, assembly.GetName().CodeBase, ex.Message));
                        }
                    }
                }
            }
        }

        /// <summary> Загрузить plugin из потока</summary>
        public void Load(Stream stream, bool ignoreIfExist = false)//при использовании такого подходя теряется информация о Location и CodeBase для Assembly
        {
            if (stream == null) return;
            var ms = stream as MemoryStream;
            if(ms!= null) Load(Assembly.Load(ms.ToArray()), ignoreIfExist);
            else using (ms = new MemoryStream())
            {
                stream.CopyTo(ms, 4096);
                Load(Assembly.Load(ms.ToArray()), ignoreIfExist);
                ms.Close();
            }
        }

        /// <summary> Загрузить plugin из файлов</summary>
        public void Load(string path, string pattern, bool ignoreIfExist = false)
        {
            foreach (var fn in Directory.EnumerateFiles(path, pattern))
            {
                //new FileInfo(fn).DeleteAlternateDataStream("Zone.Identifier");
                var v = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.Location == fn);
                if (v != null)
                {
                    AppContext.Log.Trace("Plugin assembly already loaded: " + fn);
                    Load(v);
                }
                else
                {
                    AppContext.Log.Trace("Loading plugin assembly: " + fn);
                    try
                    {
                        Load(Assembly.LoadFrom(fn), ignoreIfExist);
                    }
                    catch (BadImageFormatException) { AppContext.Log.Error("Load plugin> BadImageFormat: " + fn); }//возможен мусор в указаной папке - это не ошибка, но зафиксировать нужно
                    catch (FileLoadException ex) { AppContext.Log.Error("Load plugin> FileLoadException: " + fn + Environment.NewLine + ex.Message); }
                    catch (SecurityException ex) { AppContext.Log.Error("Load plugin> SecurityException: " + fn + Environment.NewLine + ex.Message); }
                    catch (ReflectionTypeLoadException ex)
                    {
                        var exceptionMessage = "";
                        Array.ForEach(ex.LoaderExceptions, e => exceptionMessage += e.Message + Environment.NewLine);
                        AppContext.Log.Error("Load plugin> ReflectionTypeLoadException: " + fn + Environment.NewLine + exceptionMessage);
                    }
                }
            }
        }
        public void Load(string path, bool ignoreIfExist = false)
        {
            Load(path, "*.dll", ignoreIfExist);
            Load(path, "*.exe", ignoreIfExist);
        }

        /// <summary> Загрузить plugin из исходного кода</summary>
        public void LoadFromSource(string[] referencedAssemblies, string[] source, bool ignoreIfExist = false)
        {
            Load(new CSCodeProvider().AddReference(referencedAssemblies).Compile(source), ignoreIfExist);
        }
    }
}