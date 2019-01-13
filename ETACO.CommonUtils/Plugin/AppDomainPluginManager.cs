using System;
using System.Collections.Generic;

namespace ETACO.CommonUtils.Plugin
{
    public class AppDomainPluginManager
    {
        private AppDomain domain = null;
        private PluginManager pluginManager = null;

        public void Load(string path)
        {
            domain = AppDomain.CreateDomain("Remote Load");
            pluginManager = (PluginManager)domain.CreateInstanceAndUnwrap(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, typeof(PluginManager).FullName);
            //LogMessageCrossDomainRepeater logRepeater = new LogMessageCrossDomainRepeater();
            domain.DoCallBack(new CrossAppDomainDelegate(new LogMessageCrossDomainRepeater().Repeat));
            pluginManager.Load(path);
        }

        public void Unload()
        {
            pluginManager = null;
            if (domain != null) AppDomain.Unload(domain);
            domain = null;
            GC.Collect(GC.MaxGeneration);
        }

        public List<PluginInfo> GetPluginsInfo<T>()
        {
            return pluginManager != null ? pluginManager.GetPluginsInfo<T>() : new List<PluginInfo>(0);
        }

        public T GetPlugin<T>(string name)
        {
            return pluginManager != null ? pluginManager.GetPlugin<T>(name) : default(T);
        }
    }
}