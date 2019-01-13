using System;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    /// <summary> Регистратор сообщения журнала с помощью MessageBox </summary> 
    [Serializable]
    public class CommonMessageBoxLogger
    {
        public LogMessageType Mode { get; set; }
        public string Source { get; set; }

        public CommonMessageBoxLogger() { Mode = LogMessageType.Error; }

        /// <summary> Регистрация сообщения журнала </summary>
        /// <remarks>Осторожно, блокирующий вызов (оставлен специально блокирующий вариант)
        /// нужен для пошагового уведомления пользователя (а то можно его "завалить" диалоговыми окнами с ошибками))</remarks> 
        public void WriteMessage(LogMessage message)
        {
            if (AppContext.UserInteractive && Mode.Includes(message.Type) && (Source.IsEmpty() || message.Source == Source))
            {
                var mbi = MessageBoxIcon.None;
                switch (message.Type)
                {
                    case LogMessageType.Error:
                        mbi = MessageBoxIcon.Error;
                        break;
                    case LogMessageType.Warning:
                        mbi = MessageBoxIcon.Warning;
                        break;
                    default:
                        mbi = MessageBoxIcon.None;
                        break;
                }
                MessageBox.Show(message.Text, message.Type + "", MessageBoxButtons.OK, mbi);
            }
        }
    }
}