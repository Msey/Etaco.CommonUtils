using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ETACO.CommonUtils.Script
{
    //для работы нужны сборки IronPython.dll, Microsoft.Dynamic.dll, Microsoft.Scripting.dll
    public class Python
    {
        private static readonly Type _pythonType = null;
        private readonly object _engine = null;
        private readonly object _scope = null;
        private readonly MethodInfo _eval = null;
        private readonly MethodInfo _get = null;
        static Python() { _pythonType = Assembly.LoadFrom(Path.Combine(AppContext.AppDir, "IronPython.dll")).GetType("IronPython.Hosting.Python"); }

        public Python()
        {
            _engine = _pythonType._InvokeMethod("CreateEngine");
            _scope = _engine._InvokeMethod("CreateScope");
            _eval = _engine.GetType().GetMethods().FirstOrDefault(m => m.Name == "Execute" && !m.ReturnType.IsGenericType && m.GetParameters().Length == 2);
            _get = _scope.GetType().GetMethods().FirstOrDefault(m => m.Name == "GetVariable" && !m.ReturnType.IsGenericType);
        }

        public object Eval(string code) { return _eval.Invoke(_engine, new[] { code, _scope }); } //new Python().Set("y", 5).Eval("x = y + 10\nz = x + 22\nz")
        public Python EvalFile(string filename) { _engine._InvokeMethod("ExecuteFile", filename, _scope); return this; }//new Python().Set("x",5).EvalFile("C:\\_Temp\\hello.py").Eval("factorial(x)")
        public Python Set(string name, object val) { _scope._InvokeMethod("SetVariable", name, val); return this; }
        public object Get(string name) { return _get.Invoke(_scope, new[] { name }); }

        /* hello.py
            def factorial(number):
                result = 1
                for i in xrange(2, number + 1):
                    result *= i
                return result
        */
        //dynamic f = EvalFile("C://_Temp//hello.py").Get("factorial");
        //return f(Get("y"));//Eval("factorial(y)");
        //!!!!<runtime><NetFx40_LegacySecurityPolicy enabled = "true"/></runtime> - этот ключ не даст динамически вызывать Eval("factorial(4)") - поэтому его нужно убирать из config, но тогда не работает запуск с сетевого диска

        /*var engine = Python.CreateEngine();
        engine.SetSearchPaths(new Collection<string>(new[] {
                @"C:\Python27", 
                @"C:\Python27\DLLs", 
                @"C:\Python27\Lib", 
                @"C:\Python27\Lib\site-packages", 
                @"C:\Python27\Lib\site-packages\numpy",
                @"C:\Python27\Lib\site-packages\numpy\core"
        }));
        var scope = engine.CreateScope();
        var scriptSource = engine.CreateScriptSourceFromString(_myPythonScript, SourceCodeKind.Statements);
        scriptSource.Execute(scope);*/
        //main limitation of IronPython - it does not support C-API of CPython.Hence you need to use pythonnet: https://github.com/pythonnet/pythonnet
        //You can try pure python implementation of numpy: https://github.com/wadetb/tinynumpy
        //IronPython does not support using PYDs built for CPython since they leverage implementation details of CPython.You can get a similar effect for new "PYD"s you would like to implement by writing them in C# or VB and building a DLL for .NET.


    }
}
