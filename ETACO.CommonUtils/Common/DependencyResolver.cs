using System;
using System.Collections.Generic;

namespace ETACO.CommonUtils
{
    public class DependencyResolver
    {
        private Dictionary<Type, Func<object>> store = new Dictionary<Type, Func<object>>();
        public T Get<T>(){ var f = store.GetValue(typeof(T)); if (f == null) throw new Exception("For " +typeof(T).GetFriendlyName()+" binding not found!"); return (T)f(); }
        public DependencyResolver Bind<I, C>() where C : class, I, new() { store[typeof(I)] = () => new C(); return this; }
        public DependencyResolver Bind<I, C>(C instance) where C : class, I { store[typeof(I)] = () => instance; return this; }
        public DependencyResolver Bind<I, C>(Func<object> f) where C : class, I { store[typeof(I)] = f; return this; }
    }
}
