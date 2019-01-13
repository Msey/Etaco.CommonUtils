/*using System;

namespace ETACO.CommonUtils
{
    public static class ConsoleHelper
    {
        private static int _progressPos = -1;
        /// <summary> Сбросить позицию прогресс бара </summary>
        public static void ResetProgressPosition()
        {
            _progressPos = -1;
        }

        /// <summary> Вывести сообщение в прогресс бара </summary>
        public static void WriteProgress(string text, ConsoleColor color = ConsoleColor.Gray)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (_progressPos < 0)
            {
                _progressPos = Console.CursorTop;
                Console.WriteLine(text);
            }
            else
            {
                int top = Console.CursorTop;
                int left = Console.CursorLeft;
                Console.SetCursorPosition(0, _progressPos);
                Console.Write(string.Join(" ", new string[Console.WindowWidth]));
                Console.CursorLeft = 0;
                Console.WriteLine(text);
                Console.SetCursorPosition(left, top);
            }
            Console.ForegroundColor = oldColor;
        }

        /// <summary> Вывести сообщение с определённой позиции </summary>
        public static void WriteAt(string text, int x, int y)
        {
            try
            {
                int top = Console.CursorTop;
                int left = Console.CursorLeft;
                Console.SetCursorPosition(x, y);
                Console.Write(text);
                Console.SetCursorPosition(left, top);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // <summary> Вывести сообщение определённым цветом </summary>
        public static void Write(ConsoleColor color, string text)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = oldColor;
        }
    }
}*/
