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
        if (dx == 0 && dy == 0)
        {
            return;
        }

        SendMouseInput(MouseEventMove, dx, dy);
#endif
    }

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

#if WINDOWS
    private const int MouseEventMove = 0x0001;
    private const int MouseEventRightDown = 0x0008;
    private const int MouseEventRightUp = 0x0010;
    private const int InputMouse = 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint numberOfInputs, [In] Input[] inputs, int sizeOfInputStructure);

    private static void SendMouseInput(int flags, int dx, int dy)
    {
        var input = new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Dx = dx,
                    Dy = dy,
                    MouseData = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        var inputs = new[] { input };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
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
        public int Dx;
        public int Dy;
        public uint MouseData;
        public int Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
#endif
}
