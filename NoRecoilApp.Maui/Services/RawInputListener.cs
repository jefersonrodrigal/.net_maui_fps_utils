using System;
using System.Runtime.InteropServices;

namespace NoRecoilApp.Maui.Services
{
    internal static unsafe class RawInputListener
    {
        private const int WM_INPUT = 0x00FF;
        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int HID_USAGE_PAGE_GENERIC = 0x01;
        private const int HID_USAGE_GENERIC_MOUSE = 0x02;

        private const ushort RI_MOUSE_LEFT_DOWN = 0x0001;
        private const ushort RI_MOUSE_LEFT_UP = 0x0002;
        private const ushort RI_MOUSE_RIGHT_DOWN = 0x0004;
        private const ushort RI_MOUSE_RIGHT_UP = 0x0008;

        // Events thread-safe
        public static event Action<bool>? OnLeftButtonChanged
        {
            add { lock (_sync) _onLeftButtonChanged += value; }
            remove { lock (_sync) _onLeftButtonChanged -= value; }
        }
        private static event Action<bool>? _onLeftButtonChanged;

        public static event Action<bool>? OnRightButtonChanged
        {
            add { lock (_sync) _onRightButtonChanged += value; }
            remove { lock (_sync) _onRightButtonChanged -= value; }
        }
        private static event Action<bool>? _onRightButtonChanged;

        private static readonly object _sync = new();
        private static Thread? _hookThread;
        private static IntPtr _hWnd = IntPtr.Zero;
        private static WndProcDelegate? _wndProcDelegate;
        private static readonly byte[] _rawBuffer = new byte[1024]; // Maior para scroll

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static void Start()
        {
            lock (_sync)
            {
                if (_hookThread?.IsAlive == true || _hWnd != IntPtr.Zero) return;

                _hookThread = new Thread(ThreadProc)
                {
                    IsBackground = true,
                    Name = "RawInputThread",
                    Priority = ThreadPriority.Highest
                };
                _hookThread.Start();
            }
        }

        public static void Stop()
        {
            lock (_sync)
            {
                if (_hWnd != IntPtr.Zero)
                {
                    DestroyWindow(_hWnd);
                    _hWnd = IntPtr.Zero;
                }
                _hookThread?.Join(100);
                _wndProcDelegate = null;
            }
        }

        private static void ThreadProc()
        {
            try
            {
                _wndProcDelegate = WndProc;
                var wndClass = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                    hInstance = GetModuleHandle(null),
                    lpszClassName = "NoRecoilRaw_" + Environment.TickCount
                };

                if (RegisterClassEx(ref wndClass) == 0) return;

                _hWnd = CreateWindowEx(0, wndClass.lpszClassName, "", 0, 0, 0, 0, 0,
                    new IntPtr(-3), IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

                if (_hWnd == IntPtr.Zero) return;

                var rid = new RAWINPUTDEVICE[1];
                rid[0] = new RAWINPUTDEVICE
                {
                    usUsagePage = (ushort)HID_USAGE_PAGE_GENERIC,
                    usUsage = (ushort)HID_USAGE_GENERIC_MOUSE,
                    dwFlags = (uint)RIDEV_INPUTSINK,
                    hwndTarget = _hWnd
                };
                _ = RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

                while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            catch { }
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT && _hWnd == hWnd)
            {
                ProcessRawInput(lParam);
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            GetRawInputData(lParam, 0x10000003u, IntPtr.Zero, ref dwSize, (uint)sizeof(RAWINPUTHEADER));

            if (dwSize > (uint)_rawBuffer.Length || dwSize == 0) return;

            fixed (byte* bufferPtr = _rawBuffer)
            {
                if (GetRawInputData(lParam, 0x10000003u, (IntPtr)bufferPtr, ref dwSize, (uint)sizeof(RAWINPUTHEADER)) == dwSize)
                {
                    var raw = Marshal.PtrToStructure<RAWINPUT>((IntPtr)bufferPtr);
                    if (raw.header.dwType == 0) // RIM_TYPEMOUSE = 0
                    {
                        var flags = raw.mouse.usButtonFlags;
                        var data = raw.mouse.usButtonData;

                        // Left button
                        if ((flags & RI_MOUSE_LEFT_DOWN) != 0)
                            _onLeftButtonChanged?.Invoke(true);
                        else if ((flags & RI_MOUSE_LEFT_UP) != 0)
                            _onLeftButtonChanged?.Invoke(false);

                        // Right button  
                        if ((flags & RI_MOUSE_RIGHT_DOWN) != 0)
                            _onRightButtonChanged?.Invoke(true);
                        else if ((flags & RI_MOUSE_RIGHT_UP) != 0)
                            _onRightButtonChanged?.Invoke(false);
                    }
                }
            }
        }

        // P/Invokes otimizados
        [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr CreateWindowEx(uint dwExStyle, [MarshalAs(UnmanagedType.LPTStr)] string lpClassName, [MarshalAs(UnmanagedType.LPTStr)] string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll", SetLastError = true)] static extern short RegisterClassEx(ref WNDCLASSEX lpwcx);
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
        [DllImport("user32.dll")] static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpmsg);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPTStr)] string? lpModuleName);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string? lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct RAWINPUT
        {
            [FieldOffset(0)] public RAWINPUTHEADER header;
            [FieldOffset(24)] public RAWMOUSE mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
    }
}
