using System;

namespace ETACO.CommonUtils.Script
{
    /// <summary> Выполнение кода </summary>
    /// <remarks>Eval хорошо использовать, когда один и тот же код должен выполняться много раз (проблема с выгрузкой сборки)
    ///в другом случае лучше использовать интерпретатор</remarks> 
    public class CSEval : CSEval<object>
    {
        public CSEval(string code, string compilerVersion = "") : base(code, compilerVersion) { }
        public override string Code { get { return " public class Eval { public void Evaluate(" + _args + "){ " + _code + "; } }"; } }

        /// <summary> Вычисление выражений с возвратом результата (один раз)</summary>
        /// <remarks>После вычисления сборка выгружается </remarks> 
        /// <remarks>Все передаваемые параметры имеют тип Object и имена p0...pN соответственно</remarks> 
        public static T EvalOnce<T>(string code, params object[] param)
        {
            return EvalOnce<T, CSEval<T>>(code, param);
        }

        /// <summary> Выполнение кода </summary>
        /// <remarks>После вычисления сборка выгружается </remarks> 
        /// <remarks>Все передаваемые параметры имеют тип Object и имена p0...pN соответственно</remarks> 
        public static void EvalOnce(string code, params object[] args)
        {
            EvalOnce<object, CSEval>(code, args);
        }

        private static T EvalOnce<T, E>(string code, params object[] args) where E : CSEval<T>
        {
            if (args == null) args = new object[] { null };
            using (var l = new CrossDomainLoader())
            {
                E eval = l.CreateInstance<E>(code);
                for (int i = 0; i < args.Length; i++) eval.AddParam<object>("p" + i);
                return eval.Evaluate(args);
            }
        }
    }

    /// <summary> Вычисление выражений с возвратом результата</summary>
    /// <remarks>Eval хорошо использовать, когда один и тот же код должен выполняться много раз (проблема с выгрузкой сборки)
    ///в другом случае лучше использовать интерпретатор</remarks> 
    public class CSEval<T> : MarshalByRefObject
    {
        private readonly CSCodeProvider _codeProvider = null;
        private object _instance = null;
        protected string _code = "";
        protected string _args = "";
        public virtual string Code { get { return " public class Eval { public object Evaluate(" + _args + "){ return " + _code + "; } }"; } }

        public CSEval(string code, string compilerVersion = "v2.0")
        {
            _codeProvider = new CSCodeProvider(compilerVersion);
            _code = code;
        }

        private CSEval<T> Clear()
        {
            _instance = null;
            return this;
        }

        /// <summary> Добавление входных параметров </summary>
        public CSEval<T> AddParam<P>(string name)
        {
            _args += "{0}{1} {2}".FormatStr((_args.Length > 0) ? ", " : "", GetTypeName(typeof(P)), name);
            return Clear();
        }

        /// <summary> Добавление ссылок на сборки </summary> 
        public CSEval<T> AddReference(params string[] assemblies)
        {
            _codeProvider.AddReference(assemblies);
            return Clear();
        }

        /// <summary> Добавление директивы using </summary>
        public CSEval<T> AddUsing(params string[] usings)
        {
            _codeProvider.AddUsing(usings);
            return Clear();
        }

        /// <summary> Вычислить выражение </summary>
        //для того, что вызвать return для нескольких операция используем следующую конструкцию с Func
        //private static string test() { return new Func<String>(() => { int a = 1 + 3; return a + ""; }).Invoke(); }
        public T Evaluate(params object[] args)
        {
            if (_instance == null) _instance = Activator.CreateInstance(_codeProvider.Compile(Code).GetType("Eval", true, true));
            return (T)_instance._InvokeMethod("Evaluate", args ?? new object[] { null });
        }

        /// <summary> Получить имя типа </summary>
        public static string GetTypeName(Type t)
        {
            if (!t.IsGenericType) return t.FullName;
            var result = "";
            foreach (var argType in t.GetGenericArguments()) result += (result == "" ? "" : ", ") + GetTypeName(argType);
            return t.FullName.Substring(0, t.FullName.IndexOf('`')) + "<" + result + ">";
        }
    }
}