using System;
using System.Runtime.InteropServices;

namespace NoRecoilApp.Maui.Services;

internal static class NativeInput
{
    public const int VkF2 = 0x71;
    public const int VkF3 = 0x72;
    public const int VkLButton = 0x01;
    public const int VkRButton = 0x02;

    public static bool IsKeyDown(int virtualKey)
    {
#if WINDOWS
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
#else
        return false;
#endif
    }

    public static void SendRelativeMouseMove(int dx, int dy)
    {
#if WINDOWS
        if (dx == 0 && dy == 0) return;
        SendMouseInput(MouseEventMove, dx, dy);
#endif
    }

    public static void SendMouseInput(int dx, int dy) =>
        SendRelativeMouseMove(dx, dy);

    public static void SendRightButtonDown()
    {
#if WINDOWS
        SendMouseInput(MouseEventRightDown, 0, 0);
#endif
    }

    public static void SendRightButtonUp()
    {
#if WINDOWS
        SendMouseInput(MouseEventRightUp, 0, 0);
#endif
    }

    public static void SendLeftButtonDown()
    {
#if WINDOWS
        SendMouseInput(MouseEventLeftDown, 0, 0);
#endif
    }

    public static void SendLeftButtonUp()
    {
#if WINDOWS
        SendMouseInput(MouseEventLeftUp, 0, 0);
#endif
    }

#if WINDOWS
    private const int MouseEventMove      = 0x0001;
    private const int MouseEventLeftDown  = 0x0002;
    private const int MouseEventLeftUp    = 0x0004;
    private const int MouseEventRightDown = 0x0008;
    private const int MouseEventRightUp   = 0x0010;
    private const int InputMouse          = 0;

    private static readonly int     InputSize          = Marshal.SizeOf<Input>();
    private static readonly Input[] _singleInputArray  = new Input[1];

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint numberOfInputs,
        [In] Input[] inputs,
        int sizeOfInputStructure);

    private static void SendMouseInput(int flags, int dx, int dy)
    {
        var array = _singleInputArray[0];

        array.Type                 = InputMouse;
        array.Data.Mouse.Dx        = dx;
        array.Data.Mouse.Dy        = dy;
        array.Data.Mouse.MouseData = 0;
        array.Data.Mouse.Flags     = flags;
        array.Data.Mouse.Time      = 0;
        array.Data.Mouse.ExtraInfo = IntPtr.Zero;

        _singleInputArray[0] = array;

        SendInput(1, _singleInputArray, InputSize);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int        Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int    Dx;
        public int    Dy;
        public uint   MouseData;
        public int    Flags;
        public uint   Time;
        public IntPtr ExtraInfo;
    }
#endif
}
