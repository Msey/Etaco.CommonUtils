using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы со строками </summary>
    public static class StringExtensions
    {
        private static Regex reg = new Regex(@"\{(?>[^{}]+|\{(?<DEPTH>)|\}(?<-DEPTH>))*(?(DEPTH)(!?))\}", RegexOptions.Compiled);
        /// <summary> Является ли строка пустой </summary>
        public static bool IsEmpty(this string source)
        {
            return string.IsNullOrEmpty(source);
        }

        /// <summary> Возвращает строку (если она не пустая) иначе альтернативное значение </summary>
        public static string IfEmpty(this string source, string empty = "", string notEmptyFormat = null)
        {
            return source.IsEmpty() ? empty : (notEmptyFormat == null ? source : notEmptyFormat.FormatStr(source));
        }

        /// <summary> Форматирование строки </summary>
        /// <remarks>"{0:ddMMyyyy}{1.Length}".FormatStr(DateTime.Now, "12345");"{p1}!!{p2.Day}".FormatStr(new { p1 = 42, p2 = DateTime.Now })</remarks>
        public static string FormatStr(this string source, params object[] args)
        {
            try
            {
                return string.Format(source, args);
            }
            catch (FormatException)
            {
                return FormatStrEx(source, args);
            }
        }


        /// <summary> Расширеное форматирование строки (первым параметром можно передать Config) </summary>
        public static string FormatStrEx(this string source, params object[] args)
        {
            var cfg = args.Length > 0 ? args[0] as Config : null;
            var start = cfg == null ? 0 : 1;
            return reg.Replace(source, m =>
            {
                try
                {
                    var x = m.Value;
                    var v = x.Substring(1, x.Length - 2).Trim();
                    if (v.IsEmpty()) return v;
                    if (v[0] == '#') return AppContext.JSEval.Eval(v.Substring(1).FormatStrEx(args), null, args?.Length == 0 ? null : args?[0]) + "";
                    if (v[0] == '%') return "{" + v.Substring(1) + "}";//for code: {#if(..) {%...}}
                    if (v[0] == '@') return cfg.GetValue(v.Substring(1));
                    var i = v.IndexOf(':');
                    var path = i > 0 ? v.Substring(0, i) : v;
                    var format = i > 0 ? v.Substring(i + 1) : "";
                    i = path.IndexOf('.');
                    var argInd = 0;
                    var dict = args[start] as Dictionary<string, object>;
                    object result = null;
                    if (i < 0) result = int.TryParse(path, out argInd) ? args[argInd + start] : (dict == null ? args[start]._GetProperty(path) : dict[path]);
                    else if (!int.TryParse(path.Substring(0, i), out argInd)) result = dict == null ? args[start]._GetProperty(path) : dict[path.Substring(0, i)]._GetProperty(path.Substring(i + 1));
                    else {
                        var dict2 = args[argInd + start] as Dictionary<string, object>;
                        if (dict2 == null) result = args[argInd + start]._GetProperty(path.Substring(i + 1));
                        else {
                            var i2 = path.IndexOf('.', i + 1);
                            result = i2 < 0 ? dict2[path.Substring(i+1)] : dict2[path.Substring(i+1, i2 - i - 1)]._GetProperty(path.Substring(i2 + 1));
                        }
                    }
                    return (format.IsEmpty() ? result : result._InvokeMethod("ToString", format))?.ToString();
                }
                catch (Exception ex)
                {
                    throw new Exception("'{0}' => '{1}'".FormatStr(m.Value, ex.Message));
                }
            });
        }

        /// <summary> Подпадает ли эта строка под паттерн </summary>
        /// <param name="doesNotContainMode"> doesNotContainMode = false - если содержит подстроки подпадающие под патерн, то возвращаем true иначе false, doesNotContainMode = true -  если НЕ содержит подстроки подпадающие под патерн, то возвращаем true иначе false</param>
        public static bool IsMatch(this string source, string pattern, bool ignoreCase = true, bool doesNotContainMode = false)
        {
            if (pattern.IsEmpty()) return false;
            if (pattern == "*") return true;
            if (source == null) source = "";
            if (source == pattern) return true; //fast))
            if (doesNotContainMode) pattern = "(?(?={0}$)^$|^*)".FormatStr(pattern);
            pattern = "^" + Regex.Replace(pattern.Replace(".", @"\."), @"\*", (m) => "(." + m + ")") + "$";
            return Regex.IsMatch(source, pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }

        /// <summary> Разбить строку на подстроки по разделителю </summary>
        /// <param name="source">исходная строка</param>
        /// <param name="separator">строка разделитель</param>
        /// <param name="removeEmpty">удалять ли пустые строки из результата</param>
        public static string[] Split(this string source, string separator, bool removeEmpty = true, bool useTrim = true)
        {
            if (source.IsEmpty()) return removeEmpty ? new string[0] : new string[] { "" };
            if (useTrim) source = source.Trim();
            return source.Split(new string[] { separator }, removeEmpty ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
        }

        /// <summary> Заменить все вхождения любого из указаных символов </summary>
        /// <param name="source">исходная строка</param>
        /// <param name="insert">строка для замены</param>
        /// <param name="remove">кандидаты на замену</param>
        public static string ReplaceAny(this string source, string insert, params string[] remove)
        {
            if (source.IsEmpty() || (insert == null)) return source;
            var result = source;
            Array.ForEach(remove, r => result = result.Replace(r, insert));
            return result;
        }

        /// <summary> Содержит ли строка любую из указанных </summary>
        /// <param name="source">исходная строка</param>
        /// <param name="sc">компаратор для строк</param>
        /// <param name="str">кандидаты для поиска</param>
        public static bool ContainsAny(this string source, StringComparison sc, params string[] str)
        {
            if (source.IsEmpty() || str == null) return false;
            foreach (var r in str) if (source.IndexOf(r, sc) >= 0) return true;
            return false;
        }

        /// <summary> Строка в верхнем регистре </summary>
        public static bool IsUpper(this string source)
        {
            if (source.IsEmpty()) return false;
            for (int i = 0; i < source.Length; i++) if (char.IsLower(source[i])) return false;
            return true;
        }

        /// <summary> Строка в нижнем регистре </summary>
        public static bool IsLower(this string value)
        {
            if (value.IsEmpty()) return false;
            for (int i = 0; i < value.Length; i++) if (char.IsUpper(value[i])) return false;
            return true;
        }
        
        /// <summary> Заменить первый символ на символ в верхнем регистре </summary>
        public static string ToUpperFirst(this string s)
        {
            return s.IsEmpty() ? s : s.First().ToString().ToUpper() + string.Join("", s.Skip(1));
        }

        /// <summary> Записать строку в файл. Папка создается при необходимости.</summary>
        /// <param name="source">исходная строка</param>
        /// <param name="path">путь к файлу</param>
        /// <param name="append">Дописывать ли файл если он уже есть</param>
        public static void WriteToFile(this string source, string path, bool append = true)
        {
            new FileInfo(path).Directory.Create();
            if (append) File.AppendAllText(path, source); else File.WriteAllText(path, source);
        }

        /// <summary> Получить корректный путь к файлу</summary>
        public static string GetValidFilePath(this string path, char altChar = '_')
        {
            Array.ForEach(Path.GetInvalidPathChars(), c => path = path.Replace(c, altChar));
            return path;
        }

        /// <summary> Получить корректное имя файла</summary>
        public static string GetValidFileName(this string fileName, char altChar = '_')
        {
            Array.ForEach(Path.GetInvalidFileNameChars(), c => fileName = fileName.Replace(c, altChar));
            return fileName;
        }

        /// <summary> Урезать строку до максимального числа символов </summary>
        /// <param name="source">исходная строка</param>
        /// <param name="maxLen">максимальная допустимая длина строки</param>
        /// <param name="tail">суфикс добавляемый в случае урезания</param>
        /// <returns></returns>
        public static string Trim(this string source, int maxLen, string tail)
        {
            if (source.IsEmpty()) return source;
            source = source.Trim();
            tail = tail.IfEmpty();
            if (source.Length > maxLen)
            {
                return (tail.Length > maxLen) ? tail.Substring(tail.Length - maxLen) : (source.Substring(0, maxLen - tail.Length) + tail);
            }
            return source;
        }

        /// <summary> Убрать для каждой из подстрок лидирующие и завершающие пустые(пробельные) символы </summary>
        public static string TrimMultiline(this string text, string appendDelim = "")
        {
            if (text.IsEmpty()) return string.Empty;
            var result = new StringBuilder();
            using (var sr = new StringReader(text))
            {
                var buff = sr.ReadLine();
                while (buff != null)
                {
                    result.Append(buff.Trim());
                    buff = sr.ReadLine();
                    if (buff != null) result.Append(appendDelim);
                }
            }
            return result.ToString();
        }

        /// <summary> Выравнить текст по центру строки длиной в len символов, при необходимости обрезать до len </summary>
        public static string Center(this string s, int len)
        {
            if (s.IsEmpty() || len < 1) return "";
            return s.Length == len ? s : "{0}{1}{0}".FormatStr(new string(' ', (int)Math.Ceiling(Math.Max(len - s.Length, 0) / 2.0)), s).Substring(0, len);
        }

        /// <summary> Заменяет текстовое представление эскейп-последовательностей на их значения </summary>
        /// <remarks> Можно использовать, например, при задании длинных строк с форматированием в ресурсах </remarks>
        public static string ForceEscape(this string text)
        {
            var sb = new StringBuilder(text);
            sb.Replace(@"\a", "\a");
            sb.Replace(@"\b", "\b");
            sb.Replace(@"\f", "\f");
            sb.Replace(@"\n", "\n");
            sb.Replace(@"\r", "\r");
            sb.Replace(@"\t", "\t");
            sb.Replace(@"\v", "\v");
            //sb.Replace(@"\'", "\'");
            //sb.Replace(@"\"", "\"");
            //sb.Replace(@"\\", "\");
            //sb.Replace(@"\?", "\?");

            return sb.ToString();
        }

        /// <summary> Вычисление значения нужного типа из строки</summary>
        /// <typeparam name="T">тип результата</typeparam>
        /// <param name="enumDelimiter">разделитель для значений перечислимых типов</param>
        public static T GetValue<T>(this string value, string enumDelimiter = "|", Config config = null)
        {
            var t = typeof(T);
            if (t == typeof(string)) return (T)(object)value.FormatStrEx(config ?? AppContext.Config);
            try
            {
                if (t.IsEnum)
                {
                    int result = 0;
                    foreach (var v in value.Split(enumDelimiter)) result = result | Convert.ToInt32(Enum.Parse(t, v, true));
                    return (T)Enum.ToObject(t, result);
                }
                else
                {
                    try
                    {
                        //для массивов поддерживается упрощённая запись 1,2,[3,'test']
                        if (t.IsArray && t.GetArrayRank() == 1)
                        {
                            var v = t.GetElementType();
                            return (T)AppContext.JSEval.Eval("{0}([{1}])".FormatStr(v == typeof(object) ? "ToArray" : v + "[]", value));
                        }
                        return (T)Convert.ChangeType(value, t);//System.Globalization.CultureInfo.GetCultureInfo("ru")
                    }
                    catch
                    {
                        return (T)Convert.ChangeType(AppContext.JSEval.Eval(value), t);//Eval.EvalOnce<T>(value);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Can't convert value '{0}'  to type '{1}'".FormatStr(value, typeof(T)), ex);
            }
        }

        /// <summary>Получение int из строки (быстрее стандартных функций int.TryParse в 1.5-2 раза (x86/x64) (Release - более 10 раз))</summary>
        /// <remarks>нет проверки на переполнение int и пустую строку</remarks>
        /// <remarks>int: -2 147 483 648 to 2147483647 (11 символов со знаком)</remarks>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIntFast(this string s, int pos = 0, int len = 0)
        {
            var sign = s[pos] == '-';
            int v = 0;
            for (int i = sign ? (pos + 1) : pos, l = len > 0 ? Math.Min(len + pos, s.Length) : s.Length; i < l; i++) v = v * 10 + (s[i] - '0');
            return sign ? -v : v;//-2147483648 => 2147483648 переходит в -2147483648 и -1 * -2147483648 опять  -2147483648
        }

        private static int _GetIntFast9(this string s, int pos = 0)//для ускорения чтения строк фиксированной длины 8 (без учёта знака)
        {
            return (s[pos++] - '0') * 100000000 + (s[pos++] - '0') * 10000000 + (s[pos++] - '0') * 1000000 + (s[pos++] - '0') * 100000 
                + (s[pos++] - '0') * 10000 + (s[pos++] - '0') * 1000 + (s[pos++] - '0') * 100 + (s[pos++] - '0') * 10 + (s[pos++] - '0');
        }
  
        private static int _GetIntFast2(this string s, int pos = 0) {return (s[pos++] - '0') * 10 + (s[pos++] - '0');}
        private static int _GetIntFast4(this string s, int pos = 0) {return (s[pos++] - '0') * 1000 + (s[pos++] - '0') * 100 + (s[pos++] - '0') * 10 + (s[pos++] - '0');}

        /// <summary>Получение long из строки (быстрее стандартных функций long.TryParse в 1.5-2 раза (x86/x64) (Release - x64:более 10 раз, x86: более 5 раз))</summary>
        /// <remarks>нет проверки на переполнение long и и пустую строку</remarks>
        /// <remarks>long: –9 223 372 036 854 775 808 to 9223372036854775807 (20 символов со знаком)</remarks>
        public static long GetLongFast(this string s, int pos = 0, int len = 0)//до 3 может быть медленне int (long на 64 тормозит более чем в 6 раз)
        {
            if (AppContext.Is64BitApp)//для x64 скорость операций с int и long совпадают, для x86 в 4 раза медленее
            {
                var sign = s[pos] == '-';
                long z = 0;
                for (int i = sign ? (pos + 1) : pos, l = len > 0 ? Math.Min(len + pos, s.Length) : s.Length; i < l; i++) z = z * 10 + (s[i] - '0');
                return sign ? -z : z;
            }

            len = len > 0 ? Math.Min(len, s.Length - pos) : s.Length - pos;
            if (len < 10) return s.GetIntFast(pos, len);
            if (len < 19) { var v = s.GetIntFast(pos, len - 9) * 1000000000L; return v > 0 ? v + s._GetIntFast9(pos + len - 9) : v - s._GetIntFast9(pos + len - 9);}
            if (len == 20 && s[pos] == '-') return ('0' - s[pos + 1]) * 1000000000000000000L - s._GetIntFast9(pos + 2) * 1000000000L - s._GetIntFast9(pos + 11);
            var w = s.GetIntFast(pos, 9) * 10000000000L;//default len = 19
            return w > 0 ? (w  + s._GetIntFast9(pos + 9) * 10L + (s[pos + 18] - '0')): (w - s._GetIntFast9(pos + 9) * 10L + ('0' - s[pos + 18]));
        }

        private static decimal _GetDecimalFast(this string s, int pos, int len)//без дробной части
        {
            if (len == 0) return 0;
            if (len < 10) return s.GetIntFast(pos, len); //new decimal(x, 0, 0, x < 0, 0); 
            if (len < 19) return s.GetLongFast(pos, len);//new decimal((int)x, ((int)(x >> 32)), 0, x < 0, 0);
            var l = len - 18;
            var v = (l < 10 ? s.GetIntFast(pos, l) : s.GetLongFast(pos, l)) * 1000000000000000000m;
            return v < 0 ? (v - s._GetIntFast9(pos + l) * 1000000000L - s._GetIntFast9(pos + l + 9)) : (v + s._GetIntFast9(pos + l) * 1000000000L + s._GetIntFast9(pos + l + 9));
        }

        /// <summary>Получение Decimal из строки </summary>
        /// <remarks>decimal: -79 228 162 514 264 337 593 543 950 335 to 79228162514264337593543950335 (28–29 значащих цифр, с учётом дробной lenчасти)</remarks>
        /// <remarks>x86: быстрее стандартных функций decimal.TryParse: без дробной части - на 10% (Release - более 5 раз), с дробной частью - МЕДЛЕННЕЕ НА 20% (Release - БЫСТРЕЕ более 2 раз)</remarks>
        /// <remarks>x64: быстрее стандартных функций decimal.TryParse: без дробной части - более 1,5 раз (Release - более 5 раз), с дробной частью - более 15% (Release - более 2 раз)</remarks>
        /// <remarks>x86: В DEBUG может быть медленне чем обычный decimal.TryParse (в остальных случаях существенно быстрее)</remarks>
        /// <remarks>Convert.ToDecimal(value).ToString(CultureInfo.InvariantCulture)</remarks>
        public static decimal GetDecimalFast(this string s, char delim = '.', int pos = 0, int len = 0)//if (c < '0' || c > '9') break; //
        {
            len = len > 0 ? Math.Min(len, s.Length - pos) : s.Length - pos;
            var i = s.IndexOf(delim, pos, len);
            if (i > -1)
            {
                var v = s._GetDecimalFast(pos, i - pos);
                var sign = s[pos] == '-';
                var flag = len > (sign ? 31 : 30);//с учётом delim и sign

                len = Math.Min(len - i + pos - 1, 28);
                var x = s._GetDecimalFast(i + 1, len);

                var xx = decimal.GetBits(x);
                if (flag && s[i + len + 1] >= '5') xx[0] += 1;
                x = new decimal(xx[0], xx[1], xx[2], false, (byte)len);
                return v < 0 ? (v - x) : (v > 0 ? (v+x) : (sign ? -x : x));//для -0,7 - теряем знак, т.к. v = 0, поэтому одтельно проверка на sign;
            }
            return s._GetDecimalFast(pos, len);
        }

        public static bool TryGetDecimalFast(this string s, out decimal val, int pos = 0, int len = 0)//if (c < '0' || c > '9') break; //
        {
            len = len > 0 ? Math.Min(len, s.Length - pos) : s.Length - pos;
            var sign = s[pos] == '-';
            var i = -1;
            val = decimal.MinValue;
            for(var x = sign ? pos+1 : pos; x < pos+len; x++)
            {
                var v = s[x];
                if (v == '.' || v == ',') { if (i > -1) return false; i = x; }
                else if (v < '0' || v > '9') return false;
            }
            if (i > -1)
            {
                var v = s._GetDecimalFast(pos, i - pos);
                var flag = len > (sign ? 31 : 30);//с учётом delim и sign

                len = Math.Min(len - i + pos - 1, 28);
                var x = s._GetDecimalFast(i + 1, len);

                var xx = decimal.GetBits(x);
                if (flag && s[i + len + 1] >= '5') xx[0] += 1;
                x = new decimal(xx[0], xx[1], xx[2], false, (byte)len);
                val = v < 0 ? (v - x) : (v > 0 ? (v + x) : (sign ? -x : x));//для -0,7 - теряем знак, т.к. v = 0, поэтому одтельно проверка на sign;
            }
            else val = s._GetDecimalFast(pos, len);
            return true;
        }

        /// <summary>Получение Double из строки </summary>
        public static double GetDoubleFast(this string s, char delim = '.', int pos = 0, int len = 0)//if (c < '0' || c > '9') break;// if (len == 0) return Double.NaN;
        {
            var c = s[pos];
            var sign = c == '-';
            var v = 0.0;
            len = len > 0 ? len + pos : s.Length;
            pos = sign ? (pos + 1) : pos;//тут pos после, т.к. в отличие от long переполнение нам не грозит
            c = s[pos++];

            while (c != delim)
            {
                v = 10 * v + (c - '0');
                if (pos >= len) return sign ? -v : v;
                c = s[pos++];
            }

            var exp = 0.1;
            while (pos < len)
            {
                c = s[pos++];
                v += (c - '0') * exp;
                exp *= 0.1;
            }
            return sign ? -v : v;
        }

        /// <summary>Получение DateTime из строки вида yyyy.MM.dd HH:mm:ss или zipMode=> yyyyMMddHHmmss (удобно для сортировки, не зависит от локали, без лишних символов)</summary>
        /// <remarks>Быстре от 2 до 10 раз, чем DateTime.ParseExact(DateTime.Now.ToString("yyyyMMddHHmmss"), "yyyyMMddHHmmss", null)</remarks>
        public static DateTime GetDateTimeFast(this string s, bool zipMode = false)
        {
            if (zipMode)//performance (only yyyyMMddHHmmss)
            {
                if (s.Length >= 14) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(4), s._GetIntFast2(6), s._GetIntFast2(8), s._GetIntFast2(10), s._GetIntFast2(12));
                if (s.Length >= 12) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(4), s._GetIntFast2(6), s._GetIntFast2(8), s._GetIntFast2(10), 0);
                if (s.Length >= 10) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(4), s._GetIntFast2(6), s._GetIntFast2(8), 0, 0);
                if (s.Length >= 8) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(4), s._GetIntFast2(6), 0, 0, 0);
                if (s.Length >= 6) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(4), 1, 0, 0, 0);
                return new DateTime(s.IsEmpty() ? 1 : s.GetIntFast(0, 4), 1, 1, 0, 0, 0);//4 max len
            }
            else
            {
                if (s.Length > 2 && s[2] == '.')// dd.MM.yyyy
                {
                    if (s.Length >= 19) return new DateTime(s._GetIntFast4(6), s._GetIntFast2(3), s._GetIntFast2(0), s._GetIntFast2(11), s._GetIntFast2(14), s._GetIntFast2(17));
                    if (s.Length >= 16) return new DateTime(s._GetIntFast4(6), s._GetIntFast2(3), s._GetIntFast2(0), s._GetIntFast2(11), s._GetIntFast2(14), 0);
                    if (s.Length >= 13) return new DateTime(s._GetIntFast4(6), s._GetIntFast2(3), s._GetIntFast2(0), s._GetIntFast2(11), 0, 0);
                    if (s.Length >= 10) return new DateTime(s._GetIntFast4(6), s._GetIntFast2(3), s._GetIntFast2(0), 0, 0, 0);
                    if (s.Length >= 5) return new DateTime(1, s._GetIntFast2(3), s._GetIntFast2(0), 0, 0, 0);
                    return new DateTime(1, 1, s.IsEmpty() ? 1 : s.GetIntFast(0, 2), 0, 0, 0);
                }
                else//yyyy.MM.dd
                {
                    if (s.Length >= 19) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(5), s._GetIntFast2(8), s._GetIntFast2(11), s._GetIntFast2(14), s._GetIntFast2(17));
                    if (s.Length >= 16) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(5), s._GetIntFast2(8), s._GetIntFast2(11), s._GetIntFast2(14), 0);
                    if (s.Length >= 13) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(5), s._GetIntFast2(8), s._GetIntFast2(11), 0, 0);
                    if (s.Length >= 10) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(5), s._GetIntFast2(8), 0, 0, 0);
                    if (s.Length >= 7) return new DateTime(s._GetIntFast4(0), s._GetIntFast2(5), 1, 0, 0, 0);
                    return new DateTime(s.IsEmpty() ? 1 : s.GetIntFast(0, 4), 1, 1, 0, 0, 0);
                }
            }
        }

        public static bool TryGetDateTimeFast(this string s, out DateTime dateTime, bool zipMode = false)
        {
            dateTime = DateTime.MinValue;
            int year = 1;
            int mon = 1;
            int day = 1;
            if (zipMode)//performance (only yyyyMMddHHmmss)
            {
                if (s.IsEmpty()) return true;
                var len = Math.Min(14, s.Length);//чтобы не проверять всю строку
                for (var i = 0; i < len; i++) { var v = s[i]; if (v < '0' || v > '9') return false; }
                year = len >= 4 ? s._GetIntFast4(0) : s.GetIntFast(); if (year < 1 || year > 9999) return false;
                mon = len >= 6 ? s._GetIntFast2(4) : 1; if (mon < 1 || mon > 12) return false;
                day = len >= 8 ? s._GetIntFast2(6) : 1; if (day < 1 || day > DateTime.DaysInMonth(year, mon)) return false;
                int hour = len >= 10 ? s._GetIntFast2(8) : 0; if (hour < 0 || hour > 24) return false;
                int min = len >= 12 ? s._GetIntFast2(10) : 0; if (min < 0 || min > 60) return false;
                int sec = len >= 14 ? s._GetIntFast2(12) : 0; if (sec < 0 || sec > 60) return false;
                dateTime = new DateTime(year, mon, day, hour, min, sec);
                return true;
            }
            else
            {
                if (s.IsEmpty()) return false;
                var len = Math.Min(19, s.Length);
                if (s.Length > 2 && s[2] == '.')// dd.MM.yyyy
                {
                    for (var i = 0; i < len; i++) { var v = s[i]; if ((v < '0' || v > '9') && !(i == 2 || i == 5 || i == 10 || i == 13 || i == 16)) return false; }
                    year = len >= 10 ? s._GetIntFast4(6) : 1; if (year < 1 || year > 9999) return false;
                    mon = len >= 5 ? s._GetIntFast2(3) : 1; if (mon < 1 || mon > 12) return false;
                    day = len >= 2 ? s._GetIntFast2(0) : s.GetIntFast(0,1); if (day < 1 || day > DateTime.DaysInMonth(year, mon)) return false;
                }
                else
                {
                    for (var i = 0; i < len; i++) { var v = s[i]; if ((v < '0' || v > '9') && !(i == 4 || i == 7 || i == 10 || i == 13 || i == 16)) return false; }
                    year = len >= 4 ? s._GetIntFast4(0) : s.GetIntFast(); if (year < 1 || year > 9999) return false;
                    mon = len >= 7 ? s._GetIntFast2(5) : 1; if (mon < 1 || mon > 12) return false;
                    day = len >= 10 ? s._GetIntFast2(8) : 1; if (day < 1 || day > DateTime.DaysInMonth(year, mon)) return false;
                }
                int hour = len >= 13 ? s._GetIntFast2(11) : 0; if (hour < 0 || hour > 24) return false;
                int min = len >= 16 ? s._GetIntFast2(14) : 0; if (min < 0 || min > 60) return false;
                int sec = len >= 19 ? s._GetIntFast2(17) : 0; if (sec < 0 || sec > 60) return false;
                dateTime = new DateTime(year, mon, day, hour, min, sec);
                return true;
            }
        }


        public static string[] SplitFast(this string s, char delim, int buffSize)
        {
            var v = new string[buffSize];//prealloc buffer => faster String split
            s.SplitFast(delim, v);
            return v;
        }

        public static int SplitFast(this string s, char delim, string[] buff)
        {
            int pLen = buff.Length;
            if (pLen == 0) return 0;
            int len = s.Length;
            int start = 0;
            int pos = 0;

            for (int i = 0; i < len; i++)
            {
                if (s[i] == delim)
                {
                    buff[pos++] = s.Substring(start, i - start);
                    if (pos >= pLen) return pos;
                    start = i + 1;
                }
            }

            buff[pos] = s.Substring(start, len - start);
            return pos+1;
        }
        public static int GetDistance(this string source, string target)//минимальное количество операций ins, del и upd одного символа на другой для перевода src в trg (стоимость операций 1)
        {
            //D(i,j) определяет минимальное число операций, необходимых для преобразования первых i символов S1, в первые j символов S2
            //D(i,j) = min(D(i-1, j) + 1 , D(i, j-1) + 1, D(i-1, j-1)  +  cost(i, j)), cost = 0 если  S1(i) = S2(j) иначе 1
            //D(i,j) = D(i,j-1)+1 - если последняя операция была вставка, D(i,j) = D(i-1,j)+1 - если последняя операция была удаление,D(i,j) = D(i-1,j-1)+1 - замена, D(i,j) = D(i-1,j-1) - при совпадении   
            if (source.IsEmpty()) return target.IsEmpty() ? 0 : target.Length;
            if (target.IsEmpty()) return source.Length;

            if (source.Length > target.Length) { var v = target; target = source; source = v;}

            var m = target.Length;
            var n = source.Length;
            var distance = new int[2, m + 1];
            for (var j = 1; j <= m; j++) distance[0, j] = j;//инициируем первый шаг - с пустой строкой для 

            var curr = 0;//для расчёта вместо m,n матрицы можно использовать 2,m+1 с попеременным переключением строк на предыдущую и текущую
            for (var i = 1; i <= n; ++i)
            {
                curr = i & 1;//нечёт
                distance[curr, 0] = i;
                var prev = curr ^ 1;//xor - не текущая!
                for (var j = 1; j <= m; j++)
                {
                    var cost = (target[j - 1] == source[i - 1] ? 0 : 1);//S1(i) = S2(j)
                    distance[curr, j] = Math.Min(Math.Min(distance[prev, j] + 1, distance[curr, j - 1] + 1), distance[prev, j - 1] + cost);
                }
            }
            return distance[curr, m];//правый нижний угол (конец алгоритма) матрицы даёт минимальное растояние
        }
    }
}