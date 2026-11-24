// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HarleAccel.Core
{
    /// <summary>
    /// Lê o mouse pelo Raw Input do Windows, inclusive com outra janela em foco (RIDEV_INPUTSINK).
    /// É só LEITURA: não injeta nada, não toca no jogo. É o mesmo tipo de leitura que softwares de mouse e o OBS fazem.
    /// Só pode existir um listener ativo por vez.
    /// </summary>
    public sealed class RawMouseListener : IDisposable
    {
        public delegate void MoveHandler(int dx, int dy, long timestamp);
        public delegate void ButtonHandler(bool leftDown, bool leftUp, long timestamp);

        public event MoveHandler? Moved;
        public event ButtonHandler? Buttons;

        /// <summary>Ticks do Stopwatch por segundo (para converter os timestamps).</summary>
        public static double TicksPerSecond => Stopwatch.Frequency;

        readonly HwndSource _source;
        bool _disposed;
        static RawMouseListener? _active;

        public RawMouseListener()
        {
            if (_active != null) throw new InvalidOperationException("Já existe uma leitura do mouse em andamento.");

            // janela invisível só para receber mensagens
            var p = new HwndSourceParameters("HarleAccelRawInput")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);

            var dev = new RAWINPUTDEVICE[]
            {
                new() { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_INPUTSINK, hwndTarget = _source.Handle }
            };
            if (!RegisterRawInputDevices(dev, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                _source.Dispose();
                throw new InvalidOperationException("Não consegui ler o mouse (RegisterRawInputDevices falhou).");
            }
            _active = this;
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_INPUT) return IntPtr.Zero;

            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0 || size > 1024) return IntPtr.Zero;

            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, buf, ref size, headerSize) != size) return IntPtr.Zero;
                var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buf);
                if (header.dwType != RIM_TYPEMOUSE) return IntPtr.Zero;

                var mouse = Marshal.PtrToStructure<RAWMOUSE>(buf + (int)headerSize);
                long ts = Stopwatch.GetTimestamp();

                if ((mouse.usFlags & MOUSE_MOVE_ABSOLUTE) == 0 && (mouse.lLastX != 0 || mouse.lLastY != 0))
                    Moved?.Invoke(mouse.lLastX, mouse.lLastY, ts);

                ushort bf = mouse.usButtonFlags;
                if ((bf & (RI_MOUSE_LEFT_BUTTON_DOWN | RI_MOUSE_LEFT_BUTTON_UP)) != 0)
                    Buttons?.Invoke((bf & RI_MOUSE_LEFT_BUTTON_DOWN) != 0, (bf & RI_MOUSE_LEFT_BUTTON_UP) != 0, ts);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var dev = new RAWINPUTDEVICE[]
            {
                new() { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero }
            };
            RegisterRawInputDevices(dev, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
            _source.RemoveHook(WndProc);
            _source.Dispose();
            if (_active == this) _active = null;
        }

        // ------------------------------------------------------------------ "está jogando?"

        /// <summary>
        /// True quando outra janela (o jogo) está em foco e o cursor está escondido.
        /// Em menu/lobby o cursor aparece, então esse tempo não entra na análise.
        /// </summary>
        public static bool IsLikelyInGame()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                GetWindowThreadProcessId(fg, out uint pid);
                if (pid == (uint)Environment.ProcessId) return false;

                var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
                if (!GetCursorInfo(ref ci)) return false;
                return (ci.flags & CURSOR_SHOWING) == 0;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ Win32

        const int WM_INPUT = 0x00FF;
        const uint RID_INPUT = 0x10000003;
        const uint RIM_TYPEMOUSE = 0;
        const uint RIDEV_INPUTSINK = 0x00000100;
        const uint RIDEV_REMOVE = 0x00000001;
        const ushort MOUSE_MOVE_ABSOLUTE = 0x01;
        const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        const int CURSOR_SHOWING = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWMOUSE
        {
            public ushort usFlags;
            public ushort padding;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll")]
        static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(ref CURSORINFO pci);
    }
}
