using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ETACO.CommonUtils.Script;

namespace ETACO.CommonUtils.MetatextParser
{
    /// <summary> Парсер </summary>
    public class MetatextParser : MetatextParser<JSEvalEngine> { public MetatextParser(Config config = null) : base(config) { } }

    public class MetatextParser<T> where T : JSEvalEngine
    {
        private readonly Config config = null;
        public readonly T eval = null;

        public MetatextParser(Config config = null, T eval = null)
        {
            this.config = config ?? AppContext.Config;
            if (eval != null) this.eval = eval; else this.eval = new JSEval<T>(true).Engine;
            eval.Set("LB", "{");
            eval.Set("RB", "}"); //LeftBrace //RightBrace
        }

        /// <summary> Получить список зарегистрированных функций и констант </summary>
        public List<string> GetMacrosList()
        {
            var result = new List<string>();
            foreach (var func in JSEval<T>.EngineFunctions) result.Add("{#" + func + "}");
            foreach (var cnst in eval.Parameters) result.Add("{#" + cnst + "}");
            return result;
        }

        /// <summary> Обработка текста </summary>
        public MetatextParseResult Parse(string metatext, Func<MacrosInfo, string> GetMacrosValue = null, bool throwException = false)
        {
            var result = new MetatextParseResult();

            if (!metatext.IsEmpty())
            {
                result.Text = Regex.Replace(metatext, @"\{(?>[^{}]+|\{(?<DEPTH>)|\}(?<-DEPTH>))*(?(DEPTH)(!?))\}", m =>
                {
                    try
                    {
                        return GetValue(m.Value, GetMacrosValue);
                    }
                    catch (Exception ex)
                    {
                        if (throwException) throw;
                        var error = "'{0}' => '{1}'".FormatStr(m, ex.Message);
                        result.ErrorLog.Add(error);
                        return "{" + error + "}";
                    }
                });
            }
            return result;
        }

        private string GetValue(string ps, Func<MacrosInfo, string> GetMacrosValue)//ps - min {} (len >=2)
        {
            if (ps[1] == '#')
            {
                var code = Parse(ps.Substring(2, ps.Length - 3), GetMacrosValue, true).Text;
                if (code.IsEmpty()) return code;
                var v = eval.Get(code) + "";//для переменных работает быстрее, чем Eval
                return v.IsEmpty() ? eval.Eval(code) + "" : v;
            }
            else if (ps[1] == '%') return "{" + ps.Substring(2); //for code: {#if(..) {%...}} +!!! парсер не применяется ко всем вдоженным конструкциям
            else if (ps[1] == '@') return config.GetValue(ps.Substring(2, ps.Length - 3));
            else if(GetMacrosValue != null) return GetMacrosValue(MacrosInfo.GetMacrosInfo(ps, s => Parse(s, GetMacrosValue, true).Text));
            return Parse(ps.Substring(1, ps.Length - 2), GetMacrosValue, true).Text;
        }
    }
}