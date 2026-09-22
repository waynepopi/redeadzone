using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Deadzone.App.Services;

public sealed class TrayIconService : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    public const int WM_APP = 0x8000;
    public const int WM_TRAYCALLBACK = WM_APP + 101;

    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint MF_STRING = 0x00000000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private const int CMD_OPEN = 1001;
    private const int CMD_EXIT = 1002;

    private static readonly IntPtr IDI_APPLICATION = new(32512);

    private readonly Window _targetWindow;
    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _source;
    private bool _isAdded;
    private IntPtr _hIcon = IntPtr.Zero;

    public Action? OnRestoreRequested { get; set; }
    public Action? OnExitRequested { get; set; }

    public TrayIconService(Window targetWindow)
    {
        _targetWindow = targetWindow ?? throw new ArgumentNullException(nameof(targetWindow));
    }

    public void Initialize()
    {
        var helper = new WindowInteropHelper(_targetWindow);
        _hwnd = helper.Handle;

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        // Load standard application icon
        _hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);
    }

    public void ShowTrayIcon()
    {
        if (_isAdded || _hwnd == IntPtr.Zero)
            return;

        var nid = CreateNotifyIconData();
        _isAdded = Shell_NotifyIconW(NIM_ADD, ref nid);
    }

    public void RemoveTrayIcon()
    {
        if (!_isAdded || _hwnd == IntPtr.Zero)
            return;

        var nid = CreateNotifyIconData();
        Shell_NotifyIconW(NIM_DELETE, ref nid);
        _isAdded = false;
    }

    private NOTIFYICONDATA CreateNotifyIconData()
    {
        var nid = new NOTIFYICONDATA();
        nid.cbSize = Marshal.SizeOf<NOTIFYICONDATA>();
        nid.hWnd = _hwnd;
        nid.uID = 1;
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = WM_TRAYCALLBACK;
        nid.hIcon = _hIcon;
        nid.szTip = "Deadzone";
        return nid;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYCALLBACK)
        {
            int action = lParam.ToInt32() & 0xFFFF;
            if (action == WM_LBUTTONDBLCLK)
            {
                handled = true;
                OnRestoreRequested?.Invoke();
            }
            else if (action == WM_RBUTTONUP)
            {
                handled = true;
                ShowContextMenu();
            }
        }

        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
            return;

        try
        {
            AppendMenuW(hMenu, MF_STRING, new UIntPtr(CMD_OPEN), "Open Deadzone");
            AppendMenuW(hMenu, MF_STRING, new UIntPtr(CMD_EXIT), "Exit");

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_hwnd);

            int cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
            if (cmd == CMD_OPEN)
            {
                OnRestoreRequested?.Invoke();
            }
            else if (cmd == CMD_EXIT)
            {
                OnExitRequested?.Invoke();
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        RemoveTrayIcon();

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
