using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.Expando;
using System.Text.RegularExpressions;
using Microsoft.JScript;
using System.Threading.Tasks;

namespace ETACO.CommonUtils.Script
{
    /// <summary> Реализация и расширение возможности JScript</summary>
    //Plugin//js.Eval("var type = _type('TestPlugin.Plugin'); if(type == null) _load('TestPlugin.dll'); _new('TestPlugin.Plugin').GenerateXLS(template,outFile);");
    //var x = '../../../ETACO.DirectoryEditor/bin/Debug/ETACO.DirectoryEditor.exe'; _load(x); _new('ETACO.DirectoryEditor.DirectoryEditorForm').LoadViewList(true, x + '.cfg').ShowDialog();
    //js.Eval("_.task = new Task(ToAction(function(){ for (var i = 0; i < 5; i++) { Thread.Sleep(1000); Console.WriteLine(i); }}))");
    //js.Eval("_.task = ToTask(function(){ for (var i = 0; i < 5; i++) { Thread.Sleep(1000); Console.WriteLine(i); } return 42;})");
    //js.Eval("_.task.Start();Console.WriteLine(_.task.GetAwaiter().GetResult());"); //_.task.Wait(); либо _.task.GetAwaiter().GetResult() + check exception
    public abstract class JSEvalEngine : JSContext
    {
        //нужен для функций полученых из GetClosure AVG(ROW()) => this.AVG(this.ROW()), или $this.AVG
        protected readonly Regex _setThis;
        protected readonly Regex _removeComment = new Regex(@"/\*(.*?)\*/|//(.*?)\r?\n", RegexOptions.Singleline | RegexOptions.Compiled);
        public readonly List<string> Errors = new List<string>();
        public void ClearErrors() { Errors.Clear(); }
        //----------------------------------------------------
        public readonly JSObject _ = new JSObject();//context
        //_this для доступа к JSEvalEngine из тела функиции JS _.f=fucntion(){$this.desc(y);} или манипуляции с объектом, при вызове через JSFunction $this не нужен
        //!!!ВАЖНО. Чтобы при компиляции (вызове Eval для создания _.f=fucntion(){...}) _this != null, иначе при следущем вызове _.f() получим сообщение, что $this не определён 
        public abstract object Eval(string _code, object _this = null, object _args = null);
        public abstract object EvalInContext(string _code, object _this = null, object _args = null);
        protected abstract Closure GetClosure(string expr);//быстрее, чем _Eval("x=function($){return "+expr+";}")
        /////////////////////////////
        public JSEvalEngine()
        {
            var types = GetType().GetBaseTypes(true, typeof(JSEvalEngine));//(?<![\w\d])abc(?![\w\d])
            //_setThis = new Regex(@"\b({0})(?= *\()|\b({1})\.|\b({2})\.".FormatStr(
            _setThis = new Regex(@"(?<![\w\d\.])({0})(?= *\()|(?<![\w\d\.])({1})(?= *(\W|$))".FormatStr(

                string.Join("|", types.SelectMany(v => v.GetMethodsName()).Distinct()),
                string.Join("|", types.SelectMany(v => v.GetFieldsName()).Concat(types.SelectMany(v => v.GetPropertiesName())).Distinct())),
                RegexOptions.Compiled);
        }

        public string RemoveComment(string script)
        {
            return _removeComment.Replace(script, "");
        }

        public string SetContextInScript(string script, string contextName = "$this", bool removeComment = false)
        {
            var v = contextName + ".";
            return _setThis.Replace((removeComment ? RemoveComment(script) : script).Trim(), f => v + f);
        }

