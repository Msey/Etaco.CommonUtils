using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ETACO.CommonUtils.Script;

namespace ETACO.CommonUtils
{
    [System.Diagnostics.DebuggerDisplay("{Formula}")]
    public class FormulaNode
    {
        //"+","-","*","/","f",",","<>","<",">","<=",">=","!=","=" + отедельная функция !(x)
        private readonly HashSet<string> _operands = new HashSet<string>();
        public FormulaNode Parent { get; private set; }
        public FormulaNode Left { get; private set; }
        public FormulaNode Right { get; private set; }
        public string Formula { get; private set; }
        public string Operation { get; private set; }
        public string[] Operands { get { return _operands.OrderBy(c => c).ToArray(); } }

        public override string ToString()
        {
            return this.Formula;
        }

        private void AddOperands(string v)
        {
            var c = v[0];
            if ((c != '\'' && c != '"') || v[v.Length - 1] != c) 
            { 
                if (v.ContainsAny(StringComparison.Ordinal, "&", "|", "<", ">", "+", "-", "*", "/", "=","^")) throw new Exception("Incorrect operand: " + v);
                decimal d; bool b;
                if (decimal.TryParse(v, out d) || bool.TryParse(v, out b)) return;
            }
            _operands.Add(v);
            if (Parent != null) Parent.AddOperands(v);
        }

        private static Dictionary<string, string> a2d(string[] subFormulas)
        {
            var v = new Dictionary<string, string>();
            Array.ForEach(subFormulas, s => { var i = s.IndexOf('='); v.Add(s.Substring(0, i), s.Substring(i + 1)); });
            return v;
        }

        private FormulaNode(FormulaNode parent, string formula) 
        {
            if (formula.IsEmpty()) throw new Exception("Incorrect formula");
            Parent = parent;
            Formula = formula;
        }

        public FormulaNode(string formula, params string[] subFormulas) : this(formula, a2d(subFormulas)) {}

        public FormulaNode(string formula, Dictionary<string, string> subFormulas = null)
        {
            if (formula.IsEmpty()) throw new Exception("Incorrect formula");
            Formula = formula;
            Parse(subFormulas);
            BuildFormula();
        }

        private FormulaNode Parse(Dictionary<string, string> subFormulas)
        {
            var replacedFormulas = new List<string>();
            var x = Formula;
            Formula = Regex.Replace(Formula, @"\((?>[^(\)]+|\((?<DEPTH>)|\)(?<-DEPTH>))*(?(DEPTH)(!?))\)", m =>
            {
                var v = m + "";
                replacedFormulas.Add(v.Substring(1, v.Length - 2));
                return "#" + (replacedFormulas.Count - 1); 
            });
            if (Formula.Contains('(') || Formula.Contains(')')) throw new Exception("Incorrect formula: " + x);//есть непарные скобки
            return Parse(replacedFormulas, subFormulas);
        }

        private FormulaNode Parse(List<string> replacedFormulas, Dictionary<string, string> subFormulas)
        {
            //string
            var c = Formula[0];
            var s = c == '\'' || c == '"' ? Formula.IndexOf(c,1) : 0;
            //(x,y)
            var i = Formula.IndexOf(',', s); 
            if (i > 0)
            {
                Operation = ",";
                Left = new FormulaNode(this, Formula.Substring(0, i).Trim()).Parse(replacedFormulas, subFormulas);
                Right = new FormulaNode(this, Formula.Substring(i + 1).Trim()).Parse(replacedFormulas, subFormulas);
                return this;
            }
            //<,>,<=,>=,=,!=,+,-,*,/
            foreach (var oper in new[] { "&&", "&", "||", "|", "<>", "<=", ">=", "!=", "<", ">", "=", "+", "-", "*", "/","^" }) //важна последовательность
            {
				i = Formula.IndexOf(oper, s, StringComparison.Ordinal);
                if (i > 0)
                {
                    Operation = oper;
                    Left = new FormulaNode(this, Formula.Substring(0, i).Trim()).Parse(replacedFormulas, subFormulas);
                    Right = new FormulaNode(this, Formula.Substring(i + oper.Length).Trim()).Parse(replacedFormulas, subFormulas);
                    return this;
                }
            }
            if (Formula.Length > 0 && (Formula[0]== '!' || Formula[0]== '+' || Formula[0]== '-'))
            {
                Operation = "f";
                Left = new FormulaNode(this, Formula[0]+"");
                Right = new FormulaNode(this, Formula.Substring(1)).Parse(replacedFormulas, subFormulas);
                return this;
            }
            //subformulas
            i = Formula.IndexOf('#', s);
            if (i == 0)
            {
                Formula = replacedFormulas[int.Parse(Formula.Substring(s+1))];
                Parse(subFormulas);
            }
            else if (i > 0)
            {
                Operation = "f";
                Left = new FormulaNode(this, Formula.Substring(0, i).Trim());
                Right = new FormulaNode(this, replacedFormulas[int.Parse(Formula.Substring(i + 1))]).Parse(subFormulas);
            }
            else if (subFormulas != null && subFormulas.ContainsKey(Formula))
            {
                Formula = subFormulas[Formula];
                Parse(subFormulas);
            }
            else
            {
                AddOperands(Formula);
            }
            return this;
        }

