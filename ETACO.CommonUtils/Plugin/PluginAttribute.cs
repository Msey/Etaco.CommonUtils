using System;

namespace ETACO.CommonUtils.Plugin
{
    /// <summary> Маркер для классов plugin</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PluginAttribute : Attribute
    {
        public string Name { get; private set; }
        public string Description { get; private set; }

        public PluginAttribute(string name, string description = null)
        {
            Name = name;
            Description = description;
        }
    }
}
