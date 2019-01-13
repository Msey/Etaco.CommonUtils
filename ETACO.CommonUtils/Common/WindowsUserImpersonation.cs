using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

//[assembly: SecurityPermissionAttribute(SecurityAction.RequestMinimum, UnmanagedCode = true)]
//[assembly: PermissionSetAttribute(SecurityAction.RequestMinimum, Name = "FullTrust")]


namespace ETACO.CommonUtils
{
    /// <summary> Выполнение кода с правами определённого пользователя </summary>
    public class WindowsUserImpersonation
    {
        public const int LOGON32_LOGON_INTERACTIVE = 2;
        public const int LOGON32_PROVIDER_DEFAULT = 0;

        #region DllImport
        [DllImport("advapi32.dll")]
        public static extern int LogonUserA(String lpszUserName,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            ref IntPtr phToken);
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int DuplicateToken(IntPtr hToken,
            int impersonationLevel,
            ref IntPtr hNewToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool RevertToSelf();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern bool CloseHandle(IntPtr handle);
        #endregion

        private WindowsImpersonationContext impersonationContext;
        private readonly string userName = null;
        private string domain = null;
        private string password = null;

        public WindowsUserImpersonation() { }

        public WindowsUserImpersonation(string userName, string password)
        {
            if (!userName.IsEmpty())
            {
                var tmp = userName.Split('\\');
                this.domain = (tmp.Length == 2) ? tmp[0] : "";
                this.userName = (tmp.Length == 2) ? tmp[1] : tmp[0];
            }
            this.password = password;
        }

        public WindowsUserImpersonation(string userName, string domain, string password)
        {
            this.userName = userName;
            this.password = password;
            this.domain = domain;
        }

        /// <summary> Вход в систему </summary>
        public bool Logon()
        {

            IntPtr token = IntPtr.Zero;
            IntPtr tokenDuplicate = IntPtr.Zero;
            WindowsIdentity tempWindowsIdentity;

            if (RevertToSelf())
            {
                if (LogonUserA(userName, domain, password, LOGON32_LOGON_INTERACTIVE,
                    LOGON32_PROVIDER_DEFAULT, ref token) != 0)
                {
                    if (DuplicateToken(token, 2, ref tokenDuplicate) != 0)
                    {
                        tempWindowsIdentity = new WindowsIdentity(tokenDuplicate);
                        impersonationContext = tempWindowsIdentity.Impersonate();
                        if (impersonationContext != null)
                        {
                            CloseHandle(token);
                            CloseHandle(tokenDuplicate);
                            return true;
                        }
                    }
                }
            }
            if (token != IntPtr.Zero)
                CloseHandle(token);
            if (tokenDuplicate != IntPtr.Zero)
                CloseHandle(tokenDuplicate);
            return false;
        }

        /// <summary> Возвращение исходных прав </summary>
        public void Undo()
        {
            if (impersonationContext != null) impersonationContext.Undo();
        }

        /// <summary> Выполнение кода с правами другого пользователя </summary>
        public void DoAction(Action action)
        {
            DoAction(() => { action(); return 0; });
        }

        public T DoAction<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            try
            {
                if (!userName.IsEmpty() && !Logon()) throw new Exception("Impersonation error for user: " + userName);
                return action();
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
            finally
            {
                Undo();
            }
        }
    }
}