using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace ETACO.CommonUtils.Telecom
{
    public enum MimeReaderMode
    {
        FromStrings,
        FromBytes,
    }

    public class MimeReader
    {
        private MimeReader _parent = null;
        private string _startBoundary;

        protected MimeReaderMode _bodyMode = MimeReaderMode.FromStrings;
        private MimeReaderMode _headerMode = MimeReaderMode.FromStrings;

        protected byte[] _bodyBytes;
        private byte[] _rawBytes;
        private byte[] _headerBytes;

        private readonly List<string> _encodedLines = new List<string>();
        private readonly List<string> _rawLines = new List<string>();
        private readonly List<string> _headerLines = new List<string>();

        private readonly NameValueCollection _headers = new NameValueCollection();
        private readonly List<MimeReader> _children = new List<MimeReader>();

        public NameValueCollection Headers { get { return this._headers; } }
        public IList<MimeReader> Children { get { return this._children; } }

        public string TransferEncoding { get { return Headers["content-transfer-encoding"] + ""; } }
        public ContentDisposition ContentDisposition { get; private set; }
        public ContentType ContentType { get; private set; }
        public string StartBoundary
        {
            get
            {
                if (!ContentType.Boundary.IsEmpty()) return "--" + ContentType.Boundary;
                return _startBoundary.IfEmpty("--");
            }
        }
        public string EndBoundary { get { return this.StartBoundary + "--"; } }

        public MimeReader(IEnumerable<string> lines)
        {
            if (lines == null) throw new ArgumentNullException("lines");
            CreateMimeReader(new Queue<string>(lines));
        }

        public MimeReader(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            CreateMimeReader(data);
        }

        public MimeReader(IEnumerable<string> headerLines, byte[] bodyData)
        {
            if (bodyData == null) throw new ArgumentNullException("bodyData");
            CreateMimeReader(new Queue<string>(headerLines), bodyData);
        }

        public const string CRLF = "\r\n";
        public const string MESSAGE_SEPARATOR = CRLF + CRLF;

        protected MimeReader(MimeReader parent, Queue<string> lines)
        {
            if (parent == null) throw new ArgumentNullException("parent");
            if (lines == null) throw new ArgumentNullException("lines");

            _parent = parent;
            _startBoundary = parent.StartBoundary;
            CreateMimeReader(lines);
        }

        protected MimeReader(MimeReader parent, byte[] data)
        {
            if (parent == null) throw new ArgumentNullException("parent");
            if (data == null) throw new ArgumentNullException("data");

            _parent = parent;
            _startBoundary = parent.StartBoundary;
            CreateMimeReader(data);
        }

        protected virtual MimeReader createMimeReader(MimeReader parent, Queue<string> lines)
        {
            return new MimeReader(parent, lines);
        }

        protected virtual MimeReader createMimeReader(MimeReader parent, byte[] data)
        {
            return new MimeReader(parent, data);
        }

        protected virtual MimeReader createMimeReader(byte[] data)
        {
            return new MimeReader(data);
        }

        private void CreateMimeReader(Queue<string> headerLines, byte[] data)
        {
            if (headerLines.Count > 0) ParseHeaders(headerLines);
            this._bodyMode = MimeReaderMode.FromBytes;

            if (data.Any())
            {
                this._bodyBytes = data;
                if (ContentType.MediaType == "message/rfc822")//случай с вложенными письмами
                {
                    var child = createMimeReader(this, data);
                    Children.Add(child);
                    data = child.UnprocessedData;
                }
                ParseBody(data);
            }
        }

        private void CreateMimeReader(Queue<string> _lines)
        {
            if (_lines.Count > 0)
            {
                ParseHeaders(_lines);
                if (ContentType.MediaType == "message/rfc822")//случай с вложенными письмами
                {
                    Children.Add(createMimeReader(this, _lines));
                }
                ParseBody(_lines);
            }
        }

        private void CreateMimeReader(byte[] data)
        {
            this._headerMode = MimeReaderMode.FromBytes;
            this._bodyMode = MimeReaderMode.FromBytes;
            this._rawBytes = data;
            if (data.Any())
            {
                data = ParseHeaders(data);
                this._bodyBytes = data;
                if (ContentType.MediaType == "message/rfc822")//случай с вложенными письмами
                {
                    var child = createMimeReader(this, data);
                    Children.Add(child);
                    data = child.UnprocessedData;
                }
                ParseBody(data);
            }
        }

        private byte[] ParseHeaders(byte[] data)
        {
            var crlf = DefaultEncoding.GetBytes(CRLF);

            var endOfHeader = false;
            var currentHeader = "";
            int index = 0;

            while (index < data.Length && !endOfHeader)
            {
                int lineBreakIndex = BytesExtensions.IndexOf(data, crlf, index);
                var breakFound = (lineBreakIndex != -1);
                if (!breakFound) lineBreakIndex = data.Length;

                int subStringLength = lineBreakIndex - index;

                var headerLineBytes = new byte[subStringLength];
                Array.Copy(data, index, headerLineBytes, 0, subStringLength);

                var line = DefaultEncoding.GetString(headerLineBytes);
                processHeaderLine(ref currentHeader, line, ref endOfHeader);
                index = lineBreakIndex;
                if (breakFound) index += crlf.Length;
            }

            this._headerBytes = new byte[index];
            Array.Copy(data, this._headerBytes, index);
            processCommonHeaderAttribs();
            return getUnprocessedData(data, index);
        }

        private byte[] getUnprocessedData(byte[] data, int index)
        {
            var result = new byte[data.Length - index];
            Array.Copy(data, index, result, 0, result.Length);
            return result;
        }

        private void ParseHeaders(Queue<string> _lines)
        {
            bool endOfHeader = false;
            string currentHeader = "";
            while (_lines.Count > 0 && !endOfHeader)
            {
                var line = _lines.Dequeue();
                AddHeaderLine(line);
                processHeaderLine(ref currentHeader, line, ref endOfHeader);
            }
            processCommonHeaderAttribs();
        }

        private void processCommonHeaderAttribs()
        {
            ContentType = new ContentType(Headers["content-type"].IfEmpty("text/plain; charset=us-ascii"));
            ContentType.MediaType = ContentType.MediaType.IfEmpty("text/plain").Trim().ToLowerInvariant();
            if (!ContentType.Name.IsEmpty()) ContentType.Name = DecodeWord(ContentType.Name);
            ContentDisposition = ParseContentDisposition(Headers["content-disposition"]);
        }

        private void processHeaderLine(ref string currentHeader, string line, ref bool headerEndFound)
        {
            if (line.IsEmpty()) headerEndFound = true; //конец заголовка.
            else if (line.StartsWith(" ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal))
            {
                Headers[currentHeader] = Headers[currentHeader] + line;
            }
            else
            {
                int separatorIndex = line.IndexOf(':');
                if (separatorIndex > 0)
                {
                    currentHeader = line.Substring(0, separatorIndex).ToLowerInvariant();
                    var headerValue = line.Substring(separatorIndex + 1).Trim(' ', '\t');
                    if (Headers.AllKeys.Contains(currentHeader))
                        Headers[currentHeader] = headerValue;
                    else
                        Headers.Add(currentHeader, headerValue);
                }
            }
        }

        private void ParseBody(Queue<string> _lines)
        {
            if (StartBoundary == "--") //isEmpty = > singlepart
            {
                while (_lines.Count > 0) _encodedLines.Add(AddRawLine(_lines.Dequeue()));
            }
            else //multipart
            {
                while (_lines.Count > 0 && (_lines.Peek() != this.EndBoundary))
                {
                    if (_parent != null && (_parent.StartBoundary == _lines.Peek())) return;  //конец этой вложенной части
                    if (StartBoundary == _lines.Peek()) //Начало новой дочерней вложенной части 
                    {
                        AddRawLine(_lines.Dequeue());//разделитель не попадает в тело, но должен храниться
                        Children.Add(createMimeReader(this, _lines)); //в ходе разбора из _lines убираются строки дочерней части
                    }
                    else
                    {
                        _encodedLines.Add(AddRawLine(_lines.Dequeue()));
                    }
                }

                if (_lines.Count > 0) //добавляется EndBoundary
                    AddRawLine(_lines.Dequeue());
            }
        }

        private void ParseBody(byte[] data)
        {
            this.UnprocessedData = new byte[] { };
            if (StartBoundary == "--") //isEmpty = > singlepart
            {
                this._bodyBytes = data;
            }
            else //multipart
            {
                var crlf = DefaultEncoding.GetBytes(CRLF);
                int index = 0;
                byte[] stringBytes;
                //чтение собственного текста 
                {
                    int nextIndex = index;
                    while (readLine(data, ref nextIndex, out stringBytes))
                    {
                        if (stringBytes.ValueEquals(this.StartBoundaryBytes1) || stringBytes.ValueEquals(this.EndBoundaryBytes))
                        {
                            this._bodyBytes = data.GetSubArray(0, index);
                            break;
                        }

                        index = nextIndex;
                    }
                    index = nextIndex;
                }

                //достигнут конец данных
                if (stringBytes == null || stringBytes.ValueEquals(this.EndBoundaryBytes))
                    return;

                int endIndex = BytesExtensions.IndexOf(data, this.EndBoundaryBytes, index);
                if (endIndex == -1)
                    endIndex = data.Length;

                while (index < endIndex)
                {
                    int nextIndex = BytesExtensions.IndexOf(data, this.StartBoundaryBytes1, index);
                    if (nextIndex == -1)
                        nextIndex = data.Length + crlf.Length;
                    var childData = data.GetSubArray(index, nextIndex - index - crlf.Length);
                    var child = createMimeReader(this, childData);
                    this.Children.Add(child);
                    index = nextIndex + this.StartBoundaryBytes1.Length + crlf.Length;
                }
            }
        }

        private bool readLine(byte[] data, ref int index, out byte[] stringBytes)
        {
            if (index >= data.Length)
            {
                stringBytes = null;
                return false;
            }
            else
            {
                var crlf = DefaultEncoding.GetBytes(CRLF);
                var crlfIndex = BytesExtensions.IndexOf(data, crlf, index);
                if (crlfIndex == -1)
                {
                    stringBytes = data.GetSubArrayStartingAt(index);
                    index = data.Length;
                }
                else
                {
                    stringBytes = data.GetSubArray(index, crlfIndex - index);
                    index = crlfIndex + crlf.Length;
                }
                return true;
            }
        }

        private string AddHeaderLine(string line)
        {
            this._headerLines.Add(line);
            return AddRawLine(line);
        }

        private string AddRawLine(string line)
        {
            _rawLines.Add(line);
            if (_parent != null) _parent.AddRawLine(line);
            return line;
        }

        public MimeReader FindByMediaType(string mediaType)
        {
            if (ContentType.MediaType.ToLowerInvariant() == mediaType.ToLowerInvariant())
                return this;

            foreach (var v in Children) { var v2 = v.FindByMediaType(mediaType); if (v2 != null) return v2; };
            return null;
        }

        public string GetMessage()
        {
            return (this._bodyMode == MimeReaderMode.FromBytes) ? GetString(this._bodyBytes) : string.Join("\r\n", _encodedLines.ToArray());
        }

        private string GetHeaderMessage()
        {
            return (this._headerMode == MimeReaderMode.FromBytes) ? GetString(this._headerBytes) : String.Join("\r\n", this._headerLines.ToArray());
        }

        public string GetRawMessage()
        {
            if (this._headerMode == MimeReaderMode.FromBytes)
            {
                return GetString(this._rawBytes);
            }
            else if (this._bodyMode == MimeReaderMode.FromBytes)
            {
                var header = String.Join("\r\n", this._headerLines.ToArray());
                var body = GetMessage();
                return String.Join("\r\n", new string[] { header, body });
            }
            else
            {
                return string.Join("\r\n", _rawLines.ToArray());
            }
        }

        public byte[] GetContent()
        {
            if (this._bodyMode == MimeReaderMode.FromBytes)
            {
                if (this.GetTransferEncoding() == System.Net.Mime.TransferEncoding.Base64
                || this.GetTransferEncoding() == System.Net.Mime.TransferEncoding.QuotedPrintable)
                {
                    //хотя исходные данные были в виде массива байтов, надо их перекодировать
                    var rawString = DefaultEncoding.GetString(this._bodyBytes);
                    return GetContent(rawString);
                }
                else
                {
                    //можно вернуть исходный массив
                    return this._bodyBytes;
                }
            }
            else
            {
                return GetContent(GetMessage());
            }
        }

        public byte[] GetRawContent()
        {
            if (this._headerMode == MimeReaderMode.FromBytes)
            {
                return this._rawBytes.ToArray();
            }
            else if (this._bodyMode == MimeReaderMode.FromBytes)
            {
                var headerBytes = GetContent(GetHeaderMessage());
                var result = new List<byte>();
                result.AddRange(headerBytes);
                result.AddRange(this._bodyBytes);
                return result.ToArray();
            }
            else
            {
                return GetContent(GetRawMessage());
            }
        }

        private byte[] GetContent(string content)
        {
            var te = GetTransferEncoding();
            if (te == System.Net.Mime.TransferEncoding.Base64) return Convert.FromBase64String(content);
            else if (te == System.Net.Mime.TransferEncoding.QuotedPrintable)
            {
                var encoding = GetEncoding(ContentType.CharSet);
                return encoding.GetBytes(QuotedPrintableDecode(content, encoding));
            }
            return Encoding.UTF8.GetBytes(content);
        }

        private string GetString(IEnumerable<byte> list)
        {
            var bytes = list.ToArray();

            var te = GetTransferEncoding();
            switch (te)
            {
                case System.Net.Mime.TransferEncoding.Base64:
                    return Convert.ToBase64String(bytes);
                case System.Net.Mime.TransferEncoding.QuotedPrintable:
                    {
                        var encoding = GetEncoding(this.ContentType.CharSet);
                        return encoding.GetString(bytes);
                    }
                default:
                    return DefaultEncoding.GetString(bytes);
            }
        }

        private Encoding DefaultEncoding { get { return Encoding.GetEncoding(1252); } } //default AS2 encoding

        public TransferEncoding GetTransferEncoding()
        {
            switch (this.TransferEncoding.Trim().ToLowerInvariant())
            {
                case "7bit":
                case "8bit":
                    return System.Net.Mime.TransferEncoding.SevenBit;
                case "quoted-printable":
                    return System.Net.Mime.TransferEncoding.QuotedPrintable;
                case "base64":
                    return System.Net.Mime.TransferEncoding.Base64;
                case "binary":
                default:
                    return System.Net.Mime.TransferEncoding.Unknown;
            }
        }

        public static string DecodeWord(string encodedWords)
        {
            if (encodedWords == null) throw new ArgumentNullException("encodedWords");

            const string encodedWordRegex = @"\=\?(?<Charset>\S+?)\?(?<Encoding>\w)\?(?<Content>.+?)\?\=";
            const string replaceRegex = @"(?<first>" + encodedWordRegex + @")\s+(?<second>" + encodedWordRegex + ")";
            encodedWords = Regex.Replace(encodedWords, replaceRegex, "${first}${second}");

            string decodedWords = encodedWords;

            try
            {
                MatchCollection matches = Regex.Matches(encodedWords, encodedWordRegex);
                foreach (Match match in matches)
                {
                    if (!match.Success) continue;

                    string fullMatchValue = match.Value;

                    string encodedText = match.Groups["Content"].Value;
                    string encoding = match.Groups["Encoding"].Value;
                    string charset = match.Groups["Charset"].Value;

                    Encoding charsetEncoding = GetEncoding(charset);

                    string decodedText;

                    switch (encoding.ToUpperInvariant())
                    {
                        case "B":
                            decodedText = charsetEncoding.GetString(Convert.FromBase64String(encodedText));//try catch
                            break;

                        case "Q":
                            decodedText = QuotedPrintableDecode(encodedText, charsetEncoding);
                            break;

                        default:
                            throw new ArgumentException("The encoding " + encoding + " was not recognized");
                    }
                    decodedWords = decodedWords.Replace(fullMatchValue, decodedText);
                }
            }
            catch (Exception ex)
            {
                AppContext.Log.HandleException(ex, "Неверная кодировка заголовка письма " + encodedWords);
            }
            return decodedWords;
        }

        public static ContentDisposition ParseContentDisposition(string contentDisposition)
        {
            //Fix bug with timezone and time (00 - min or sec) (in FW4 - fixed) + decode filename
            if (contentDisposition.IsEmpty()) return null;
            var v = contentDisposition.Split(";");
            var result = new ContentDisposition(v[0]);
            for (int i = 1; i < v.Length; i++)
            {
                string s = v[i].Trim();
                int index = s.IndexOf('=');
                if (index > 0 && index < s.Length - 1)
                {
                    string key = s.Substring(0, index).Trim();
                    string val = s.Substring(index + 1).Trim();
                    if (key.Equals("filename", StringComparison.InvariantCultureIgnoreCase)) result.FileName = DecodeWord(val.Trim('"'));
                    else if (key.Equals("size", StringComparison.InvariantCultureIgnoreCase)) try { result.Size = Int64.Parse(val); }
                        catch { }
                    else if (key.EndsWith("date", StringComparison.InvariantCultureIgnoreCase))
                    {
                        val = val.Trim('"').Trim().ReplaceAny("+0000", "UT", "GMT").Replace("EDT", "-0400").ReplaceAny("-0500", "EST", "CDT").ReplaceAny("-0600", "CST", "MDT").ReplaceAny("-0700", "MST", "PDT").Replace("PST", "-0800");
                        if (key.Equals("creation-date", StringComparison.InvariantCultureIgnoreCase)) try { result.CreationDate = DateTime.ParseExact(val, "ddd, dd MMM yyyy HH:mm:ss zzzz", CultureInfo.InvariantCulture); }
                            catch { }
                        else if (key.Equals("modification-date", StringComparison.InvariantCultureIgnoreCase)) try { result.ModificationDate = DateTime.ParseExact(val, "ddd, dd MMM yyyy HH:mm:ss zzzz", CultureInfo.InvariantCulture); }
                            catch { }
                        else if (key.Equals("read-date", StringComparison.InvariantCultureIgnoreCase)) try { result.ReadDate = DateTime.ParseExact(val, "ddd, dd MMM yyyy HH:mm:ss zzzz", CultureInfo.InvariantCulture); }
                            catch { }
                    }
                    else result.Parameters.Add(key, val);
                }
            }
            return result;
        }

        public static string QuotedPrintableDecode(string contents, Encoding encoding)//REFACTOR
        {
            if (contents == null) throw new ArgumentNullException("contents");
            var hexRegex = new Regex("(\\=([0-9A-F][0-9A-F]))", RegexOptions.IgnoreCase);

            using (var writer = new MemoryStream())
            {
                using (var reader = new StringReader(contents))
                {
                    string line;
                    string decodeLine;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.TrimEnd();
                        decodeLine = hexRegex.Replace(line, (m) => Convert.ToChar(Convert.ToInt32(m.Groups[2].Value, 16)).ToString());
                        decodeLine = line.EndsWith("=", StringComparison.Ordinal) ? decodeLine.Substring(0, decodeLine.Length - 1) : (decodeLine + Environment.NewLine);
                        Array.ForEach<char>(decodeLine.ToCharArray(), (ch) => writer.WriteByte(Convert.ToByte(ch)));
                    }
                }
                writer.Flush();
                return encoding.GetString(writer.ToArray());
            }
        }

        public static Encoding GetEncoding(string charSet)
        {
            if (charSet.IsEmpty()) return Encoding.ASCII;

            string charSetUpper = charSet.ToUpperInvariant();
            if (charSetUpper.Contains("WINDOWS") || charSetUpper.Contains("CP"))
            {
                int codepageNumber = int.Parse(charSetUpper.Replace("CP", "").Replace("WINDOWS", "").Replace("-", ""), CultureInfo.InvariantCulture);
                return Encoding.GetEncoding(codepageNumber);
            }

            if (charSet.Equals("utf8", StringComparison.InvariantCultureIgnoreCase)) charSet = "utf-8";
            try
            {
                return Encoding.GetEncoding(charSet);
            }
            catch
            {
                return Encoding.ASCII;
            }
        }

        private byte[] StartBoundaryBytes1 { get { return DefaultEncoding.GetBytes(StartBoundary); } }
        private byte[] StartBoundaryBytes2 { get { return DefaultEncoding.GetBytes(CRLF + StartBoundary); } }
        private byte[] EndBoundaryBytes { get { return DefaultEncoding.GetBytes(EndBoundary); } }

        internal byte[] UnprocessedData { get; set; }

        protected bool checkSmimeType(string expectedType)
        {
            return this.ContentType.Parameters["smime-type"].IfEmpty("").ToLowerInvariant() == expectedType;
        }

        public bool IsEncrypted { get { return checkSmimeType("enveloped-data"); } }

        //Не производит обработку ошибок, выкидывает криптографические исключения
        public MimeReader Decrypt(X509Certificate2Collection certificates)
        {
            //необязательный параметр
            if (certificates == null)
                certificates = new X509Certificate2Collection();
            var encodedEncryptedMessage = this.GetContent();
            var envelopedCms = new EnvelopedCms();
            envelopedCms.Decode(encodedEncryptedMessage);
            envelopedCms.Decrypt(certificates);
            var decryptedData = envelopedCms.Encode();
            verifyValidCertificate(envelopedCms, certificates);
            return createMimeReader(decryptedData);
        }

        private void verifyValidCertificate(EnvelopedCms envelopedCms, X509Certificate2Collection certificates)
        {
            string ssn;

            try
            {
                ssn = ((System.Security.Cryptography.Xml.X509IssuerSerial)(envelopedCms.RecipientInfos[0].RecipientIdentifier.Value)).SerialNumber;
            }
            catch (Exception)
            {
                //Could not check the serial number. Ignore the exception
                return;
            }

            foreach (var cert in certificates)
            {
                if (cert.SerialNumber == ssn)
                    return;
            }

            throw new Exception("Message addressed to the wrong recipient: " + ssn);
        }

        public bool IsSigned { get { return this.ContentType.MediaType == "multipart/signed"; } }

        public MimeReader GetSignedPart()
        {
            return this.IsSigned ? this.Children[0] : this;
        }

        //выкидывает исключение, когда сообщение не подписано или подпись неверна
        public void VerifySignature(X509Certificate2Collection certificates)
        {
            if (!this.IsSigned)
                throw new Exception("Message not signed");

            if (this.Children.Count != 2)
                throw new Exception("Unexpected children count of signed message");

            //необязательный параметр
            if (certificates == null)
                certificates = new X509Certificate2Collection();

            var payload = this.Children[0];
            var signature = this.Children[1];

            var payloadBytes = payload.GetRawContent();
            var signatureBytes = signature.GetContent();

            var cms = new SignedCms(new ContentInfo(payloadBytes), true);
            cms.Decode(signatureBytes);

            //выбросит исключение, если сообщение неверно подписано
            cms.CheckSignature(certificates, true);

            //если не передан список сертификатов, сравниваем только с системными хранилищами
            if (certificates.Count == 0)
                return;

            //однако, исключения не будет, если сообщение подписано ключом не из списка certificates
            //далее проверяем, что ключ верный

            var signerCertificates = from SignerInfo si in cms.SignerInfos where si.Certificate != null select si.Certificate;

            //вначале проверяем наличие сертификата прямым путем
            if (!signerCertificates.Any(certificates.Contains))
            {
                //затем проверяем по серийному номеру - может потребоваться, когда сертификат устарел или не лежит в хранилище
                var signerSerials = from SignerInfo si in cms.SignerInfos
                                    let signerId = si.SignerIdentifier
                                    let serial = signerId.Type == SubjectIdentifierType.SubjectKeyIdentifier ? signerId.Value.ToString()
                                               : signerId.Type == SubjectIdentifierType.IssuerAndSerialNumber ? ((System.Security.Cryptography.Xml.X509IssuerSerial)signerId.Value).SerialNumber
                                               : ""
                                    where !String.IsNullOrEmpty(serial)
                                    select serial;

                foreach (var serial in signerSerials)
                {
                    var foundBySerial = certificates.Find(X509FindType.FindBySerialNumber, serial, false);
                    if (foundBySerial.Count > 0)
                        return;

                    var foundByHash = certificates.Find(X509FindType.FindByThumbprint, serial, false);
                    if (foundByHash.Count > 0)
                        return;
                }

                throw new Exception("Invalid signature");
            }
        }
    }
}