        public string desc(object v = null, int colLength = 20)
        {
            bool all = v == null || v == DBNull.Value;
            v = all ? GetType() : v;
            if (v is Type)
            {
                var l = new List<string>();
                do
                {
                    var t = ((Type)v).UnderlyingSystemType;
                    l.Add((l.Count > 0 ? "\r\n" : "") + "Type: " + t.GetFriendlyName(false) + ((t.BaseType != null || t.GetInterfaces().Length > 0) ? " : " : "") + t.BaseType?.GetFriendlyName(false)
                        + ((t.BaseType != null && t.GetInterfaces().Length > 0) ? ", " : "") + string.Join(", ", t.GetInterfaces().Select(x => x.GetFriendlyName(false))));
                    l.AddRange(t.GetPropertiesDescription(false)); l.AddRange(t.GetConstructorsDescription(false)); l.AddRange(t.GetMethodsDescription(false));
                    v = t.BaseType;
                } while (all && (Type)v != typeof(object));
                v = l;
            }
            else if (v.GetType().IsCommon()) return v + "";
            else if (v is FunctionWrapper) v = (v._InvokeMember("members", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField, Type.DefaultBinder) as IEnumerable<MemberInfo>).Where(mi => mi is MethodInfo).Select(mi => ((MethodInfo)mi).GetMethodDescription(false));
            else if (v is Closure) return v + "";
            var y = v as IEnumerable;
            if (y == null || v is string) return v is DataTable ? ((DataTable)v).ToString(colLength) : v + "";
            if (!y.GetEnumerator().MoveNext()) return v.GetType().GetFriendlyName(false) + " is empty!";
            var res = new List<string>();
            foreach (var z in y) res.Add(z == null ? "null" : (z is DictionaryEntry ? "{Key}:\t{Value}".FormatStr(z) : (y is JSObject ? (z + ":" + desc(((JSObject)y)[z])) : (z is JSObject ? desc(z) : (z.GetType().IsCommon()?z+"":
                            "[{0} {1}]".FormatStr(z.GetType().GetFriendlyName(false), string.Join("\t", z.GetType().GetProperties().Select(p=>p.Name +":" +p.GetValue(z,null)))))))));
            return string.Join(Environment.NewLine, res);
        }
        public object Get(string key, object val = null) { return _[key] ?? val; }
        public object Set(string key, object val) { return _[key] = val; }//для удаления можно использовать 'delete _.x' либо ClearContext(string key = null)
        public void ClearContext(string key = null) { foreach (var m in key == null ? _.GetMembers(BindingFlags.Public) : _.GetMember(key, BindingFlags.Public)) ((IExpando)_).RemoveMember(m); }//аналог Eval("delete x")
        //GCall(AppContext.JSEval,'Get', [System.String],['x','oops'])
        public object GCall(object obj, string method, Type[] genericTypes, object[] args = null) { return obj._InvokeGenericMethod(method, genericTypes, args ?? new object[0]); }
        //GMake('Dictionary',[System.String, int],[StringComparer.OrdinalIgnoreCase])  
        public object GMake(string typeName, Type[] genericTypes, object[] args = null)
        {
            typeName += "`" + genericTypes.Length;
            var type = AppContext.GetType(typeName);
            if (type == null && null == (type = Type.GetType("System.Collections.Generic." + typeName))) throw new Exception("Type '" + typeName + "' or 'System.Collections.Generic.'" + typeName + "' not found");
            return type.CreateGeneric(genericTypes, args ?? new object[0]);
        }
        public void AddPlugin(string name, Type type, string desc = null) { AppContext.PluginManager.AddPlugin(name, type, desc); }
        //В некоторых случаях удобнее пользоваться Assembly.LoadFrom(fn) - но тут нет проверки повтороной загрузки сборки и т.п.
        public object GetPlugin(string name) { return AppContext.PluginManager.GetPlugin<object>(name); }
        public object CreatePlugin(string name) { var v = AppContext.PluginManager; int i = name.IndexOf(':'); return i < 0 ? v.CreateInstance<object>(name) : v.CreateInstance<object>(name.Substring(0, i), name.Substring(i + 1)); }
        public object GetPluginsInfo(Type type = null) { return AppContext.PluginManager.GetPluginsInfo(type); }   
        //GetJSLinq([{x:"one", y:"1"},{x:"two",y:"2"},{x:"one",y:"One"}]).Where(function(v) { return v.x == "one"; }).Select(function(v) { return {z:v.y}; })
        public JSLinq GetJSLinq(IEnumerable v) { return new JSLinq(v); }
        //для одномерных массивов лучше использовать System.String[](['test','42'])
        public object[] ToArray(object o) { var v = new List<object>(); if (o != null && o.GetType().IsArray) return (object[])o;
            var o2 = o as JSObject; if (o2 != null) foreach (var x in o2) v.Add(o2[x] is JSObject ? ToArray(o2[x]) : o2[x]);
            else if (o is IEnumerable) return ((IEnumerable)o).Cast<object>().ToArray();
            return v.ToArray();
        }