        public string BuildFormula(bool useSpace = false) 
        {
            if (!Operation.IsEmpty()) // если нет оператора то формула имеет вид x1 - всё ок
            {
                var v = useSpace ? " " : "";
                if (Operation == ",")       Formula = Left.BuildFormula(useSpace) + v + ","+ v + Right.BuildFormula(useSpace);
                else if (Operation == "f")  Formula = Left.Formula + "(" + v + Right.BuildFormula(useSpace) + v + ")";
                else
                {
                    var lf = Left.BuildFormula(useSpace);
                    var rf = Right.BuildFormula(useSpace);
                    if (new[] { "<>", "<=", ">=", "!=", "<", ">", "=", "*", "/", "^" }.Contains(Operation))
                    {
                        if (Left.Operation == "-" || Left.Operation == "+") lf = "(" + v + lf + v + ")";
                        if (Right.Operation == "-" || Right.Operation == "+" || (Operation == "/" && !Right.Operation.IsEmpty())) rf = "(" + v + rf + v + ")";
                    }
                    if (new[] { "&&", "&", "||", "|"}.Contains(Operation))
                    {
                        if (new[] { "<>", "<=", ">=", "!=", "<", ">", "="}.Contains(Left.Operation)) lf = "(" + v + lf + v + ")";
                        if (new[] { "<>", "<=", ">=", "!=", "<", ">", "="}.Contains(Right.Operation)) rf = "(" + v + rf + v + ")";
                    }
                    Formula = lf + v + Operation + v + rf;
                }
            }
            return Formula;
        }

        public object Eval(Func<string, string[], object> onFormula = null, string[] parameters = null, List<Tuple<FormulaNode, object>> trace = null)
        {
            var v = new Dictionary<string, object>();
            if(parameters != null )Array.ForEach(parameters, (p) =>
            {
                var s = p.Split("=");
                if (s.Length > 1)
                {
                    try{ v.Add(s[0], decimal.Parse(s[1], CultureInfo.InvariantCulture));}
                    catch
                    {
                        try{ v.Add(s[0], bool.Parse(s[1]));}
                        catch
                        {
                            v.Add(s[0], s[1]);
                        }
                    }
                }
            });
            return Eval(onFormula, v, trace);
        }

        public object Eval(Func<string, string[], object> onFormula, Dictionary<string, object> parameters, List<Tuple<FormulaNode, object>> trace = null) 
        {
            object v = null;
            if (Right != null)
            {
                var r = Right.Eval(onFormula, parameters, trace);

                if (Operation == "f" && Left.Formula == "!") v = !Convert.ToBoolean(r);//("!" + r).ToLower().GetValue<bool>();
                else if (Operation == "f" && Left.Formula == "+") v = Convert.ToDecimal(r);
                else if (Operation == "f" && Left.Formula == "-") v = -Convert.ToDecimal(r);
                else if (Operation == "f" && onFormula != null) v = onFormula(Left.Formula, (r + "").Split(","));
                else if (Operation == "f") v = new JSEval().Eval("System.Math." + CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Left.Formula) + "(" + r + ")");//("System.Math." + CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Left.Formula) + "(" + r + ")").GetValue<decimal>();   //hack просто Math. нельзя, т.к. будет использоваться JScript.Math
               
