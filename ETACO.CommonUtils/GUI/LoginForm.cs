using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    /// <summary> Форма ввода параметров соединения с базой данных </summary>
    public partial class LoginForm : Form
    {
        public LoginForm(params object[] dbList)
        {
            InitializeComponent();
            TopLevel = true;
            if (dbList != null && dbList.Length > 0)
            {
                if (dbList.Length == 1 && dbList[0] + "" == "")
                    cbDatabase.DropDownStyle = ComboBoxStyle.Simple;
                else
                    cbDatabase.Items.AddRange(dbList);
            }
            else
                labelDB.Visible = cbDatabase.Visible = false;
        }

        /// <summary> Показать форму ввода параметров </summary>
        /// <returns> true - DialogResult == DialogResult.OK, иначе - false </returns>
        public bool Login(ref string username, ref string password, ref string database, string dblabel)
        {
            var loginhistory = "";
            return Login(ref username, ref password, ref database, dblabel, ref loginhistory);
        }

        /// <summary> Показать форму ввода параметров </summary>
        /// <returns> true - DialogResult == DialogResult.OK, иначе - false </returns>
        public bool Login(ref string username, ref string password, ref string database, string dblabel, string configSection)
        {
            var historyFileName = "{2}\\{0}.{1}.loginhistory".FormatStr(AppContext.AppFileName, configSection, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            var useHistory = AppContext.Config.GetParameter(configSection, "history", false);
            var history = "";
            if (useHistory && File.Exists(historyFileName))
            {
                try
                {
                    using (var sr = new StreamReader(historyFileName))
                    {
                        history = sr.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    AppContext.Log.HandleException(ex);
                }
            }

            bool result = Login(ref username, ref password, ref database, dblabel, ref history);
            if (useHistory)
            {
                try
                {
                    history.WriteToFile(historyFileName, false);
                }
                catch (Exception ex)
                {
                    AppContext.Log.HandleException(ex);
                }
            }
            return result;
        }

        /// <summary> Показать форму ввода параметров </summary>
        /// <returns> true - DialogResult == DialogResult.OK, иначе - false </returns>
        public bool Login(ref string username, ref string password, ref string database, string dblabel, ref string loginhistory)
        {
            DBLabel.Text = /*"Соединение с БД " +*/ dblabel;
            tbName.Text = username;
            cmsMRULogins.Items.Clear();
            var mru = new  HashSet<string>();
            if (!loginhistory.IsEmpty())
            {
                foreach (var s in loginhistory.Split('|','\r','\n'))
                  if(s != "" && mru.Add(s))
                      cmsMRULogins.Items.Add(s, null, onMRUSelect);
            }
            tbName.Width += cmsMRULogins.Items.Count==0 ? btnHistory.Width : 0;
            btnHistory.Visible = cmsMRULogins.Items.Count > 0;
            tbPassword.Text = password;
            cbDatabase.Text = database;
            ActiveControl = username == "" ? tbName : tbPassword;
            var res = ShowDialog() == DialogResult.OK;
            if (res)
            {
                username = tbName.Text;
                password = tbPassword.Text;
                database = cbDatabase.Text;
                mru.Add(username+"@"+database);
                loginhistory = "";
                foreach (var s in mru)
                    loginhistory += s + "|";
            }
            return res;
        }

        private void onMRUSelect(Object sender, EventArgs e)
        { 
            var mruitem = sender as ToolStripItem;
            int atpos = mruitem.Text.IndexOf('@');
            if (atpos == -1)
                atpos = mruitem.Text.Length;
            if (atpos > 0)
                tbName.Text = mruitem.Text.Substring(0, atpos);
            if (atpos+1 < mruitem.Text.Length)
                cbDatabase.Text = mruitem.Text.Substring(atpos+1);
        }
        
        /// <summary> Показать форму ввода параметров </summary>
        /// <returns> true - DialogResult == DialogResult.OK, иначе - false </returns>
        public static bool GetLogin(ref string username, ref string password, ref string database, string dblabel)
        {
            var loginhistory = "";
            return GetLogin(ref username, ref password, ref database, dblabel, ref loginhistory);
        }

        /// <summary> Показать форму ввода параметров </summary>
        /// <returns> true - DialogResult == DialogResult.OK, иначе - false </returns>
        public static bool GetLogin(ref string username, ref string password, ref string database, string dblabel, string sectionname, object[] dbList = null)
        {
            using (var lf = new LoginForm(dbList))
                return lf.Login(ref username, ref password, ref database, dblabel, sectionname);
        }

        /// <summary> Показать форму ввода параметров </summary>
        /// <returns> true - DialogResult == DialogResult.OK, иначе - false </returns>
        public static bool GetLogin(ref string username, ref string password, ref string database, string dblabel, ref string loginhistory)
        {
            using (var lf = new LoginForm())
                return lf.Login(ref username, ref password, ref database, dblabel, ref loginhistory);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            cmsMRULogins.Show(btnHistory,10,10);
        }

        private void LoginForm_Shown(object sender, EventArgs e)
        {
            if(!cbDatabase.Visible)
                Height -= cbDatabase.Height;
        }
    }
}
