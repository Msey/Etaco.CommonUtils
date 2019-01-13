using System;
using System.Collections.Generic;

namespace ETACO.CommonUtils.Script
{
    /// <summary> Реализация и расширение возможности JScript</summary>
    public abstract class JSEvalCalcEngine : JSEvalEngine
    {
        private readonly Dictionary<string, JSFunction> _compiledExprList = new Dictionary<string, JSFunction>(StringComparer.Ordinal); //public for clear
        public void ClearCompiledList() { _compiledExprList.Clear(); }
        public JSFunction CompileExpr(string expr, bool throwException = false)
        {//в случае Compile кода _.test(1,2)=>this._.this.test() т.к. test уже попадает в функции класса и содержиться в _setThis Regex
            var code = SetContextInScript(expr, "this", true);
            //требования к expr => каждый оператор заканчивается ; кроме случая, когда он едиственный, все блоки if, while, for заключают опреаторы в {}, чтобы отличить от вызова функции
            var statments = code.Split(';');
            for (var i = statments.Length-1; i >= 0; i--)
            {
                var v = statments[i].Trim();
                if (v.Length > 0) { statments[i] = v[0] == '}' ? "} return " + v.Substring(1).TrimStart() : "return " + v.TrimStart();  break;}//иначе return /r/n 42; возвращает null
            }
            code = string.Join(";", statments);

            try { return new JSFunction(GetClosure(code), this); }
            catch(Exception ex) {
                if (throwException) throw HandleJSException(ex) ?? ex;
                Errors.Add(GetJSErrorMessage(ex) +" : [from compiler]");
                return JSFunction.Empty;
            }
        }
        public object Calculate(string code, object args = null) //быстрее в 8 раз чем Eval
        {
            var v = _compiledExprList.GetValue(code);
            if(v == JSFunction.Empty) return Eval(code, this, args);
            if(v == null) v = _compiledExprList[code] = CompileExpr(code);//тут exception быть не может
            if (v == JSFunction.Empty) return Eval(code, this, args);
            return v.Invoke(args); //синтаксически верная функция
        }//!!!! пересчёт формул с Exception долго считает только!!! в debug, в release всё летает

        public void Compile(IEnumerable<string> sources)
        {
            foreach (var s in sources) if (_compiledExprList.GetValue(s) == null) _compiledExprList[s] = CompileExpr(s);
        }
    }
}
