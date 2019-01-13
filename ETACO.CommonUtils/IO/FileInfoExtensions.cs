using System.IO;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с файлами </summary>
    public static class FileInfoExtensions
    {
        /// <summary> Занят ли файл другим процессом файлами </summary>
        public static bool IsBusy(this FileInfo fi)
        {
            if (!fi.Exists) return false;
            try
            {
                using (new FileStream(fi.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { return false; }
            }
            catch
            {
                return true;
            }
        }

        /// <summary> Прочесть данные из файла </summary>
        public static byte[] ReadToEnd(this FileInfo fi)
        {
            using (var v = fi.OpenRead()) { return v.ReadToEnd(); }
        }

        /// <summary> Заархивировать файл </summary>
        public static void Zip(this FileInfo fi, bool useGZip = true, string fileName = "")
        {
            using (var from = fi.OpenRead())
            {
                using (var to = File.Create(fileName.IfEmpty(fi.FullName + ".zip")))  from.Zip(to, useGZip);
            }
        }

        /// <summary> Разархивировать файл </summary>
        public static void UnZip(this FileInfo fi, bool useGZip = true, string fileName = "")
        {
            using (var from = fi.OpenRead())
            {
                using (var to = File.Create(fileName.IfEmpty(fi.FullName.Remove(fi.FullName.Length - fi.Extension.Length)))) from.UnZip(to, useGZip);
            }
        }
    }
}