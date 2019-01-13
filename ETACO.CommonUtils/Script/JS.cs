using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.Expando;
using Microsoft.JScript;
using System.Linq;

namespace ETACO.CommonUtils.Script
{
    /// <summary> Выполнение кода JScript</summary>
    /// Проблемы с Import.JScriptImport, но можно использовать переданные объекты new JS().Set("x", new Form()).Eval("x.Show();") + use Dispose
    /// Обходной вариант использовать JSContext._new => new JS().Eval("_new('System.Data.DataTable')")
    /// Производительность совпадает с JSEval
    /*using (var js = new JS()){
        js.Set("x", 42).Set("y", "Svodka.xls").Eval(
        @"  var type = _type('TestPlugin.Plugin');
        if(type == null) _load('TestPlugin.dll'); //чтобы два раза не грузить сборку
        _new('TestPlugin.Plugin').GenerateXLS(x,y);");
    }*/
    internal class JS : MarshalByRefObject, IDisposable // можно искользовать как отедльный класс без CommonUtils
    {
        private GlobalScope _scope = null;
        private string _mode = "";

        /// <summary> Создание экземпляра класса </summary>
        /// <param name="reff">Список ссылок на дополнительные сборки</param>
        public JS(params string[] reff)
        {
            //VsaEngine.CreateEngineAndGetGlobalScope(false, new[] { "System.dll"});
            var v = new List<string>() { "mscorlib.dll", "System.dll", "System.Xml.dll", "System.Data.dll", "System.Drawing.dll", "System.Windows.Forms.dll"/*, AppContext.AppFullFileName */};
            if (reff != null) v.AddRange(reff);
            var gs = (GlobalScope)JSContext._type("Microsoft.JScript.Vsa.VsaEngine").InvokeMember("CreateEngineAndGetGlobalScope", BindingFlags.InvokeMethod, Type.DefaultBinder, null, new object[] { false, v.ToArray() });//fast=false иначе with(this){x=0} throw stackowerflowexception
            Import.JScriptImport("System", gs.engine); //для имён с '.' ("System.Collections" и т.п.) - не работает
            _scope = new GlobalScope(gs, gs.engine);
            gs.engine.PushScriptObject(_scope);
            With.JScriptWith(new JSContext(), _scope.engine);//по умолчанию используется стандартный контекст
        }

        public JS SetMode(bool unsafeMode) { _mode = unsafeMode ? "unsafe" : ""; return this; }

        public JS SetContext(object ext = null) { 
            _scope.engine.PopScriptObject();//удаляем старый контекст 
            With.JScriptWith(ext ?? new JSContext(), _scope.engine);
            return this; 
        }
        
        /// <summary> Выполнение кода </summary>
        /// <remarks> Для вызова функций из Math нужно использовать System.Math., а не Math., иначе вызывается функция JScript</remarks>
        public object Eval(string expr, params object[] args) //addFunc =>  1) Eval("var zzz = function(t){return t;}",true); 2)Eval("zzz(10,42)");
        {
            if (args != null && args.Length > 0) for (int i = 0; i < args.Length; i++) Set("$" + i, args[i]);
            try
            {   
                return Microsoft.JScript.Eval.JScriptEvaluate(expr, _mode, _scope.engine);
            }
            catch (JScriptException jse)
            {
                throw new Exception(jse.Message + $" (row:{jse.Line} col:{jse.StartColumn})" + Environment.NewLine + ">" + jse.LineText.Split('\n')[jse.Line - 1], jse);
            }
        }

        /// <summary> Установка значения переменной в контексте</summary>
        public JS Set(string name, object value)
        {   //_scope.AddField(name);    
            _scope.InvokeMember(name, BindingFlags.SetField, null, _scope, new object[] { value }, null, null, null);
            return this;
        }

        public object Get(string name, object defaultValue = null)
        {
            try
            {
                return _scope.InvokeMember(name, BindingFlags.GetField, null, _scope, null, null, null, null);
            }
            catch (MissingFieldException)
            {
                return defaultValue;
            }
        }

        public Dictionary<string, string> GetFunctions()// js.Eval("f=function(x,y){return x+y;}");!!!! важно, что НЕ new function
        {
            var res = new Dictionary<string, string>();
            foreach (var v in _scope.GetFields(BindingFlags.Public).Select(x => new { Name = x.Name, Val = x.GetValue(x) }).Where(x=>x.Val is Closure)) res.Add(v.Name, v.Name + (v.Val + "").Substring(8, (v.Val + "").IndexOf('{') - 8));
            return res;
        }

        public Dictionary<string, object> GetFields()
        {
            var res = new Dictionary<string, object>();
            foreach (var v in _scope.GetFields(BindingFlags.Public).Select(x=> new {Name=x.Name, Val= x.GetValue(x)}).Where(x=> !(x.Val is Closure) && !(x.Val is Namespace))) res[v.Name] = v.Val;
            return res;
        }

        public void Remove(string name)
        {
            foreach (var m in _scope.GetMember(name, BindingFlags.Public)) ((IExpando)_scope).RemoveMember(m); //аналог Eval("delete x"), removes a property from an object (т.е. не var!!! переменные), их можно только = null
        }

        public void Clear()
        {
            foreach (var m in _scope.GetMembers(BindingFlags.Public)) ((IExpando)_scope).RemoveMember(m);
        }

        public void Dispose() { Dispose(true); }

        ~JS() { Dispose(false);}

        private void Dispose(bool disposing)
        {
            if(_scope!= null)
            {
                _scope.engine.Close();
                if (disposing) GC.SuppressFinalize(this);
            }
            _scope = null;         
        }
    }

    public class JSContext
    {
        public object _new(string name) { return _new(name, null); } //Eval("_new('System.Windows.Forms.Form').ShowDialog()")
        public object _new(string name, params object[] prms) { return Activator.CreateInstance(_type(name), prms); }
        public Assembly _load(string name) { return Assembly.LoadFrom(name); }
        public static Type _type(string typeName) { return AppContext.GetType(typeName);}
    }
}
