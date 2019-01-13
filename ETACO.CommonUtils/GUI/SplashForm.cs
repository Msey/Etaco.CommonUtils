using System.Windows.Forms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ETACO.CommonUtils
{
    /// <summary> Отображение формы с заставкой </summary>
    public sealed partial class SplashForm : Form
    {
        private static SplashForm instance = null;

        /// <summary> Показать форму с заставкой </summary>
        public static void ShowSplash(string SysName, string AppName = "", string Ver = "", string CopyRight = "")
        {
            if (instance == null)
                instance = new SplashForm();
            instance.SetInfo(SysName, AppName, Ver, CopyRight);
            
            instance.Show();
            instance.Update();
            instance.BringToFront();
        }

        /// <summary>
        /// Проверка "видимости" окна заставки
        /// </summary>
        public static bool SplashIsVisible()
        {
            return instance != null;
        }

        /// <summary>
        /// Установка фоновой картинки
        /// </summary>
        /// <param name="img">Картинка</param>
        /// <param name="layout">Режим показа картинки</param>
        /*public static void SetSplashBGImage(Image img, ImageLayout layout = ImageLayout.Tile)
        {
            if (instance != null)
            {
                instance.panel1.BackgroundImage = img;
                instance.panel1.BackgroundImageLayout = layout;
                instance.Refresh();
            }
        }*/


        /// <summary>
        /// Установка информационного сообщения процесса загрузки
        /// </summary>
        /// <param name="msg">Текст сообщения</param>
        public static void SetSplashProgressMessage(string msg)
        {
            if (instance != null)
            {
                instance.labelProgress.Text = msg;
                instance.Refresh();
            }
        }

        /// <summary>
        /// Установка названия приложения
        /// </summary>
        /// <param name="appName">Название приложения</param>
        public static void SetSplashAppName(string appName)
        {
            if (instance != null)
            {
                instance.labelAppName.Text = appName;
                instance.Refresh();
            }
        }

        /// <summary> Показать форму "О программе ..." </summary>
        public static void ShowAbout(Form Parent, string SysName, string AppName, string Ver, string CopyRight, string CopyRight2 = "")
        {
            using(var sf = new SplashForm(Parent))
                sf.About(SysName, AppName, Ver, CopyRight, CopyRight2);
        }

        /// <summary> Скрыть форму с заставкой </summary>
        public static void HideSplash()
        {
            if (instance != null)
            {
                instance.Close();
                instance.Dispose();
                instance = null;
            }
        }

        /// <summary> Переместить форму с заставкой на задний фон</summary>
        public static void Blind()
        {
            if (instance != null)
            {
                instance.SendToBack();
            }
        }

        /// <summary> Переместить форму с заставкой на передний фон</summary>
        public static void UnBlind()
        {
            if (instance != null)
            {
                instance.BringToFront();
                instance.Update();
            }
        }

        private SplashForm(Form Parent = null)
        {
            InitializeComponent();
            Owner = Parent;
            StartPosition = Owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            TopLevel = true;
            labelSysName.Text = Application.ProductName;
            labelAppName.Text = "";
            labelVersion.Text = "v " + Application.ProductVersion;
            labelCopyRight.Text = "Copyright " + Application.CompanyName;
            labelProgress.Text = "";
            //panel1.BackColor
        }

        private void SetInfo(string SysName, string AppName, string Ver, string CopyRight, string CopyRight2 = "")
        {
            labelSysName.Text = SysName;
            if (AppName != "")
                labelAppName.Text = AppName;
            var ver = AppContext.GetEntryAssembly().GetName().Version;

            if (Ver.Contains("[]"))
                Ver = Ver.Replace("[]", ver.Major == 0 && ver.Minor == 0 && ver.Build == 0  ? "" : "{0}.{1}.{2}".FormatStr(ver.Major, ver.Minor, ver.Build));
            if (Ver.Contains("[1]"))
                Ver = Ver.Replace("[1]", ver.Major == 0 ? "" : ver.Major + "");
            if (Ver.Contains("[2]"))
                Ver = Ver.Replace("[2]", ver.Minor == 0 ? "" : ver.Minor + "");
            if (Ver.Contains("[3]"))
                Ver = Ver.Replace("[3]", ver.Build == 0 ? "" : ver.Build + "");
            if (Ver.Contains("[4]"))
                Ver = Ver.Replace("[4]", ver.Revision == 0 ? "" : ver.Revision + "");
            if (Ver != "")
                labelVersion.Text = Ver;
            if(CopyRight != "")
                labelCopyRight.Text = CopyRight;
            labelCopyRight2.Text = CopyRight2;
        }

        private void About(string SysName, string AppName, string Ver, string CopyRight, string CopyRight2="")
        {
            SetInfo(SysName, AppName, Ver, CopyRight, CopyRight2);
            labelSysName.MouseClick += Close_MouseClick;
            labelAppName.MouseClick += Close_MouseClick;
            labelVersion.MouseClick += Close_MouseClick;
            labelProgress.MouseClick += Close_MouseClick;
            Logo.MouseClick += Close_MouseClick;
            //if (CopyRight2 + "" != "")
            //    labelCopyRight.MouseClick += labelCopyRight_MouseClick;

			if (!SystemInformation.TerminalServerSession)
			{
				Opacity = 0;
				var tmr = new Timer { Tag = this, Interval = 20 };
				tmr.Tick += new EventHandler(tmr_Tick);
				tmr.Start();
			}
            StartPosition = Owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;

            ShowDialog(Owner);
        }

        private static void tmr_Tick(object sender, EventArgs e)
        {
            var tmr = sender as Timer;
            var frm = tmr.Tag as Form;
            if (frm == null)
            {
                tmr.Stop();
                tmr.Dispose();
                return;
            }
            try
            {
                frm.Opacity += 0.06;
                //frm.Update();
                if (frm.Opacity >= 0.99) tmr.Tag = null;
            }
            catch { }
        }

        private void Close_MouseClick(object sender, MouseEventArgs e)
        {
            Close();
        }

        private void labelCopyRight_MouseClick(object sender, MouseEventArgs e)
        {
            if (labelCopyRight2.Text != "")
            {
                labelCopyRight2.Visible = !labelCopyRight2.Visible;
                if (labelCopyRight2.Visible)
                    labelCopyRight2.BringToFront();
                else
                    labelCopyRight2.SendToBack();
            }
            else
                Close_MouseClick(sender, e);
        }

        private void labelCopyRight_MouseEnter(object sender, EventArgs e)
        {
            if (labelCopyRight2.Text != "")
            { 
                labelCopyRight.Font = new Font("Tahoma", 9.75F, FontStyle.Underline); 
                labelCopyRight.Cursor = Cursors.Hand;
            }
        }

        private void labelCopyRight_MouseLeave(object sender, EventArgs e)
        {
            if (labelCopyRight2.Text != "")
            {
                labelCopyRight.Font = new Font("Tahoma", 9.75F, FontStyle.Regular);
                labelCopyRight.Cursor = Cursors.Default;
            }
        }

    }

}