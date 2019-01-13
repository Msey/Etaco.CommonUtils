using System;

namespace ETACO.CommonUtils.Plugin
{
    public class PluginInfo
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Type Type { get; private set; }

        public PluginInfo(string name, Type type, string description = null)
        {
            Name = name;
            Type = type;
            Description = description??"";
        }
    }
}
