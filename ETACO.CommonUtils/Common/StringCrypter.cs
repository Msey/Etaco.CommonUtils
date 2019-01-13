using System;
using System.Security.Cryptography;
using System.Text;

namespace ETACO.CommonUtils
{
    /// <summary> Шифрование данных </summary>
    public class StringCrypter
    {
        /// <summary>32 символа!</summary>
        private string Key;
        /// <summary>16 символов!</summary>
        private string Vector;

        public StringCrypter(string key = "NOBODY  CAN  GET  THIS  PASSWORD", string vector = "VECTOR IS NOTHIN")
        {
            if (key.IsEmpty() || key.Length != 32) throw new ArgumentException("'StringCrypter.key' must have 32 characters");
            if (vector.IsEmpty() || vector.Length != 16) throw new ArgumentException("'StringCrypter.vector' must have 16 characters");
            Key = key;
            Vector = vector;
        }

        /// <summary> Расшифровать строку из буфера </summary>
        /// <param name="source">буфер</param>
        /// <returns>расшифрованная строка</returns>
        public string Decrypt(byte[] source)
        {
            using (var myRijndael = new RijndaelManaged { Key = Encoding.UTF8.GetBytes(Key), IV = Encoding.UTF8.GetBytes(Vector) })
            {
                return Encoding.UTF8.GetString(myRijndael.CreateDecryptor().TransformFinalBlock(source, 0, source.Length));
            }
        }

        /// <summary> Расшифровать строку из буфера содержащегося в Base64 </summary>
        /// <param name="source">буфер в Base64</param>
        /// <returns>расшифрованная строка</returns>
        public string DecryptFromBase64(string source)
        {
            return Decrypt(Convert.FromBase64String(source));
        }

        /// <summary> Шифрование строки </summary>
        public byte[] Encrypt(string source)
        {
            using (var myRijndael = new RijndaelManaged { Key = Encoding.UTF8.GetBytes(Key), IV = Encoding.UTF8.GetBytes(Vector) })
            {
                var sbytes = Encoding.UTF8.GetBytes(source);
                return myRijndael.CreateEncryptor().TransformFinalBlock(sbytes, 0, sbytes.Length);
            }
        }

        /// <summary> Шифрование строки (результирующий буфер сконвертирован в Base64)</summary>
        public string EncryptToBase64(string source)
        {
            return Convert.ToBase64String(Encrypt(source));
        }
    }
}
