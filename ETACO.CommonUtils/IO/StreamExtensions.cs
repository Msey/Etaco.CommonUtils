using System;
using System.IO;
using System.IO.Compression;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с потоками </summary>
    public static class StreamExtensions
    {
        /// <summary> Прочесть данные из потока </summary>
        public static byte[] ReadToEnd(this Stream stream, int buffSize = 16384)
        {
            if (buffSize <= 0) throw new ArgumentOutOfRangeException("buffSize");
            if (stream == null) throw new ArgumentNullException("stream");
            using (var result = new MemoryStream())
            {
                return CopyTo(stream, result).ToArray();
            }
        }

        /// <summary> Записать данные в поток </summary>
        public static void Write(this Stream stream, byte[] buffer)
        {
            if (buffer != null) stream.Write(buffer, 0, buffer.Length);
        }

        /// <summary> Записать данные в поток (в .NET 4 уже есть такой метод) </summary>
        public static T CopyTo<T>(this Stream input, T output, int buffSize = 16384) where T : Stream//16 * 1024
        {
            input.CopyTo(output);
            return output;
        }

        /// <summary> Архивация данных в потоке </summary>
        public static void Zip(this Stream input, Stream output, bool useGZip = true, int buffSize = 16384)
        {
            using (var v = useGZip ? (Stream)new GZipStream(output, CompressionMode.Compress) : new DeflateStream(output, CompressionMode.Compress))
            {
                input.CopyTo(v, buffSize);
            }
        }

        /// <summary> Разархивация данных в потоке </summary>
        public static void UnZip(this Stream input, Stream output, bool useGZip = true, int buffSize = 16384)
        {
            using (var v = useGZip ? (Stream)new GZipStream(input, CompressionMode.Decompress) : new DeflateStream(input, CompressionMode.Decompress))
            {
                v.CopyTo(output, buffSize);
            }
        }

        /// <summary> Записать в файл </summary>
        /// <param name="stream">исходный поток</param>
        /// <param name="path">путь к файлу</param>
        /// <param name="append">Дописывать ли файл если он уже есть</param>
        public static void WriteToFile(this Stream stream, string path, bool append = false)
        {
            var fi = new FileInfo(path);
            fi.Directory.Create();
            using (var fs = fi.Open(append ? FileMode.Append : FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fs);
            }
        }
    }
}