        public IEnumerable FixJSArray(IEnumerable v)
        {
            var x = v as ArrayObject;
            if(x == null) foreach (var z in v) yield return z;
            else foreach (var z in x) yield return x[z];
        }
        public Dictionary<string, object> ToDictionary(object o)
        {
            var v = new Dictionary<string, object>(StringComparer.Ordinal);
            var o2 = o as JSObject;
            if (o2 != null) foreach (string f in o2) { var o3 = o2[f]; v.Add(f, o3 is JSObject ? ToDictionary(o3) : o3); }
            return v;
        }
        public DataTable ToTable(object[] data, string[] cols = null)//пока оставил для ETACO.DirectoryEditor (потом нужно будет убрать)
        {
            var dt = new DataTable(); cols = cols ?? new[] { "KEY", "VALUE" }; foreach (var x in cols) dt.Columns.Add(x.ToUpper());
            for (var i = 0; i < data.Length / cols.Length; i++) { var r = dt.NewRow(); for (var j = 0; j < cols.Length; j++) r[j] = data[i * cols.Length + j]; dt.Rows.Add(r); }
            return dt;
        }
        public object ToJSObject(object obj)// use for cast from AnonymousType or return JSObject : cast({x:1,y:2}) return JSObject(){x=1;y=1;}
        {
            if (obj == null) return null;
            var t = obj.GetType();
            if (!t.IsAnonymous()) return obj;
            var v = new JSObject(); foreach (var p in t.GetProperties()) v[p.Name] = p.GetValue(obj, null);
            return v;
        }
        public JSFunction ToJSFunction(object func)
        {
            var f = func as ScriptFunction; if (f == null) throw new Exception("Parameter 'func' in GetFunction is not a correct function body");
            return new JSFunction(f, this);
        }
        public Action ToAction(Closure f) { return () => f.Invoke(this, new object[f.length].Select(x => this).ToArray()); }
        public Func<object> ToFunc(Closure f) { return () => { return f.Invoke(this, new object[f.length].Select(x => this).ToArray()); }; }
        public Task<object> ToTask(Closure f) { return new Task<object>(ToFunc(f)); }
        ///!!! Нельзя передавать функции созданные внутри других функций (scope разный) иначе Exception: Unable to cast object of type 'Microsoft.JScript.WrappedNamespace' to type 'Microsoft.JScript.StackFrame'.
        ///!!! use: DoAsync(_.x); _.y = function(){$this.Info('?');} _.x = function(){$this.GUIInvoke($this._.y);} либо DoAsync(function(){$this.GUIInvoke($this._F(function(){$this.Info('?')}));})
        public Closure _F(Closure f) { var v = f + ""; return GetClosure(v.Substring(11, v.Length - 12)); }//создание F в безопасном контекте (так лушче, чем экранировать " или ')
        public void DoAsync(Closure f) { Async.DoAsync(ToAction(f), x => x.GetResult()); }//второй делегат только для проверки Exception
        public void GUIInvoke(Closure v) { System.Windows.Forms.Application.OpenForms[0].InvokeAction(ToAction(v)); }//new Control().Invoke может быть создан уже в другом потоке
        public object AddHandler(object obj, string eventName, ScriptFunction func) { return JSEventHelper.AddHandler(obj, eventName, func); }
        public void RemoveHandler(object obj, string eventName, Delegate func) { JSEventHelper.RemoveHandler(obj, eventName, func); }
        public static bool IsJSObject(object v) { return v is JSObject; }
        public static bool IsJSFunction(object v) { return v is Closure; }
        public static object[] GetJSObjectFields(object o, params string[] names)//name in uppercase
        {
            var v = new object[names.Length];
            var js = o as JSObject;
            if (js == null) return v;

