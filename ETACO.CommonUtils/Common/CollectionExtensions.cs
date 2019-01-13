using System;
using System.Collections.Generic;
using System.Linq;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с коллекциями </summary>
    public static class CollectionExtensions
    {
        /// <summary> Возвращает элемент словаря или (если его нет) значение по умолчанию </summary>
        /// <remarks> dict[key]=value - если элемента key нет в словаре, то он туда добавится </remarks>
        /// <param name="dict">словарь</param>
        /// <param name="key">ключ</param>
        /// <param name="defaultValue">значение по умолчанию</param>
        /// <param name="addIfNotExist">флаг (добавить defaultValue в словарь для key)</param>
        public static TValue GetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue, bool addIfNotExist)
        {
            TValue result;
            if (dict.TryGetValue(key, out result)) return result;
            if (addIfNotExist) dict.Add(key, defaultValue);
            return defaultValue;
        }

        public static TValue GetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default(TValue))
        {
            TValue result;
            return (dict.TryGetValue(key, out result)) ? result : defaultValue;
        }

        /// <summary> Возвращает новый массив с добавленным элементом </summary>
        /// <remarks> не самый быстрый способ </remarks>
        public static T[] Add<T>(this T[] source, T value)
        {
            return new List<T>(source) { value }.ToArray();
        }

        /// <summary> Возвращает массив в виде строки </summary>
        /// <remarks> для удобного просомтра при отладке </remarks>
        public static string AsString<T>(this T[] source, string delim = ";")
        {
            return string.Join(delim, source);//System.Globalization.CultureInfo.InvariantCulture???
        }

        public static string AsString(this Array source, string delim = ";")
        {
            var s = new string[source.Length];
            for (var i = 0; i < source.Length; i++) s[i] = source.GetValue(i) + "";
            return string.Join(delim, s);//System.Globalization.CultureInfo.InvariantCulture???
        }

        /// <summary> Возвращает признак входит ли значение в указанный список </summary>
        public static bool OneOf<T>(this T value, params T[] list)
        {
            if (value == null || list == null || list.Length == 0) return false;
            return Array.Exists(list, v => v.Equals(value));
        }
        /// <summary> Возвращает deepcopy коллекции </summary>
        public static IEnumerable<T> Clone<T>(this IEnumerable<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }

        public static int BinarySearch<T>(this List<T> list, T item, Func<T, T, int> compare)
        {
            return list.BinarySearch(item, new ComparisonComparer<T>(compare));
        }
    }

    public class ComparisonComparer<T> : IComparer<T>
    {
        private readonly Comparison<T> comparison;
        public ComparisonComparer(Func<T, T, int> compare)
        {
            comparison = new Comparison<T>(compare);
        }
        public int Compare(T x, T y)
        {
            return comparison(x, y);
        }
    }

    public class Array<T>
    {
        private static readonly T[] v = new T[0];
        public static T[] Empty { get { return v; } }
    }

    public class EmptyList<T>
    {
        private static readonly List<T> v = new List<T>(0);
        public static List<T> Empty { get { return v; } }
    }
}