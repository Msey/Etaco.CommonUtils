using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    /// <summary> Работа с GUI </summary>
    public static class GUIUtils
    {
        /// <summary> Блокировка обновления окна </summary>
        /// <code>
        /// LockWindowUpdate(rtbFormulaEditor.Handle);
        /// {... do somthing ...}
        /// LockWindowUpdate((IntPtr)0);   
        /// </code>
        [DllImport("user32")]
        public static extern int LockWindowUpdate(IntPtr hwnd);

        /// <summary> Возвращает двухбуквенное обозначение текущего языка </summary>
        public static string GetCurrentLang()
        {
            return Application.CurrentCulture.TwoLetterISOLanguageName;
        }

        /// <summary> Получить текущую строку для источника данных </summary>
        public static DataRow GetCurrentRow(BindingSource bindingSource)
        {
            var drv = GetCurrent(bindingSource);
            return drv == null ? null : drv.Row;
        }

        /// <summary> Получить текущую строку для источника данных </summary>
        public static DataRowView GetCurrent(BindingSource bindingSource)
        {
            return bindingSource != null ? bindingSource.Current as DataRowView : null;
        }

        /// <summary> Найти представление для данной строки </summary>
        public static DataRowView FindRow(BindingSource bs, DataRow dr)
        {
            foreach (DataRowView row in bs.List) if (dr == row.Row) return row;
            return null;
        }

        /// <summary> Устанавливает границы для контрола </summary>
        public static void SetBounds(this Control control, Rectangle r)
        {
            control.SetBounds(r.X, r.Y, r.Width, r.Height);
        }

        /// <summary>
        /// Обновить ресурсы формы в соответствии с указанной культурой
        /// </summary>
        /// <param name="someForm">Форма</param>
        /// <param name="cultureInfo">Культура</param>
        /// <param name="propertyNames">Необязательный список названий полей (поумолчанию: "Text", "Location", "Caption", "Hint")</param>
        public static void LocalizeForm(this Form someForm, CultureInfo cultureInfo, string[] propertyNames = null)
        {
            var someFormType = someForm.GetType();
            var res = new ResourceManager(someFormType);

            //зададим список свойств объектов, которые будем извлекать из файла ресурсов  
            foreach (var propertyName in propertyNames ?? new[] { "Text", "Location", "Caption", "Hint" })
            {
                //выбор всех свойств класса формы, извлечение из файла ресурсов значения, и их установка  
                foreach (var fieldInfo in someFormType.GetFields(BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance))
                {
                    var propertyInfo = fieldInfo.FieldType.GetProperty(propertyName);
                    if (propertyInfo != null)
                    {
                        var objProperty = res.GetObject("{0}.{1}".FormatStr(fieldInfo.Name, propertyInfo.Name), cultureInfo);
                        if (objProperty != null)
                        {
                            var field = fieldInfo.GetValue(someForm);
                            if (field != null)
                                propertyInfo.SetValue(field, objProperty, null);
                        }
                    }
                }
                //код для установки свойств самих форм  
                var propertyInfo1 = someFormType.GetProperty(propertyName);
                if (propertyInfo1 != null)
                {
                    var objProperty1 = res.GetObject("$this." + propertyInfo1.Name, cultureInfo);
                    if (objProperty1 != null)
                        propertyInfo1.SetValue(someForm, objProperty1, null);
                }
            }
        }

        /// <summary> Показать стандартный диалог "Открыть с помощью ..." для указанного файла </summary>
        public static void ShowOpenWithDialog(string fileName)
        {
            Process.Start(new ProcessStartInfo() { FileName = "rundll32.exe", Arguments = string.Format("shell32.dll,OpenAs_RunDLL {0}", fileName) });
        }

        /// <summary> Создаём прямоугольник со скруглёнными углами </summary>
        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            return CreateRoundedRectangle(rect.X, rect.Y, rect.Width, rect.Height, radius);
        }

        /// <summary> Создаём прямоугольник со скруглёнными углами </summary>
        public static GraphicsPath CreateRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            int xw = x + width;
            int yh = y + height;
            int xwr = xw - radius;
            int yhr = yh - radius;
            int xr = x + radius;
            int yr = y + radius;
            int r2 = radius * 2;
            int xwr2 = xw - r2;
            int yhr2 = yh - r2;

            var p = new GraphicsPath();
            p.StartFigure();

            p.AddArc(x, y, r2, r2, 180, 90);
            p.AddLine(xr, y, xwr, y);
            p.AddArc(xwr2, y, r2, r2, 270, 90);
            p.AddLine(xw, yr, xw, yhr);
            p.AddArc(xwr2, yhr2, r2, r2, 0, 90);
            p.AddLine(xwr, yh, xr, yh);
            p.AddArc(x, yhr2, r2, r2, 90, 90);
            p.AddLine(x, yhr, x, yr);

            p.CloseFigure();
            return p;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };
        [DllImport("shell32.dll")]
        static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        /// <summary> Получение иконки связанной с файлом </summary>
        public static Icon GetCorrespondingFileIcon(string filename, bool small = false)
        {
            try
            {
                var shinfo = new SHFILEINFO();
                SHGetFileInfo(filename, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), (uint)(small ? 0x101 : 0x100));
                return Icon.FromHandle(shinfo.hIcon);
            }
            catch
            {
                return null;
            }
        }

        /// <summary> Выполнение дейставия с Control из другого потока </summary>
        public static void InvokeAction(this Control control, Action action)
        {
            if (control.InvokeRequired) control.Invoke(action);
            else action();
        }

        /// <summary> Выполнение длительной операции с показом progressbar </summary>
        public static void DoLongAction(Action action)
        {
            var form = new Form() { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(139, 13), ShowInTaskbar = false };
            var panel = new Panel() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
            panel.Controls.Add(new PictureBox() { BackColor = SystemColors.ControlLightLight, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, Image = Properties.Resources.progress, SizeMode = PictureBoxSizeMode.StretchImage });
            form.Controls.Add(panel);
            try
            {
                var v = Async.DoAsync(action, x => form.InvokeAction(() => { try { form.Close(); } catch { } }));
                if (!v.Wait(100) && !form.IsDisposed) form.ShowDialog(Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
                v.GetResult();//check exception
            }
            finally
            {
                try { form.Close(); } catch { }
            }
        }

        //создание ярлыка
        [ComImport, TypeLibType(0x1040), Guid("F935DC23-1CF0-11D0-ADB9-00C04FD58A0B")]
        private interface IWshShortcut
        {
            [DispId(0)]
            string FullName { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0)] get; }
            [DispId(0x3e8)]
            string Arguments { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0x3e8)] get; [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3e8)] set; }
            [DispId(0x3e9)]
            string Description { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0x3e9)] get; [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3e9)] set; }
            [DispId(0x3ea)]
            string Hotkey { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0x3ea)] get; [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3ea)] set; }
            [DispId(0x3eb)]
            string IconLocation { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0x3eb)] get; [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3eb)] set; }
            [DispId(0x3ec)]
            string RelativePath { [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3ec)] set; }
            [DispId(0x3ed)]
            string TargetPath { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0x3ed)] get; [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3ed)] set; }
            [DispId(0x3ee)]
            int WindowStyle { [DispId(0x3ee)] get; [param: In] [DispId(0x3ee)] set; }
            [DispId(0x3ef)]
            string WorkingDirectory { [return: MarshalAs(UnmanagedType.BStr)] [DispId(0x3ef)] get; [param: In, MarshalAs(UnmanagedType.BStr)] [DispId(0x3ef)] set; }
            [TypeLibFunc(0x40), DispId(0x7d0)]
            void Load([In, MarshalAs(UnmanagedType.BStr)] string PathLink);
            [DispId(0x7d1)]
            void Save();
        }

        public static void CreateShortcut(string fileName, string targetPath, string arguments, string workingDirectory, string description, string hotkey, string iconPath)
        {
            object shell = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                var shortcut = (IWshShortcut)shell._InvokeMethod("CreateShortcut", fileName);
                shortcut.Description = description;
                shortcut.Hotkey = hotkey;
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDirectory;
                shortcut.Arguments = arguments;
                if (!iconPath.IsEmpty()) shortcut.IconLocation = iconPath;
                shortcut.Save();
            }
            finally
            {
                if(shell!= null) Marshal.ReleaseComObject(shell);
            }
        }
    }
}