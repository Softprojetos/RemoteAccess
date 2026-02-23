using System.Runtime.InteropServices;

namespace RemoteAccess.Desktop.Services;

public static class InputSimulator
{
    public static void MoveMouse(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void MouseClick(int x, int y, string button, bool isDown)
    {
        SetCursorPos(x, y);
        uint flag = (button, isDown) switch
        {
            ("left", true) => MOUSEEVENTF_LEFTDOWN,
            ("left", false) => MOUSEEVENTF_LEFTUP,
            ("right", true) => MOUSEEVENTF_RIGHTDOWN,
            ("right", false) => MOUSEEVENTF_RIGHTUP,
            ("middle", true) => MOUSEEVENTF_MIDDLEDOWN,
            ("middle", false) => MOUSEEVENTF_MIDDLEUP,
            _ => 0
        };
        if (flag == 0) return;

        var input = CreateMouseInput(flag, 0);
        SendInput(1, [input], INPUT.Size);
    }

    public static void MouseScroll(int delta)
    {
        var input = CreateMouseInput(MOUSEEVENTF_WHEEL, delta);
        SendInput(1, [input], INPUT.Size);
    }

    public static void KeyPress(ushort vkCode, bool isDown)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vkCode,
                    dwFlags = isDown ? 0u : KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };
        SendInput(1, [input], INPUT.Size);
    }

    public static void SimulateInput(string action, double xRatio, double yRatio,
        string button, int keyCode, int delta, int screenWidth, int screenHeight)
    {
        var x = (int)(xRatio * screenWidth);
        var y = (int)(yRatio * screenHeight);

        switch (action)
        {
            case "mouse_move":
                MoveMouse(x, y);
                break;
            case "mouse_down":
                MouseClick(x, y, button, true);
                break;
            case "mouse_up":
                MouseClick(x, y, button, false);
                break;
            case "mouse_wheel":
                MoveMouse(x, y);
                MouseScroll(delta);
                break;
            case "key_down":
                KeyPress((ushort)keyCode, true);
                break;
            case "key_up":
                KeyPress((ushort)keyCode, false);
                break;
        }
    }

    // ── P/Invoke ──────────────────────────────────────────────────
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();

    private static INPUT CreateMouseInput(uint flags, int data) => new()
    {
        type = INPUT_MOUSE,
        u = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                dwFlags = flags,
                mouseData = data,
                time = 0,
                dwExtraInfo = GetMessageExtraInfo()
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
        public static int Size => Marshal.SizeOf<INPUT>();
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