            foreach (var x in js.GetFields(BindingFlags.Default))
            {
                var i = Array.FindIndex(names, s => x.Name.Equals(s, StringComparison.OrdinalIgnoreCase));
                if (i < 0) throw new Exception("Parameter '" + x.Name + "' not found");
                v[i] = x.GetValue(js);
            }
            return v;
        }

        public Exception HandleJSException(Exception ex)
        {
            if (ex is IndexOutOfRangeException) return null; //заглушка для вызова вложеных функций (проблема в движке)!!! exception из скрипта обёрнуты в JScriptException (сюда не попадут)
            var jse = ex as JScriptException;
            if (jse == null)
            {
                var tie = ex as TargetInvocationException;
                if (tie == null) return ex;
                jse = tie.InnerException as JScriptException;
            }
            var v = jse == null ? ex.InnerException : new Exception(GetJSErrorMessage(jse), jse);
            return v;
        }

        public string GetJSErrorMessage(Exception ex)
        {
            var jse = ex as JScriptException;
            return jse == null ? ex.Message : GetJSErrorMessage(jse);
        }
        private string GetJSErrorMessage(JScriptException jse)//нужно ускорить работу с генерацией сообщение
        {
            if (jse.LineText.IsEmpty()) return jse.Message;
            var line = jse.LineText.Split('\n')[jse.Line - 1];
            var end = Math.Min(Math.Max(jse.EndColumn, jse.StartColumn), line.Length+1);
            var start = Math.Max(1, Math.Min(end, jse.StartColumn));
            return jse.GetFullExceptionText() + " line {0} : '{1}!->{2}<-!{3}'".FormatStr(jse.Line, line.Substring(0, start - 1).TrimStart(), line.Substring(start - 1, end - start), line.Substring(end - 1).TrimEnd());
        }

        public class JSLinq : IEnumerable
        {
            private IEnumerable data;
            public JSLinq(IEnumerable data) { this.data = data;}
            //new JSLinq([{x:"one", y:"1"},{x:"two",y:"2"},{x:"one",y:"One"}]).Where(function(v) { return v.x == "one"; }).Select(function(v) { return {z:v.y}; }).ToList()
            public JSLinq Where(Closure f) { return new JSLinq(Where(data, f)); }
            public JSLinq Select(Closure f) { return new JSLinq(Select(data, f)); }
            public List<object> ToList() { return data.Cast<object>().ToList(); }
            //ToArray(Select(Where([{x:"one", y:"1"},{x:"two",y:"2"},{x:"one",y:"One"}], function(v) { return v.x == "one"; }), function(v) { return { zzz: v.y}; }))[0].zzz
            public static IEnumerable Select(IEnumerable c, Closure f) { var x = c as JSObject; if(x!= null) foreach (var v in c) yield return f.Invoke(null, x[v]); else foreach (var v in c) yield return f.Invoke(null, v); }
            public static IEnumerable Where(IEnumerable c, Closure f)
            {
                var x = c as JSObject;
                if (x != null)  foreach (var v in c) { if ((bool)f.Invoke(null, x[v]) == true) yield return x[v]; } 
                else foreach (var v in c) { if ((bool)f.Invoke(null, v) == true) yield return v; } 
            }
            public IEnumerator GetEnumerator() { return data.GetEnumerator();}
        }

        /// <summary> Списко имен доступных переменных в контексте (включая функции)</summary>
        public IEnumerable<string> Parameters { get { foreach (var x in _) if (!(_[x] is Closure)) yield return x + ""; } }
        /// <summary> Описание доступных функций JScript в контексте</summary>
        public IEnumerable<ScriptFunctionInfo> ScriptFunctions { get { foreach (var x in _) if (_[x] is Closure) yield return new ScriptFunctionInfo(x.ToString(), _[x] as Closure); } }
    }

