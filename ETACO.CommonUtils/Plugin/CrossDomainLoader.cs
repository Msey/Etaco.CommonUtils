using System;
using System.Reflection;
//using System.Security;
//using System.Security.Permissions;

namespace ETACO.CommonUtils
{
    /// <summary>Загрузка сборок с в отдельный домен</summary>
    /// <remarks>Позволяет загружать сборки и при необходимости выгружать их из памяти</remarks>
    /// <remarks>Используется как обёртка для классов автоматически генерирующих сборки или подгружающих их динамически</remarks> 
    public class CrossDomainLoader : IDisposable
    {
        private AppDomain domain = null;

        /// <summary> Создание экземпляра указанного типа </summary>
        public T CreateInstance<T>(params object[] args) where T : MarshalByRefObject
        {
            //var appDomain = AppDomain.CreateDomain("Trusted Domain", null, new AppDomainSetup() { ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase}, new PermissionSet(PermissionState.Unrestricted));
            //appDomain.CreateInstance(Assembly.GetExecutingAssembly().FullName, "Program");
            if (args == null) args = new object[] { null };
            if (domain == null) domain = AppDomain.CreateDomain("X", AppDomain.CurrentDomain.Evidence);
            return (T)domain.CreateInstanceFromAndUnwrap(typeof(T).Assembly.Location, typeof(T).FullName,
                                                         false, BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding,
                                                         null, args, null, null);
        }

        /// <summary> Выгрузка домена </summary>
        public CrossDomainLoader Unload()
        {
            if (domain != null) AppDomain.Unload(domain);
            domain = null;
            return this;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}