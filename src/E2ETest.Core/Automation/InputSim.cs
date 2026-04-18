#if NET8_PLUS
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace E2ETest.Core.Automation
{
    /// <summary>SendInput/keybd_event 기반 입력 시뮬레이션. 실제 메시지 큐에 들어가므로 UIA로 안되는 레거시 앱에도 동작.</summary>
    internal static class InputSim
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        public static void MouseMove(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void MouseClickLeft()
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            inputs[1].type = INPUT_MOUSE;
            inputs[1].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(30);
        }

        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var inputs = new INPUT[text.Length * 2];
            int idx = 0;
            foreach (var ch in text)
            {
                inputs[idx].type = INPUT_KEYBOARD;
                inputs[idx].u.ki.wScan = ch;
                inputs[idx].u.ki.dwFlags = KEYEVENTF_UNICODE;
                idx++;
                inputs[idx].type = INPUT_KEYBOARD;
                inputs[idx].u.ki.wScan = ch;
                inputs[idx].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
                idx++;
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(30);
        }

        public static void SelectAllAndType(string text)
        {
            // Ctrl+A 후 타이핑
            SendVk(0x11, false); // VK_CONTROL down
            SendVk(0x41, false); // A down
            SendVk(0x41, true);  // A up
            SendVk(0x11, true);  // VK_CONTROL up
            TypeText(text);
        }

        private static void SendVk(ushort vk, bool keyUp)
        {
            var input = new INPUT[1];
            input[0].type = INPUT_KEYBOARD;
            input[0].u.ki.wVk = vk;
            input[0].u.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
#endif
