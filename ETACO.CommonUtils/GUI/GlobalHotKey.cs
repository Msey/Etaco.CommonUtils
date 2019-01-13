/*using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ETACO.CommonUtils
{
    /// <summary> Работа с 'горячими клавишами' </summary>
    public class GlobalHotkeys : IDisposable
    {
        public const int WM_HOTKEY = 0x312;  
        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32", SetLastError = true)]
        private static extern int UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("kernel32", SetLastError = true)]
        private static extern short GlobalAddAtom(string lpString);
        [DllImport("kernel32", SetLastError = true)]
        private static extern short GlobalDeleteAtom(short nAtom);

        private readonly List<HotKeyInfo> registeredHotKeys = new List<HotKeyInfo>();
        private DefaultControl control = null;

        public GlobalHotkeys() 
        {
            control = new DefaultControl(this);
        }

        /// <summary> Регистрация обработчика горячей клавиши </summary>
        public GlobalHotkeys Register(Keys key, Action<Message> action)
        {
            //Unregister(key);
            var hki = registeredHotKeys.Find(k => k.Key == key);
            if (hki != null)
            {
                hki.Actions.Add(action);
            }
            else
            {
                var modifiers = KeysModifiers.None;

                if ((key & Keys.Alt) == Keys.Alt)           modifiers = modifiers | KeysModifiers.Alt;
                if ((key & Keys.Control) == Keys.Control)   modifiers = modifiers | KeysModifiers.Control;
                if ((key & Keys.Shift) == Keys.Shift)       modifiers = modifiers | KeysModifiers.Shift;

                var k = key & ~Keys.Control & ~Keys.Shift & ~Keys.Alt;
                try
                {
                    var atomName = typeof(DefaultControl) + "" + DateTime.Now.Millisecond;
                    var hotkeyID = GlobalAddAtom(atomName);
                    if (hotkeyID == 0) throw new Exception("Unable to generate unique hotkey ID. Error: " + Marshal.GetLastWin32Error());
                    if (!RegisterHotKey(control.Handle, hotkeyID, (uint)modifiers, (uint)k)) throw new Exception("Unable to register hotkey. Error: " + Marshal.GetLastWin32Error());
                    registeredHotKeys.Add(new HotKeyInfo(key, hotkeyID, action));
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }
            return this;
        }

        private void Unregister(HotKeyInfo hki)
        {
            if (hki.ID != 0)
            {
                UnregisterHotKey(control.Handle, hki.ID);
                GlobalDeleteAtom(hki.ID);
                registeredHotKeys.Remove(hki);
            }
        }

        /// <summary> Отключение ранее зарегистрированной горячей клавиши </summary>
        public GlobalHotkeys Unregister(Keys key)
        {
            var hki = registeredHotKeys.Find((k) => k.Key == key);
            if (hki != null) Unregister(hki);
            return this;
        }

        /// <summary> Отключение всех ранее зарегистрированных горячих клавиш </summary>
        public GlobalHotkeys UnregisterAll()
        {
            for (int i = registeredHotKeys.Count - 1; i >= 0; i--) Unregister(registeredHotKeys[i]);
            return this;
        }

        /// <summary> Отключение всех ранее зарегистрированных горячих клавиш </summary>
        public void Dispose()
        {
            UnregisterAll();
        }

        private class DefaultControl:Control
        {
            readonly GlobalHotkeys ghk = null;
            public DefaultControl(GlobalHotkeys ghk) 
            {
                this.ghk = ghk;
            }
            
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == WM_HOTKEY) foreach (var msg in ghk.registeredHotKeys) if ((short)m.WParam == msg.ID) msg.Execute(m);
            }
        }

        private enum KeysModifiers
        {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            Windows = 8
        }

        private class HotKeyInfo
        {
            public Keys Key;
            public short ID;
            public readonly List<Action<Message>> Actions = new List<Action<Message>>();

            public HotKeyInfo(Keys key, short id, Action<Message> action)
            {
                Key = key;
                ID = id;
                Actions.Add(action);
            }

            public void Execute(Message m)
            {
                Actions.ForEach((a) => a(m));
            }
        }
    }
}*/