                if (v == null)
                {
                    var l = Left.Eval(onFormula, parameters, trace);

                    if (Operation == "+") if(r is string || l is string) v= ""+l+r; else v = Convert.ToDecimal(l) + Convert.ToDecimal(r);
                    else if (Operation == "-") v = Convert.ToDecimal(l) - Convert.ToDecimal(r);
                    else if (Operation == "*") v = Convert.ToDecimal(l) * Convert.ToDecimal(r);
                    else if (Operation == "/") v = Convert.ToDecimal(l) / Convert.ToDecimal(r);
                    else if (Operation == "^") v = Math.Pow(Convert.ToDouble(l), Convert.ToDouble(r));
                    //для Eval сойдёт и строковое представление параметроов
                    else if (Operation == ",") v = (((l is bool || l is string) ? l : Convert.ToDecimal(l).ToString(CultureInfo.InvariantCulture)) + ","
                                               + ((r is bool || r is string) ? r : Convert.ToDecimal(r).ToString(CultureInfo.InvariantCulture))).ToLower();

                    if (v == null)
                    {
                        var lr = Left.GetRightVal(onFormula, parameters, trace);
                        var rl = Right.GetLeftVal(onFormula, parameters, trace);

                        if (lr is string || rl is string) v = new JSEval().Eval("'" + lr + "'" + (Operation == "=" ? "==" : (Operation == "<>" ? "!=" : Operation))  + "'" + rl + "'");
                        else if (Operation == "<") v = Convert.ToDecimal(lr) < Convert.ToDecimal(rl) && (!(l is bool) || (bool)l) && (!(r is bool) || (bool)r);
                        else if (Operation == ">") v = Convert.ToDecimal(lr) > Convert.ToDecimal(rl) && (!(l is bool) || (bool)l) && (!(r is bool) || (bool)r);
                        else if (Operation == "<=") v = Convert.ToDecimal(lr) <= Convert.ToDecimal(rl) && (!(l is bool) || (bool)l) && (!(r is bool) || (bool)r);
                        else if (Operation == ">=") v = Convert.ToDecimal(lr) >= Convert.ToDecimal(rl) && (!(l is bool) || (bool)l) && (!(r is bool) || (bool)r);
                        else if (Operation == "=") v = l is bool && r is bool ? Convert.ToBoolean(l) == Convert.ToBoolean(r) : Convert.ToDecimal(lr) == Convert.ToDecimal(rl) && (!(l is bool) || (bool)l) && (!(r is bool) || (bool)r);
                        else if (Operation == "!=" || Operation == "<>") v = l is bool && r is bool ? Convert.ToBoolean(l) != Convert.ToBoolean(r) : Convert.ToDecimal(lr) != Convert.ToDecimal(rl) && (!(l is bool) || (bool)l) && (!(r is bool) || (bool)r);
                        else if (Operation == "&&" || Operation == "&") v = Convert.ToBoolean(l) && Convert.ToBoolean(r);
                        else if (Operation == "||" || Operation == "|") v = Convert.ToBoolean(l) || Convert.ToBoolean(r);
                    }
                }
            }
            if (v == null)
            {
                if (parameters != null && parameters.ContainsKey(Formula)) return parameters[Formula];
                var c = Formula[0]; if ((c == '\'' || c == '"') && Formula[Formula.Length - 1] == c) return Formula.Substring(1, Formula.Length - 2);
                try { return decimal.Parse(Formula, CultureInfo.InvariantCulture); }
                catch
                {
                    try { return bool.Parse(Formula); }
                    catch (Exception ex) { throw new Exception("'" + Formula + "' is undefined.", ex); }
                }
            }
            if (trace != null) trace.Add(new Tuple<FormulaNode, object>(this, v));
            return v;
        }

        private object GetLeftVal(Func<string, string[], object> onFormula, Dictionary<string, object> parameters, List<Tuple<FormulaNode, object>> trace = null)
        {
            var v = Eval(onFormula, parameters, trace);
            return v is bool && (Left!= null) ? Left.GetLeftVal(onFormula, parameters, trace): v;
        }

        private object GetRightVal(Func<string, string[], object> onFormula, Dictionary<string, object> parameters, List<Tuple<FormulaNode, object>> trace = null)
        {
            var v = Eval(onFormula, parameters, trace);
            return v is bool && (Right != null) ? Right.GetRightVal(onFormula, parameters, trace) : v;
        }
    }
}