    public class JSFunction
    {
        public static readonly JSFunction Empty = new JSFunction(null, null);
        private ScriptFunction function;//f.AddField(name).SetValue(f, value)//access from this.x// f.GetField(name, BindingFlags.Instance)?.GetValue(f)
        private JSEvalEngine _engine;
        private object _this;
        internal JSFunction(ScriptFunction function, JSEvalEngine engine) { _engine = engine; _this = function is Closure ? engine : null/*delegate*/; this.function = function;}
        public object Invoke(object args = null) { return function?.Invoke(_this, args); } //use _engine.HandleJSException(e) or _engine.GetJSErrorMessage(jse)
        public object Invoke(object[] args) { return function?.Invoke(_this, args); }
    }

    public class JSIndexer<T>
    {
        public T data { get; private set; }
        private Func<T, string, object> get = null;
        private Action<T, string, object> set = null;
        public event Action</*get*/bool, /*name*/string, /*value*/object> OnChangeData;
        public JSIndexer(Func<T, string, object> get = null, Action<T, string, object> set = null) { this.get = get; this.set = set;}
        public JSIndexer<T> SetData(T data) { this.data = data; return this; }
        public object this[string key] { get { var v = get?.Invoke(data, key); OnChangeData?.Invoke(true, key, v); return v; } set { OnChangeData?.Invoke(false, key, value); set?.Invoke(data, key, value); } }
    }
    public class JSEventHelper
    {
        private static int _count = 10;
        private static Type _type = null;
        private static CSCodeProvider _codeProvider = (CSCodeProvider)new CSCodeProvider().AddReference("Microsoft.JScript.dll").AddUsing("Microsoft.JScript");

        public static Type GetType(int count)
        {
            if (_count < count)
            {
                _type = null;
                _count = count * 2;
            }
            if (_type != null) return _type;
            var code = "class EventWrapper { private ScriptFunction func = null; public EventWrapper(ScriptFunction func) { this.func = func; } public void Wrap() { func.Invoke(null); }\n";
            string g = "", f = "", c = "";
            for (int i = 1; i <= _count; i++)
            {
                g += ((g.Length > 0) ? ", " : "") + "T" + i;
                f += ((f.Length > 0) ? ", " : "") + "T{0} t{0}".FormatStr(i);
                c += ((c.Length > 0) ? ", " : "") + "t" + i;
                code += "public void Wrap<" + g + ">(" + f + "){ func.Invoke(null," + c + "); }\n";
            }
            return _type = _codeProvider.Compile(code + "\n}").GetType("EventWrapper", true, true);
        }

        /// <summary> Добавление обобщённого обработчика события </summary>
        public static Delegate AddHandler(object obj, string eventName, ScriptObject func)
        {
            if (obj == null) throw new Exception("Object for AddHandler is null");

            var eventInfo = obj.GetType().GetEvent(eventName);
            if (eventInfo == null) throw new Exception("Event '{0}' for '{1}' not found.".FormatStr(eventName, obj.GetType()));

            var iParams = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();
            var iParamsLen = iParams.Length;

            var wType = GetType(iParamsLen);
            var wrapper = Activator.CreateInstance(wType, func);

            foreach (var mi in wType.GetMethods())
            {
                if (mi.Name == "Wrap" && (mi.GetParameters().Length == iParamsLen))
                {
                    Delegate result = null;
                    if (iParams.Length == 0) result = Delegate.CreateDelegate(eventInfo.EventHandlerType, wrapper, mi);
                    else
                    {
                        var types = new Type[iParamsLen];
                        for (var j = 0; j < iParamsLen; j++) types[j] = iParams[j].ParameterType;
                        result = Delegate.CreateDelegate(eventInfo.EventHandlerType, wrapper, mi.MakeGenericMethod(types));
                    }
                    eventInfo.AddEventHandler(obj, result);
                    return result;
                }
            }
            throw new Exception("ScriptObject with {0} parameters not found.".FormatStr(iParamsLen));
        }

        /// <summary> Удаление обобщённого обработчика события </summary>
        public static void RemoveHandler(object obj, string eventName, Delegate func)
        {
            if (obj == null) throw new Exception("Object for RemoveHandler is null");

            var eventInfo = obj.GetType().GetEvent(eventName);
            if (eventInfo == null) throw new Exception("Event '{0}' for '{1}' not found.".FormatStr(eventName, obj.GetType()));
            eventInfo.RemoveEventHandler(obj, func);
        }
    }
}
