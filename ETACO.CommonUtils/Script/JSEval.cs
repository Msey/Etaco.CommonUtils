using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.JScript;

namespace ETACO.CommonUtils.Script
{
    public class JSEval : JSEval<JSEvalEngine> {public JSEval() : this(false) {} public JSEval(bool unsafeMode = false, string ext = "") : base(unsafeMode, ext) {}}
    /// <summary> Выполнение кода JScript</summary>
    public class JSEval<T> : MarshalByRefObject where T : JSEvalEngine
    {
        private static readonly Func<object[],T> _activator;
        static JSEval() { _activator = GetActivator(); }
        private static Func<object[], T> GetActivator(string ext = null)
        {
            var t = typeof(T);//lock = Monitor.Enter(this); try{...}finally{Monitor.Exit(this);}
            var jsc = new JSCodeProvider(0).AddTypeReference(t.GetBaseTypes(true, typeof(JSEvalEngine)).ToArray());
            var jscpa = (JSCodeProviderAttribute)Attribute.GetCustomAttribute(t, typeof(JSCodeProviderAttribute), true);
            if (jscpa != null) jsc.AddReference(jscpa.Referencies).AddUsing(jscpa.Usings);
            var code = "package X {/*expando*/class X extends " + t.FullName + @" {private var $mode:String;
                public function X(unsafe:Boolean) { $mode = unsafe?'unsafe':'';} 
                public function Eval($code:String,$this:Object,$:Object) { return eval($code, $mode);}
                public function EvalInContext($code:String,$this:Object,$:Object) { with(_){with($this||this){return eval($code, $mode);}}}
                protected function GetClosure($code:String):Closure {return new Function('$',$code);}" + ext + "}}";
            return jsc.Compile(code).GetType("X.X").GetConstructor(new[] { typeof(bool) }).GetActivator<T>();
        }
        /// <summary> Экземпляр используемого JSEvalEngine </summary>
        public T Engine { get; private set; }
        /// <summary> Создание экземпляра класса </summary>
        /// <param name="unsafeMode"> Уровень безопасности выполняемого скрипта (unsafe=true - можно объявлять функции в коде и работать с GUI/IO и т.п.)</param>
        /// <remarks> обращение к контексту через: Get('a'), Set('a') либо переменную '_', т.е.: _.a</remarks>
        /// <remarks>new JSEval(false, "function Add(x){return x+Get('y',42);}").Eval("Add(10)");//!!! нет default параметров</remarks>
        public JSEval(bool unsafeMode = false, string ext = "")
        {
            Engine = (ext.IsEmpty() ? _activator : GetActivator(ext))(new object[] { unsafeMode });
            object v; try {v = Engine.Eval("Microsoft.JScript.Globals.contextEngine"); }//иногда при создании из разных потоков кидает InvalidCastException
            catch (InvalidCastException) {v = Engine.Eval("Microsoft.JScript.Globals.contextEngine"); }//fix: with(this){x=0}//.CompilerOptions = "/fast-" - не работает
            v._InvokeMember("doFast", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance, Type.DefaultBinder, false);
            //var gs =(IVsaScriptScope)EvalObject.Eval("Globals.contextEngine.GetGlobalScope()");
        }
        /// <summary> Описание доступных функций JSEvalEngine </summary>
        public static string[] EngineFunctions { get { return typeof(T).GetBaseTypes(true, typeof(JSEvalEngine)).SelectMany(v => v.GetMethodsDescription(false)).Distinct().ToArray(); } }
        /// <summary> Выполнение кода JScript </summary>
        public object Eval(string code, object _this = null, params object[] args) { return Eval(code, _this, (object)args); }
        public object Eval(string code, object _this = null, object args= null) //Eval("Environment.FailFast('Oops')") - убивает приложение не смотря  try/finally
        {
            try
            {
                return Engine.EvalInContext(code, _this, Engine.ToJSObject(args));//for array use args = object[], un script use $[0] ...$[n]
            }
            catch (Exception ex) {throw Engine.HandleJSException(ex)??ex;}
        }
        /// <summary> Помещение переменную в контекст</summary>
        /// <remarks> Для установки свойств Engine.key = value, а для динамической компиляции Engine._SetProperty(key, value)</remarks>
        public JSEval<T> Set(string name, object val) { Engine.Set(name, val); return this;}
        public JSEval<T> Set<S>(IEnumerable<KeyValuePair<string, S>> data) { if (data != null) foreach (var v in data) Engine.Set(v.Key, v.Value); return this;}
        /// <summary> Получение переменной из контекста </summary>
        public S Get<S>(string name, S defaultValue = default(S)) { return (S)Engine.Get(name, defaultValue);}
        /// <summary> Удаление переменной(ых) из контекста</summary>
        public JSEval<T> Clear(string key=null) { Engine.ClearContext(key); return this; }
        //Compile time 50-90 ms
        //Eval	  : total = 1 408 ms.   iteration = 100 000.  //Eval+Compile	 : total = 1 468 ms.
        //Calculate: total = 92 ms.                           //Calculate+Compile: total = 98 ms.         
        public JSEval<T> Compile(Func<ScriptFunctionInfo, string> onFunc = null,  bool unsafeMode = false)
        {   //есть проблемма - параметры поумолчанию для func(x,y), до компиляции f() - ок, после компиляции f() - exception (не указаны параметры)
            var v = string.Join("\r\n", Engine.ScriptFunctions.Select(x => x.GetStubText(onFunc))).Replace("$this.", ""); //foreach (var p in Engine.Parameters) v += "\r\npublic var " + p + ":Object";
            return new JSEval<T>(unsafeMode, v.IsEmpty()?"":new Regex(string.Join("|", Engine.ScriptFunctions.Select(x => "_." + x.Name))).Replace(v, f => f.Value.Substring(2)));
        }
    }

    public class ScriptFunctionInfo
    {
        public readonly string Name;
        public readonly string[] Arguments;
        public readonly string Text;
        public readonly Dictionary<string, object> Attributes = new Dictionary<string, object>();
        internal ScriptFunctionInfo(string name, Closure func)
        {
            Name = name;
            var v = (func + "").Substring(8);
            Arguments = v.Substring(1, v.IndexOf(')')-1).Split(",");    
            Text = "function " + name + v;
            foreach (var a in func) Attributes.Add(a+"", func[a]);
        }
        public override string ToString(){return Text;}

        public string GetStubText(Func<ScriptFunctionInfo, string> onFunc = null)
        {
            if (Arguments.Length == 0) return "public " + Text;
            var callArgs = Enumerable.Range(0, Arguments.Length).Select(x => "null").ToArray();
            var funcArgs = "";
            var code = "public function " + Name + "(" + funcArgs + "){ return " + Name + "(" + string.Join(",", callArgs)+ ");}\r\n";

            for (int i = 0; i < Arguments.Length - 1; i++)
            {
                funcArgs += ((funcArgs.Length > 0) ? ", " : "") + Arguments[i];
                callArgs[i] = Arguments[i];
                code += "public function " + Name + "(" + funcArgs + "){ return " + Name + "(" + string.Join(",", callArgs) + ");}\r\n";
            }
            return code + "public " + (onFunc == null? "" : onFunc(this))  + " " + Text;
        }
    }
}