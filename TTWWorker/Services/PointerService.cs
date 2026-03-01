using System.Runtime.InteropServices;

namespace TTWWorker.Services;

public class PointerService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nint dwExtraInfo);

    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;

    public (int X, int Y)? CaptureCurrentPosition()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return GetCursorPos(out var point)
                ? (point.X, point.Y)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public string GetCaptureDiagnostic()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "Поддерживается только Windows 11";
        }

        return GetCursorPos(out _) ? "ok" : "GetCursorPos вернул false";
    }

    public bool MoveTo(int x, int y)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return SetCursorPos(x, y);
        }
        catch
        {
            return false;
        }
    }

    public bool LeftClick(int x, int y, int count, int delayMilliseconds = 60)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            if (!SetCursorPos(x, y)) return false;

            var delay = Math.Max(1, delayMilliseconds);
            for (var i = 0; i < Math.Max(1, count); i++)
            {
                mouse_event(LeftDown, 0, 0, 0, 0);
                mouse_event(LeftUp, 0, 0, 0, 0);
                Thread.Sleep(delay);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
