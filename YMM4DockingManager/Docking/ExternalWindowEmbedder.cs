using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using YMM4DockingManager.Interop;

namespace YMM4DockingManager.Docking;

/// <summary>
/// 外部プロセスのトップレベル HWND を WinForms のホスト <see cref="System.Windows.Forms.Panel"/> の子にするための P/Invoke 操作。
/// キャプション削除・WS_CHILD 化・SetParent・サイズ調整・デタッチ時のスタイル復元を担当する。
/// </summary>
internal static class ExternalWindowEmbedder
{
    public static bool TryGetWindowProcessInfo(IntPtr hwnd, out string? processName, out string? exePath)
    {
        processName = null;
        exePath = null;
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return false;

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return false;

            using var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;

            try
            {
                exePath = proc.MainModule?.FileName;
            }
            catch
            {
                // Access deniedなどがあり得る
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetWindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return null;
        var sb = new StringBuilder(512);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        var t = sb.ToString();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    public static string? GetWindowClass(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return null;
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        var c = sb.ToString();
        return string.IsNullOrWhiteSpace(c) ? null : c;
    }

    public static bool IsExplorerWindow(string? processName, string? className)
    {
        if (!string.IsNullOrWhiteSpace(processName) &&
            processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            return true;

        // 一般的なエクスプローラーのクラス名
        if (!string.IsNullOrWhiteSpace(className) &&
            (className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
             className.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    public static void EmbedIntoWinFormsPanel(IntPtr hwnd, IntPtr hostPanelHandle)
    {
        if (hwnd == IntPtr.Zero) throw new ArgumentException("Invalid hwnd.", nameof(hwnd));
        if (hostPanelHandle == IntPtr.Zero) throw new ArgumentException("Invalid host handle.", nameof(hostPanelHandle));

        // 子ウィンドウとして埋め込むため、タイトルバー相当のスタイルを落とし WS_CHILD を付与する
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
        style &= ~NativeMethods.WS_CAPTION;
        style &= ~NativeMethods.WS_THICKFRAME;
        style &= ~NativeMethods.WS_MINIMIZEBOX;
        style &= ~NativeMethods.WS_MAXIMIZEBOX;
        style &= ~NativeMethods.WS_SYSMENU;
        style |= NativeMethods.WS_CHILD;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);

        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~NativeMethods.WS_EX_DLGMODALFRAME;
        exStyle &= ~NativeMethods.WS_EX_CLIENTEDGE;
        exStyle &= ~NativeMethods.WS_EX_STATICEDGE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        // 親 HWND をデスクトップからホスト Panel に差し替え（以降クライアント座標系でレイアウト）
        NativeMethods.SetParent(hwnd, hostPanelHandle);
    }

    /// <summary>ホスト Panel の ClientSize に合わせて子を伸縮。SWP_ASYNCWINDOWPOS でデッドロックを避けやすくする。</summary>
    public static void ResizeEmbeddedWindow(IntPtr hwnd, int width, int height)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return;
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            Math.Max(1, width),
            Math.Max(1, height),
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_ASYNCWINDOWPOS);
    }

    /// <summary>タブ非表示時用。親を外し画面外へ小さく移動（ユーザーには見えない位置）。スタイルは触らない。</summary>
    public static void TempDetachToHidden(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return;
        try
        {
            NativeMethods.SetParent(hwnd, IntPtr.Zero);
            // 典型的な「画面外」座標。完全復元は TryFullDetach 側で行う
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 100, 100, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
        catch { }
    }

    /// <summary>ユーザー解除や終了時。デスクトップの子に戻し、保存していた GWL_STYLE / EXSTYLE を復元して見えるサイズにする。</summary>
    public static void FullDetachRestore(IntPtr hwnd, int originalStyle, int originalExStyle)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return;

        try
        {
            NativeMethods.SetParent(hwnd, IntPtr.Zero);

            // 0 のときは未取得（異常系）なので上書きしない
            if (originalStyle != 0)
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, originalStyle);
            if (originalExStyle != 0)
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, originalExStyle);

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 100, 100, 1280, 720,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }
        catch { }
    }
}

