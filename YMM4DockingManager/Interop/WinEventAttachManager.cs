using System;
using System.Windows.Input;
using YMM4DockingManager.Docking;

namespace YMM4DockingManager.Interop;

/// <summary>
/// グローバルな <see cref="NativeMethods.EVENT_SYSTEM_MOVESIZEEND"/> フック。
/// ユーザーが Ctrl を押したままタイトルバー移動を終えたタイミングで、カーソル下の Dock に <see cref="DockingController.TryAttach"/> を試みる。
/// </summary>
internal static class WinEventAttachManager
{
    private static readonly NativeMethods.WinEventDelegate _callback = OnWinEvent;
    private static IntPtr _hook = IntPtr.Zero;
    private static bool _started;

    /// <summary>初回の Dock 表示などから呼ばれる。フックはプロセス単位で 1 本だけ。</summary>
    public static void EnsureStarted()
    {
        if (_started) return;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);
        _started = _hook != IntPtr.Zero;

        if (System.Windows.Application.Current != null)
        {
            System.Windows.Application.Current.Exit += (_, _) => Stop();
        }
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
    }

    public static void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        try { NativeMethods.UnhookWinEvent(_hook); } catch { }
        _hook = IntPtr.Zero;
        _started = false;
    }

    private static void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType != NativeMethods.EVENT_SYSTEM_MOVESIZEEND) return;
        if (hwnd == IntPtr.Zero) return;

        // コールバックスレッドでは DispatcherObject に触れない。Keyboard.IsKeyDown は UI スレッドで。
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null) return;

        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                    return;

                if (!NativeMethods.GetCursorPos(out var pt))
                    return;

                var panelIndex = DockTargetRegistry.FindPanelIndexUnderCursor();
                if (panelIndex == null) return;

                DockingController.TryAttach(panelIndex.Value, hwnd, out _);
            }
            catch
            {
                // no-op
            }
        }));
    }
}

