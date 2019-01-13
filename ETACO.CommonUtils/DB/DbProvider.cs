using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;

namespace ETACO.CommonUtils
{
    static class DbProvider
    {
        private static Dictionary<string, Assembly> cache  = new Dictionary<string, Assembly>(); //для ручной загрузки
        public static DbProviderFactory GetProvider(string provider, string factory, string dll = null)
        {
            var key = provider + "." + factory;
            var lib = cache.GetValue(key);
            try
            {
                if (lib == null) return DbProviderFactories.GetFactory(provider);
            }
            catch
            {//foreach(var x in Assembly.ReflectionOnlyLoadFrom(f).GetReferencedAssemblies()) AppContext.Log.Error(x.FullName + " ==> " +  x.CodeBase);
                cache[key] = lib = Assembly.LoadFrom(Path.Combine(AppContext.AppDir, dll.IfEmpty(provider + ".dll")));
            }
            //find public&private constructor (lib.CreateInstance - only public)
            return (DbProviderFactory)lib.GetType(key).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null).Invoke(null);
        }
    }
}
