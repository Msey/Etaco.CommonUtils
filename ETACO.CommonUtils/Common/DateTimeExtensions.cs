using System;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с DateTime </summary>
    public static class DateTimeExtensions
    {
        /// <summary> Возвращает номер квартала (1-4) </summary>
        public static int GetQuarter(this DateTime dt)
        {
            return (dt.Month - 1) / 3 + 1;
        }

        /// <summary> Сложение двух дат </summary>
        public static DateTime Add(this DateTime dt, DateTime otherDT)
        {
            return dt.AddTicks(otherDT.Ticks);
        }
        /// <summary> Вычитание двух дат </summary>
        public static DateTime SubtractEx(this DateTime dt, DateTime otherDT)
        {
            return dt.AddTicks(-otherDT.Ticks);
        }



        /// <summary> Быстрое преобразование даты к строке (sortable)</summary>
        /// <remarks> zipMode == true => "yyyyMMddHHmmss" иначе "yyyy.MM.dd HH:mm:ss"</remarks>
        /// <remarks> в 4 раза быстрее, чем dt.ToString("yyyyMMddHHmmss") или dt.ToString("yyyy.MM.dd HH:mm:ss")</remarks>
        public static string GetStringFast(this DateTime time, bool zipMode = false, bool trimEmptyTime = false)
        {
            int year = time.Year;
            int month = time.Month;
            int day = time.Day;
            int hour = time.Hour;
            int minute = time.Minute;
            int second = time.Second;
            trimEmptyTime = trimEmptyTime && second == 0 && minute == 0 && hour == 0;

            var v = new char[zipMode ? (trimEmptyTime?8:14) : (trimEmptyTime ? 10 : 19)];
            var i = 0;
            v[i++] = (char)('0' + year / 1000);
            v[i++] = (char)('0' + year / 100 % 10);
            v[i++] = (char)('0' + year % 100 / 10);
            v[i++] = (char)('0' + year % 10);
            if(!zipMode) v[i++] = '.';
            v[i++] = (char)('0' + month / 10);
            v[i++] = (char)('0' + month % 10);
            if (!zipMode) v[i++] = '.';
            v[i++] = (char)('0' + day / 10);
            v[i++] = (char)('0' + day % 10);
            if (!trimEmptyTime)
            {
                if (!zipMode) v[i++] = ' ';
                v[i++] = (char)('0' + hour / 10);
                v[i++] = (char)('0' + hour % 10);
                if (!zipMode) v[i++] = ':';
                v[i++] = (char)('0' + minute / 10);
                v[i++] = (char)('0' + minute % 10);
                if (!zipMode) v[i++] = ':';
                v[i++] = (char)('0' + second / 10);
                v[i++] = (char)('0' + second % 10);
            }
            return new string(v);
        }
    }
}
