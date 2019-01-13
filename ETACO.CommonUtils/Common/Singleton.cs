using System;
using System.Reflection;
//using System.Runtime.InteropServices;
//using mscoree;  // Add COM reference - C:\WINDOWS\Microsoft.NET\Framework\vXXXXXX\mscoree.tlb

namespace ETACO.CommonUtils
{
    /// <summary> Реализация (анти)паттерна Singleton </summary>
    public class Singleton<T> : MarshalByRefObject where T : class, new()
    {
        private static volatile T _instance;
        private static object _lock = new object();

        protected Singleton() { }

        /// <summary> Возвращает экземпляр объекта </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            try
                            {
                                _instance = new T();
                            }
                            catch (TargetInvocationException te)
                            {
                                throw te.InnerException;
                            }
                        }
                    }
                }
                return _instance;
            }

            set
            {
                lock (_lock)
                {
                    if (value == null) throw new ArgumentException("Instance for Singleton is null");
                    _instance = value;
                }
            }
        }
    }

    
    /*public class CrossAppDomainSingleton<T> : MarshalByRefObject where T : MarshalByRefObject, new()
    {
        private static readonly string AppDomainName = "Singleton AppDomain";
        private static T _instance;

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute()]
        private static AppDomain GetAppDomain(string friendlyName)
        {
            IntPtr enumHandle = IntPtr.Zero;
            //var host = new CorRuntimeHost();
            var host = (ICorRuntimeHost)Activator.CreateInstance(Type.GetTypeFromProgID("CLRMetaData.CorRuntimeHost")); //Type.GetTypeFromProgID("CLRMetaData.CorRuntimeHost").GUID = "CB2F6723-AB3A-11D2-9C40-00C04FA30A3E"
            try
            {
                host.EnumDomains(out enumHandle);

                object domain = null;
                while (true)
                {
                    host.NextDomain(enumHandle, ref domain);
                    if (domain == null) break;
                    
                    var appDomain = (AppDomain)domain;
                    if (appDomain.FriendlyName.Equals(friendlyName)) return appDomain;
                }
            }
            finally
            {
                host.CloseEnum(enumHandle);
                Marshal.ReleaseComObject(host);
                host = null;
            }
            return null;
        }


        public static T Instance
        {
            get
            {
                if (null == _instance)
                {
                    var appDomain = GetAppDomain(AppDomainName);
                    if (null == appDomain) appDomain = AppDomain.CreateDomain(AppDomainName);
                    
                    var type = typeof(T);
                    T instance = (T)appDomain.GetData(type.FullName);
                    if (null == instance)
                    {
                        instance = (T)appDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);
                        appDomain.SetData(type.FullName, instance); 
                    }
                    _instance = instance;
                }

                return _instance;
            }

            set
            {
                _instance = value;
            }
        }

        [Guid("CB2F6722-AB3A-11D2-9C40-00C04FA30A3E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICorRuntimeHost
        {
            void CloseEnum(IntPtr enumHandle);
            void EnumDomains(out IntPtr enumHandle);
            void NextDomain(IntPtr enumHandle, [MarshalAs(UnmanagedType.IUnknown)] ref object appDomain);
        }
    }*/
}
