using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace NalaApps.Action.App;

internal sealed class WindowsInputRecorder : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WhKeyboardLl = 13;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x10;
    private const uint LlmhfInjected = 0x01;

    private readonly Action<ActionStepItem> _onStep;
    private readonly Stopwatch _clock = new();
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private long _lastEventMs;
    private long _lastMoveMs;
    private int _lastMoveX = int.MinValue;
    private int _lastMoveY = int.MinValue;

    public WindowsInputRecorder(Action<ActionStepItem> onStep)
    {
        _onStep = onStep;
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public bool IsRecording => _mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero;

    public void Start()
    {
        if (IsRecording) return;
        _clock.Restart();
        _lastEventMs = 0;
        IntPtr module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, module, 0);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, module, 0);
        if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
        {
            Stop();
            throw new InvalidOperationException("Windows 입력 녹화 훅을 시작할 수 없습니다.");
        }
    }

    public void Stop()
    {
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = IntPtr.Zero;
        _keyboardHook = IntPtr.Zero;
        _clock.Stop();
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
            if ((data.Flags & LlmhfInjected) == 0)
            {
                int message = wParam.ToInt32();
                long now = _clock.ElapsedMilliseconds;
                if (message == WmMouseMove)
                {
                    int distance = Math.Abs(data.Point.X - _lastMoveX) + Math.Abs(data.Point.Y - _lastMoveY);
                    if (now - _lastMoveMs >= 180 && distance >= 12)
                    {
                        Emit("MouseMove", "마우스 이동", $"X={data.Point.X}, Y={data.Point.Y}", data.Point.X, data.Point.Y, 0, null, now);
                        _lastMoveMs = now;
                        _lastMoveX = data.Point.X;
                        _lastMoveY = data.Point.Y;
                    }
                }
                else if (message is WmLButtonDown or WmLButtonUp or WmRButtonDown or WmRButtonUp or WmMButtonDown or WmMButtonUp)
                {
                    string button = message is WmLButtonDown or WmLButtonUp ? "Left" : message is WmRButtonDown or WmRButtonUp ? "Right" : "Middle";
                    string state = message is WmLButtonDown or WmRButtonDown or WmMButtonDown ? "Down" : "Up";
                    Emit("MouseButton", $"마우스 {ButtonLabel(button)} {StateLabel(state)}", $"{button} {state}, X={data.Point.X}, Y={data.Point.Y}", data.Point.X, data.Point.Y, 0, $"{button}:{state}", now);
                }
                else if (message == WmMouseWheel)
                {
                    int delta = unchecked((short)((data.MouseData >> 16) & 0xffff));
                    Emit("MouseWheel", delta > 0 ? "마우스 휠 위" : "마우스 휠 아래", $"Delta={delta}", data.Point.X, data.Point.Y, delta, null, now);
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() is WmKeyDown or WmSysKeyDown)
        {
            KeyboardHookData data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            if ((data.Flags & LlkhfInjected) == 0)
            {
                Key key = KeyInterop.KeyFromVirtualKey((int)data.VirtualKeyCode);
                string modifiers = GetModifiers();
                string detail = string.IsNullOrEmpty(modifiers) ? key.ToString() : $"{modifiers}+{key}";
                Emit("KeyPress", "키보드 입력", detail, 0, 0, (int)data.VirtualKeyCode, detail, _clock.ElapsedMilliseconds);
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private void Emit(string type, string name, string detail, int x, int y, int value, string? text, long now)
    {
        int delay = (int)Math.Clamp(now - _lastEventMs, 0, 60000);
        _lastEventMs = now;
        _onStep(new ActionStepItem
        {
            Type = type,
            Name = name,
            Detail = detail,
            X = x,
            Y = y,
            Value = value,
            Text = text ?? string.Empty,
            DelayBeforeMs = delay
        });
    }

    private static string GetModifiers()
    {
        List<string> values = [];
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) values.Add("Ctrl");
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0) values.Add("Alt");
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) values.Add("Shift");
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) values.Add("Win");
        return string.Join("+", values);
    }

    private static string ButtonLabel(string button) => button switch { "Left" => "왼쪽", "Right" => "오른쪽", _ => "가운데" };
    private static string StateLabel(string state) => state == "Down" ? "누름" : "놓음";

    public void Dispose() => Stop();

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] private struct PointData { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData { public PointData Point; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardHookData { public uint VirtualKeyCode; public uint ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
}

internal static class WindowsInputPlayer
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020;
    private const uint MouseMiddleUp = 0x0040;
    private const uint MouseWheel = 0x0800;
    private const uint KeyUp = 0x0002;

    public static async Task PlayAsync(IEnumerable<ActionStepItem> steps, CancellationToken token, Action<ActionStepItem> progress)
    {
        foreach (ActionStepItem step in steps)
        {
            token.ThrowIfCancellationRequested();
            if (!step.Enabled) continue;
            await Task.Delay(Math.Clamp(step.DelayBeforeMs, 0, 60000), token);
            progress(step);
            switch (step.Type)
            {
                case "MouseMove":
                    SetCursorPos(step.X, step.Y);
                    break;
                case "MouseButton":
                    SetCursorPos(step.X, step.Y);
                    PlayMouseButton(step.Text);
                    break;
                case "MouseWheel":
                    SendMouse(MouseWheel, step.Value);
                    break;
                case "KeyPress":
                    PlayKey(step.Value);
                    break;
                case "TextInput":
                    foreach (char character in step.Text)
                    {
                        token.ThrowIfCancellationRequested();
                        SendUnicode(character);
                    }
                    break;
                case "Wait":
                    await Task.Delay(Math.Clamp(step.Value, 0, 600000), token);
                    break;
            }
        }
    }

    private static void PlayMouseButton(string value)
    {
        string[] parts = value.Split(':');
        string button = parts.ElementAtOrDefault(0) ?? "Left";
        string state = parts.ElementAtOrDefault(1) ?? "Down";
        uint flag = (button, state) switch
        {
            ("Left", "Down") => MouseLeftDown,
            ("Left", "Up") => MouseLeftUp,
            ("Right", "Down") => MouseRightDown,
            ("Right", "Up") => MouseRightUp,
            ("Middle", "Down") => MouseMiddleDown,
            _ => MouseMiddleUp
        };
        SendMouse(flag, 0);
    }

    private static void PlayKey(int virtualKey)
    {
        if (virtualKey <= 0) return;
        Input[] inputs =
        [
            new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = (ushort)virtualKey } } },
            new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = (ushort)virtualKey, Flags = KeyUp } } }
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendUnicode(char character)
    {
        const uint unicode = 0x0004;
        Input[] inputs =
        [
            new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { ScanCode = character, Flags = unicode } } },
            new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { ScanCode = character, Flags = unicode | KeyUp } } }
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendMouse(uint flags, int data)
    {
        Input[] inputs = [new() { Type = InputMouse, Data = new InputUnion { Mouse = new MouseInput { MouseData = unchecked((uint)data), Flags = flags } } }];
        SendInput(1, inputs, Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int Dx; public int Dy; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
