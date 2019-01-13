using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Reflection;

namespace ETACO.CommonUtils
{
    //dynamic xx = new Expando(); xx.z = 12; var f = new JSEval().Set("xx", xx).Eval("_.xx['z']");
    public class Expando : DynamicObject, INotifyPropertyChanged//, IDynamicMetaObjectProvider
    {
        private object _obj;
        private Type _objType;
        private readonly Dictionary<string,object> Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;//"{0} has changed to {1}.", e.PropertyName, ((IDictionary<String, Object>)s).GetValue(e.PropertyName,"deleted"));
        private void OnPropChanged(string key) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key)); }

        public Expando(object ext = null)
        {
            _obj = ext;
            _objType = ext?.GetType();
        }

        public bool TryGetMember(string key, out object result)
        {
            if (Properties.TryGetValue(key, out result)) return true;
            if (_obj != null) try { if(_GetProperty(key, out result)) return true; } catch { }
            result = Properties[key] = new Expando();
            return true;
        }

        public bool TrySetMember(string key, object value)
        {
            if (_obj != null) try { if (_SetProperty(key, value)) { OnPropChanged(key); return true; } } catch { }// if (_obj == null || Properties.ContainsKey(key)) Properties[key] = value; //performance
            Properties[key] = value;
            OnPropChanged(key);//у вложеных типов нужно отведльно подписываться на событие : exp.x.y = 42 для exp не отработает т.к. tryGetMember(x).TrySetMember(y) 
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result){ return TryGetMember(binder.Name, out result); }
        public override bool TrySetMember(SetMemberBinder binder, object value){ return TrySetMember(binder.Name, value); }
        public override bool TryGetIndex(GetIndexBinder binder, object[] ind, out object result) { return TryGetMember(ind[0] + "", out result); }
        public override bool TrySetIndex(SetIndexBinder binder, object[] ind, object value) { return TrySetMember(ind[0] + "", value); }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (_obj != null) try {if (InvokeMethod(binder.Name, args, out result)) return true;} catch { }

            object v = null;
            if(Properties.TryGetValue(binder.Name, out v)){ try { result = ((Delegate)v).DynamicInvoke(args); return true; } catch { }}

            result = null;
            return false;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var v in Properties.Keys) yield return v;
        }
        public IEnumerable<KeyValuePair<string, object>> GetProperties()
        {
            foreach (var p in Properties) yield return new KeyValuePair<string, object>(p.Key, p.Value);//чтобы не могли изменить извне
            if (_objType != null) foreach (var prop in _objType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) yield return new KeyValuePair<string, object>(prop.Name, prop.GetValue(_obj, null));
        }

        public object this[string key] { get { object v; TryGetMember(key, out v); return v; } set { TrySetMember(key, value); } }

        private T _FindMember<T>(string name, BindingFlags bFlag) where T : MemberInfo
        {
            if (_objType == null) return null;
            var mi = _objType.GetMember(name, BindingFlags.Public | bFlag | BindingFlags.Instance);       
            return mi.Length == 0 ? null : (T)mi[0];
        }

        private bool _GetProperty(string name, out object result)
        {
            var v = _FindMember<PropertyInfo>(name, BindingFlags.GetProperty);
            if(v!= null) { result = v.GetValue(_obj, null); return true;}
            result = null;
            return false;
        }

        private bool _SetProperty(string name, object value)
        {
            var v = _FindMember<PropertyInfo>(name, BindingFlags.SetProperty);
            if (v != null) { v.SetValue(_obj, value, null); return true;}
            return false;
        }

        private bool InvokeMethod(string name, object[] args, out object result)
        {
            var v = _FindMember<MethodInfo>(name, BindingFlags.InvokeMethod);
            if (v != null) { result = v.Invoke(_obj, args); return true;}
            result = null;
            return false;
        }
    }
}
