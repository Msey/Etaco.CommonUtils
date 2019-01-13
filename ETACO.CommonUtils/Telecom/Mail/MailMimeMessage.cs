using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace ETACO.CommonUtils.Telecom.Mail
{
    public class MailMimeMessage : MailMessage
    {
        public readonly List<MailMimeMessage> Children = new List<MailMimeMessage>();

        public DateTime DeliveryDate { get { return Headers["date"].IsEmpty() ? default(DateTime) : Convert.ToDateTime(Headers["date"]); } }

        public string Routing { get { return Headers["received"]; } }

        public string MessageId { get { return Headers["message-id"]; } }

        public string ReplyToMessageId { get { return TrimBrackets(Headers["in-reply-to"]); } }

        public string MimeVersion { get { return Headers["mime-version"]; } }

        public string ContentId { get { return Headers["content-id"]; } }

        public string ContentDescription { get { return Headers["content-description"]; } }

        public ContentDisposition ContentDisposition { get { return Headers["content-description"].IsEmpty() ? null : new ContentDisposition(Headers["content-description"]); } }

        public ContentType ContentType { get { return Headers["content-type"].IsEmpty() ? null : new ContentType(Headers["content-type"]); } }

        public readonly Encoding DefaultEncoding = Encoding.UTF8;

        public MailMimeMessage(MimeReader reader, Encoding defaultEncoding)
        {
            if (defaultEncoding != null) DefaultEncoding = defaultEncoding;
            if (!reader.ContentType.Boundary.IsEmpty())
            {
                BuildMultiPartMessage(reader);
            }
            else if (reader.ContentType.MediaType == "message/rfc822")
            {
                if (reader.Children.Count == 0) throw new Exception("Invalid child count on message/rfc822 entity.");
                BuildMultiPartMessage(reader.Children[0]);//заголовок внутри сообщения
            }
            else if (reader.ContentDisposition != null && reader.ContentDisposition.DispositionType == "attachment") //fix dong bug
            {
                AddAttachment(reader);
            }
            else
            {
                BuildSinglePartMessage(reader);
            }

            string value = "";
            foreach (string key in reader.Headers.AllKeys)
            {
                value = reader.Headers[key].IfEmpty(" ");
                try
                {
                    Headers.Add(key, value);
                }
                catch (FormatException) //value not ASCII (т.е. не Subject: =?koi8-r?B?79TQ1dPLwQ==?=)
                {
                    Headers.Add(key, @"=?{0}?B?{1}?=".FormatStr(DefaultEncoding.WebName, Convert.ToBase64String(DefaultEncoding.GetBytes(value)))); //некоторые присылают не ascii (c FW4 - поддерживается UTF-8 )
                }

                value = MimeReader.DecodeWord(value);
                switch (key)
                {
                    case "bcc":
                        if (!IsUndisclosed(value)) Array.ForEach(SplitAddresses(value), Bcc.Add);
                        break;
                    case "cc":
                        if (!IsUndisclosed(value)) Array.ForEach(SplitAddresses(value), CC.Add);
                        break;
                    case "from":
                        if (!IsUndisclosed(value)) From = CreateMailAddress(value);
                        break;
                    case "reply-to":
                        if (!IsUndisclosed(value)) Array.ForEach(SplitAddresses(value), ReplyToList.Add);
                        //ReplyTo = CreateMailAddress(value);
                        break;
                    case "subject":
                        Subject = value.ReplaceAny("", Environment.NewLine);
                        SubjectEncoding = Encoding.UTF8;
                        break;
                    case "to":
                        if (!IsUndisclosed(value)) Array.ForEach(SplitAddresses(value), To.Add);
                        break;
                }
            }
        }

        private static bool IsUndisclosed(string value) { return value.Trim().StartsWith("undisclosed", StringComparison.InvariantCultureIgnoreCase); }

        private MailAddress[] SplitAddresses(string addressString)
        {
            try
            {
                //return addressString.Split(",").Select(a => CreateMailAddress(a)).ToArray();//for debug
                return new MailAddressCollection() { addressString }.ToArray();
            }
            catch
            {
                return addressString.Split(",").Select(a => CreateMailAddress(a)).ToArray();
            }
        }

        private MailAddress CreateMailAddress(string address)
        {
            try
            {
                //patch для случая: Служба поддержки ЗАО <ЭТА и К> (ServiceDesk@etaco.ru) <ServiceDesk@ETACO.RU>
                address = address.Trim('\t');
                int index = address.LastIndexOf('<');
                if (index > 0) return new MailAddress(address.Substring(index, address.Length - index), address.Substring(0, index), Encoding.UTF8);

                if (!address.Contains("@")) address = address + "_@fake.com"; //patch для случая пустого адреса или без домена 
                return new MailAddress(address);
            }
            catch (FormatException e)
            {
                throw new Exception("Unable to create mail address from provided string: " + address, e);
            }
        }

        private string TrimBrackets(string value)
        {
            if (!value.IsEmpty() && value.StartsWith("<", StringComparison.Ordinal) && value.EndsWith(">", StringComparison.Ordinal))
            {
                return value.Trim('<', '>');
            }
            return value;
        }

        private MailMimeMessage AddAttachment(MimeReader reader)
        {
            //возможы FormatException для смещения времени по UTC на 0, должно быть исправленно в FW4
            var attachment = new Attachment(new MemoryStream(reader.GetContent(), false), reader.ContentType);
            var te = reader.GetTransferEncoding();
            if (te != TransferEncoding.Unknown) attachment.TransferEncoding = te; //важно установить до установки attachment.ContentDisposition.FileName (а то возможен FormatException )

            if (reader.ContentDisposition != null)
            {
                var cd = reader.ContentDisposition;
                foreach (string key in cd.Parameters.Keys)
                {
                    switch (key)
                    {
                        //PATCH: обход глюка в .NET 4 - происходит падение при следующем сценарии:
                        //var cd = new ContentDisposition();
                        //cd.Parameters.Add("creation-date", "Thu, 09 Oct 2014 09:56:31 +0400");
                        //var cd2 = new ContentDisposition();
                        //cd2.Parameters.Add("creation-date", cd.Parameters["creation-date"]);

                        case "creation-date": attachment.ContentDisposition.CreationDate = cd.CreationDate; break;
                        case "modification-date": attachment.ContentDisposition.ModificationDate = cd.ModificationDate; break;
                        case "read-date": attachment.ContentDisposition.ReadDate = cd.ReadDate; break;
                        default: attachment.ContentDisposition.Parameters.Add(key, cd.Parameters[key]); break;
                    }
                }
            }

            if (!reader.Headers["content-id"].IsEmpty())
                try { attachment.ContentId = TrimBrackets(Headers["content-id"]); }
                catch { } //refactor

            Attachments.Add(attachment);
            return this;
        }

        private MailMimeMessage AddAlternateView(MimeReader reader)
        {
            var alternateView = new AlternateView(new MemoryStream(reader.GetContent(), false), reader.ContentType);
            var te = reader.GetTransferEncoding();
            if (te != TransferEncoding.Unknown) alternateView.TransferEncoding = te; //fix bug for Content-Type: text/html;

            try { alternateView.ContentId = TrimBrackets(Headers["content-id"] + ""); }
            catch { } //refactor
            AlternateViews.Add(alternateView);
            return this;
        }

        private void BuildSinglePartMessage(MimeReader reader)//может сделать уже в итоге на основе AlternateViews???
        {
            Encoding encoding = MimeReader.GetEncoding(reader.ContentType.CharSet);
            Body = encoding.GetString(reader.GetContent());
            BodyEncoding = encoding;
            IsBodyHtml = string.Equals(MediaTypeNames.Text.Html, reader.ContentType.MediaType, StringComparison.InvariantCultureIgnoreCase);
        }

        private void BuildMultiPartMessage(MimeReader reader)
        {
            foreach (MimeReader child in reader.Children)
            {
                if (child.ContentType.MediaType.StartsWith("multipart", StringComparison.Ordinal))
                {
                    BuildMultiPartMessage(child);
                }
                else if (child.ContentType.MediaType == MediaTypeNames.Text.Html)
                {
                    AddAlternateView(child); //multipart/alternative; - parent
                }
                else if (child.ContentType.MediaType == MediaTypeNames.Text.Plain && child.ContentDisposition == null)
                {
                    BuildSinglePartMessage(child);
                }
                else if (child.ContentType.MediaType == "message/rfc822") //что делать с другими сообщениями типа message/*
                {
                    Children.Add(new MailMimeMessage(child, DefaultEncoding));
                }
                else if (child.ContentDisposition != null && !child.ContentDisposition.Inline)
                {
                    AddAttachment(child);
                }
            }
        }
    }
}