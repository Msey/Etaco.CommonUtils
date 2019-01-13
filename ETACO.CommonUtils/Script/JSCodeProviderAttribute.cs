using System;

namespace ETACO.CommonUtils.Script
{
    /// <summary> Маркер для классов функцианального расширения BaseEval</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class JSCodeProviderAttribute : Attribute
    {
        public string[] Usings { get; private set; }
        public string[] Referencies { get; private set; }
        public JSCodeProviderAttribute(string usings, string referencies = null) { Usings = (usings + "").Split(';'); Referencies = (referencies + "").Split(';'); }
    }
}
