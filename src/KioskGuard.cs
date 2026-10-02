using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pintinho
{
    // Hook de teclado global: engole as teclas que tirariam a criança do app.
    class KioskGuard : IDisposable
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104;
        const int VK_TAB = 0x09, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_ESCAPE = 0x1B;
        const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_APPS = 0x5D, VK_F4 = 0x73, VK_Q = 0x51;
        const int LLKHF_ALTDOWN = 0x20;

        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public int vkCode; public int scanCode; public int flags; public int time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        readonly LowLevelKeyboardProc proc; // mantém o delegate vivo (senão o GC coleta)
        IntPtr hook;

        public event EventHandler ExitRequested;

        public KioskGuard()
        {
            proc = HookProc;
            using (Process p = Process.GetCurrentProcess())
            using (ProcessModule m = p.MainModule)
                hook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(m.ModuleName), 0);
        }

        static bool Pressed(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                int vk = k.vkCode;
                bool down = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool alt = (k.flags & LLKHF_ALTDOWN) != 0;
                bool ctrl = Pressed(VK_CONTROL);

                if (down && vk == VK_Q && ctrl && Pressed(VK_SHIFT))
                {
                    if (ExitRequested != null) ExitRequested(this, EventArgs.Empty);
                    return (IntPtr)1;
                }
                if (vk == VK_LWIN || vk == VK_RWIN || vk == VK_APPS) return (IntPtr)1;
                if (alt && (vk == VK_TAB || vk == VK_ESCAPE || vk == VK_F4)) return (IntPtr)1;
                if (ctrl && vk == VK_ESCAPE) return (IntPtr)1;
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }
    }
}
