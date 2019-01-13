using System;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с перечислениями и флагами </summary>
    public static class EnumExtensions
    {
        /// <summary> Возвращает признак указывающий подпадают ли значения под маску </summary>
        /// <returns> true - маска value содержит все flags, false - иначе</returns>
        public static bool Includes<T>(this T value, T flag, params T[] flags) where T : struct, IConvertible
        {
            var val = value.ToUInt32(null);
            var fl = flag.ToUInt32(null);
            bool res = (val & fl) == fl;
            
            for (int i = 0; res && i < flags.Length; i++)
            {
                fl = flags[i].ToUInt32(null);
                res = res && (val & fl) == fl;
            }
            return res;
        }

        public static T ParseEnum<T>(this string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return defaultValue;
            T result;
            return Enum.TryParse(value, true, out result) ? result : defaultValue;
        }
    }
}
