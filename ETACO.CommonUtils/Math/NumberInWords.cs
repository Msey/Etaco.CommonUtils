namespace ETACO.CommonUtils
{

    public static class NumberInWords
    {
        public enum Gender { Masculine, Feminine, Neuter };

        private static string[] X = {"", " один", " два", " три", " четыре", " пять", " шесть", " семь", " восемь", " девять", " десять",
                                " одиннадцать", " двенадцать", " тринадцать", " четырнадцать", " пятнадцать", " шестнадцать", " семнадцать", " восемнадцать", " девятнадцать" }; // 0 пропускаем
        private static string[] XX = { "", "", " двадцать", " тридцать", " сорок", " пятьдесят", " шестьдесят", " семьдесят", " восемьдесят", " девяносто" }; // 0 и 10 пропускаем (10 есть в X)
        private static string[] XXX = { "", " сто", " двести", " триста", " четыреста", " пятьсот", " шестьсот", " семьсот", " восемьсот", " девятьсот" }; // 0 пропускаем
        private static object[,] XXXX = { {Gender.Feminine, "тысяча", "тысячи", "тысяч"}, {Gender.Masculine, "миллион", "миллиона", "миллионов"},
                                      {Gender.Masculine, "миллиард", "миллиарда", "миллиардов"},  {Gender.Masculine, "триллион", "триллиона", "триллионов"},
                                      {Gender.Masculine, "квадрилион", "квадрилиона", "квадрилионов"}, { Gender.Masculine, "квинтилион", "квинтилиона", "квинтилионов"}};

        /// <summary> Возвращает значение последних трёх разрядов числа value прописью </summary>
        private static string GetXXX(ulong value, Gender gender, string one, string two_four, string five_)
        {
            var result = XXX[(value % 1000) / 100] + XX[(value % 100) / 10];
            value = value % 100;
            if (value > 19) { value = value % 10; }
            if (value == 2 && gender == Gender.Feminine) result += " две";
            else if (value == 1 && gender == Gender.Feminine) result += " одна";
            else if (value == 1 && gender == Gender.Neuter) result += " одно";
            else result += X[value];
            return result + " " + GetSuffix(value, one, two_four, five_);
        }

        /// <summary> Определяет окончание в наименовании "штук" </summary>
        public static string GetSuffix(ulong value, string one, string two_four, string five_)
        {
            if (value == 0) return five_;
            if (value > 19) value = value % 100;
            if (value > 19) value = value % 10;
            if (value == 0) return five_;
            return value == 1 ? one : (value < 5 ? two_four : five_);
        }

        /// <summary> Возвращает значение числа value прописью </summary>
        /// <param name="gender">род "штуки"</param>
        /// <param name="one">наименование одной "штуки"</param>
        /// <param name="two_four">наименование от 2 до 4 "штук"</param>
        /// <param name="five_">наименование остальных "штук"</param>
        public static string GetInWords(ulong value, Gender gender = Gender.Masculine, string one = "", string two_four = "", string five_ = "")
        {
            if (value == 0) return "ноль " + GetSuffix(value, one, two_four, five_);
            var result = GetXXX(value, gender, one, two_four, five_);
            value = value / 1000;
            for (byte i = 0; value > 0; i++, value = value / 1000)
            {
                result = GetXXX(value, (Gender)XXXX[i, 0], (string)XXXX[i, 1], (string)XXXX[i, 2], (string)XXXX[i, 3]) + result;
            }
            return result.Trim();
        }

        /// <summary> Возвращает сумму валюты money прописью </summary>
        /// <param name="gender">род "фигуры"</param>
        /// <param name="one">наименование одной "фигуры"</param>
        /// <param name="two_four">наименование от 2 до 4 "фигур"</param>
        /// <param name="five_">наименование остальных "фигур"</param>
        /// <param name="sgender">род "пипса"</param>
        /// <param name="sone">наименование одного "пипса"</param>
        /// <param name="stwo_four">наименование от 2 до 4 "пипсов"</param>
        /// <param name="sfive_">наименование остальных "пипсов"</param>
        /// <param name="sInWords">нужно ли представлять сумму "пипсов" прописью</param>
        public static string GetCurrencyInWords(decimal money, Gender gender, string one, string two_four, string five_, Gender sgender, string sone, string stwo_four, string sfive_, bool sInWords = false)
        {
            money = decimal.Round(money, 2);
            var figure = decimal.ToUInt64(decimal.Truncate(money));
            var pips = (ulong)((money - figure) * 100);
            return GetInWords(figure, gender, one, two_four, five_) + " " + (sInWords ? GetInWords(pips, sgender, sone, stwo_four, sfive_) : (pips.ToString("00") + " " + GetSuffix(pips, sone, stwo_four, sfive_)));
        }

        /// <summary> Возвращает сумму валюты RUR прописью </summary>
        /// <param name="sInWords">нужно ли представлять сумму "пипсов" прописью</param>
        public static string GetRURInWords(decimal money, bool sInWords = false)
        {
            return GetCurrencyInWords(money, Gender.Masculine, "рубль", "рубля", "рублей", Gender.Feminine, "копейка", "копейки", "копеек", sInWords);
        }

        /// <summary> Возвращает сумму валюты USD прописью </summary>
        /// <param name="sInWords">нужно ли представлять сумму "пипсов" прописью</param>
        public static string GetUSDInWords(decimal money, bool sInWords = false)
        {
            return GetCurrencyInWords(money, Gender.Masculine, "доллар США", "доллара США", "долларов США", Gender.Masculine, "цент", "цента", "центов", sInWords);
        }
    }
}