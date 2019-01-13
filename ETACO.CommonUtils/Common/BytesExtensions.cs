using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с byte[] </summary>
    public static class BytesExtensions
    {
        /// <summary> Возвращает кодировку текста содержащегося в буфере </summary>
        /// <remarks> Кодировка определяется по первым байтам буфера </remarks>
        /// <returns> Если определить кодировку неудалось - возвращает <c>Encoding.Default</c></returns>
        public static Encoding GetEncoding(this byte[] data)
        {
            //use Encoding.GetPreamble(); -но не все кодировки её имеют
            if (data != null)
            {
                if ((data.Length >= 4) && ((data[0] == 0 && data[1] == 0 && data[2] == 0xFE && data[3] == 0xFF) || (data[0] == 0xFF && data[1] == 0xFE && data[2] == 0 && data[3] == 0))) return Encoding.UTF32;
                if ((data.Length >= 2) && (data[0] == 0xFE && data[1] == 0xFF)) return Encoding.BigEndianUnicode;
                if ((data.Length >= 2) && (data[0] == 0xFF && data[1] == 0xFE)) return Encoding.Unicode;
                if ((data.Length >= 3) && (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)) return Encoding.UTF8;
            }
            return Encoding.UTF8;//ибо Encoding.Default - на разных машинах будет выдавать разные кодировки 
        }

        /// <summary> Возвращает текст содержащийся в буфере </summary>
        /// <param name="data">буфер</param>
        /// <param name="encoding">кодировка (если не указана, то определяется автоматически)</param>
        public static string GetString(this byte[] data, Encoding encoding = null)
        {
            return (encoding ?? data.GetEncoding()).GetString(data);
        }

        /// <summary> Возвращает шестнадцатеричное представление буфера </summary>
        /// <param name="data">буфер</param>
        /// <param name="delim">разделитель между цифрамими в представлении</param>
        public static string ToHexString(this byte[] data, string delim = "")
        { 
            var sb = new StringBuilder();
            foreach (byte d in data)
            {
                if (sb.Length > 0) sb.Append(delim);
                sb.Append(d.ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary> Конвертирует буфер в Base64 </summary>
        public static string ToBase64String(this byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        /// <summary> Возвращает хеш (MD5)</summary>
        public static byte[] GetMD5HashCode(this byte[] data)
        {
            return data.GetHashCode("MD5");
        }

        /// <summary> Возвращает хеш (SHA1)</summary>
        public static byte[] GetSHA1HashCode(this byte[] data)
        {
            return data.GetHashCode("SHA1");
        }

        /// <summary> Возвращает хеш </summary>
        /// <param name="data">буфер</param>
        /// <param name="signType">алгоритм хеширования</param>
        public static byte[] GetHashCode(this byte[] data, string signType)
        {
            return HashAlgorithm.Create(signType).ComputeHash(data);
        }

        /// <summary> Возвращает хеш </summary>
        /// <param name="data">буфер</param>
        /// <param name="key">ключ алгоритма хеширования</param>
        /// <param name="algorithmName">алгоритм хеширования</param>
        public static byte[] GetHashCode(this byte[] data, byte[] key, string algorithmName = "HMACSHA1")
        {
            var hash = HMAC.Create(algorithmName);
            hash.Key = key;
            return hash.ComputeHash(data);
        }

        /// <summary> Побайтовое сравнение byte[] </summary>
        /// <returns> true - массивы имеют одинаковый размер и поэлементно равны, false - иначе</returns>
        public static bool ValueEquals(this byte[] data, byte[] buff)
        {
            if (data == null || buff == null) return false;
            if (data.Length != buff.Length) return false;
            for (int i = 0; i < data.Length; i++)
                if (data[i] != buff[i])
                    return false;
            return true;
        }

        /// <summary> Поиск подмассива </summary>
        /// <returns> индекс первого вхождения subString в data, -1 если вхождений нет </returns>
        public static int IndexOf(this byte[] data, byte[] subString)
        {
            return IndexOf(data, subString, 0);
        }

        /// <summary> Поиск подмассива </summary>
        /// <param name="data">Массив данных</param>
        /// <param name="subString">Подмассив</param>
        /// <param name="startIndex">Индекс начала поиска</param>
        /// <returns> индекс первого вхождения subString в data (не менее startIndex), -1 если вхождений нет </returns>
        public static int IndexOf(this byte[] data, byte[] subString, int startIndex)
        {
            for (int index = startIndex; index < data.Length - subString.Length; ++index)
            {
                //TODO: более эффективный алгоритм http://en.wikipedia.org/wiki/Knuth-Morris-Pratt_algorithm
                //(разница в скорости пренебрежимо мала при subString.Length << data.Length)
                bool found = true;
                
                for (int i = 0; i < subString.Length; ++i)
                {
                    if (data[index + i] != subString[i])
                    {
                        found = false;
                        break;
                    }
                }

                if (found) return index;
            }
            return -1;
        }

        public static byte[] GetSubArrayStartingAt(this byte[] data, int startIndex)
        {
            return GetSubArray(data, startIndex, data.Length - startIndex);
        }

        public static byte[] GetSubArray(this byte[] data, int startIndex, int length)
        {
            var result = new byte[length];
            Array.Copy(data, startIndex, result, 0, length);
            return result;
        }

        /// <summary>Return a single array of bytes out of all the supplied byte arrays.</summary>
        /// <param name="arBytes">Byte arrays to add</param>
        /// <returns>The single byte array.</returns>
        public static byte[] ConcatBytes(params byte[][] arBytes)
        {
            long lLength = 0;
            long lPosition = 0;

            //Get total size required.
            foreach (byte[] ar in arBytes)
                lLength += ar.Length;

            //Create new byte array
            var toReturn = new byte[lLength];

            //Fill the new byte array
            foreach (byte[] ar in arBytes)
            {
                ar.CopyTo(toReturn, lPosition);
                lPosition += ar.Length;
            }

            return toReturn;
        }

        /// <summary> Записать массив в файл. Папка создается при необходимости.</summary>
		/// <param name = "data"> массив </param>
        /// <param name= "path">путь к файлу</param>
        public static void WriteToFile(this byte[] data, string path)
        {
            new FileInfo(path).Directory.Create();
            File.WriteAllBytes(path, data);
        }

        /// <summary> РазЖать массив с использование DeflateStream</summary>
        /// <param name = "data"> массив </param>
        public static byte[] DeCompress(this byte[] data)
        {
            if (data == null) return data;
            using (var ds = new DeflateStream(new MemoryStream(data, false), CompressionMode.Decompress))
            {
                try
                {
                    return ds.ReadToEnd();
                }
                catch//(InvalidDataException)
                {
                    return data;
                }
            }
        }

        /// <summary> Сжать массив с использование DeflateStream</summary>
        /// <param name = "data"> массив </param>
        public static byte[] Compress(this byte[] data)
        {
            if (data == null) return data;
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionMode.Compress)){ds.Write(data, 0, data.Length);}
                return ms.ToArray();
            }
        }
    }
}
