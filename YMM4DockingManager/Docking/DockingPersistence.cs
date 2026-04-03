using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YMM4DockingManager.Settings;

namespace YMM4DockingManager.Docking;

/// <summary>
/// <see cref="DockingManagerSettings"/>（JSON）と <see cref="DockingStateStore"/> の同期、および保存済みヒントからの自動復元。
/// ランタイム専用フィールド（AttachedHwnd, OriginalStyle 等）は JSON に書かない。
/// </summary>
internal static class DockingPersistence
{
    /// <summary>起動時など。ディスク上の ExePath / ProcessName 等をストアへ読み込む（まだ SetParent はしない）。</summary>
    public static void LoadIntoState()
    {
        var settings = DockingManagerSettings.Instance;

        foreach (var p in settings.Panels.Take(10))
        {
            DockingStateStore.Update(p.PanelIndex, s =>
            {
                s.ExePath = p.ExePath;
                s.ProcessName = p.ProcessName;
                s.WindowTitleHint = p.WindowTitleHint;
                s.WindowClassHint = p.WindowClassHint;
            });
        }
    }

    /// <summary>ストアの「復元に使うヒント」だけを各 Panel の設定に反映しファイルへ保存する。</summary>
    public static void SaveFromState()
    {
        var settings = DockingManagerSettings.Instance;

        for (int i = 1; i <= 10; i++)
        {
            var s = DockingStateStore.Get(i);
            var p = settings.Panels.FirstOrDefault(x => x.PanelIndex == i);
            if (p == null)
            {
                p = new DockingPanelSettings { PanelIndex = i };
                settings.Panels.Add(p);
            }

            p.ExePath = s.ExePath;
            p.ProcessName = s.ProcessName;
            p.WindowTitleHint = s.WindowTitleHint;
            p.WindowClassHint = s.WindowClassHint;
        }

        settings.Save();
    }

    /// <summary>Dock 01〜10 それぞれで、未アタッチかつヒントがあればプロセスから MainWindowHandle を探して TryAttach。</summary>
    public static void TryAutoRestoreAll()
    {
        for (int i = 1; i <= 10; i++)
            TryAutoRestore(i);
    }

    public static void TryAutoRestore(int panelIndex)
    {
        var s = DockingStateStore.Get(panelIndex);
        if (s.AttachedHwnd != IntPtr.Zero) return;

        var hwnd = FindWindowFromHints(s.ProcessName, s.ExePath);
        if (hwnd == IntPtr.Zero) return;

        if (DockingController.TryAttach(panelIndex, hwnd, out _))
        {
            // attach成功で最新情報が入るので保存
            SaveFromState();
        }
    }

    /// <summary>exe 名またはプロセス名で候補プロセスを列挙し、メインウィンドウのあるものを 1 件返す。</summary>
    private static IntPtr FindWindowFromHints(string? processName, string? exePath)
    {
        try
        {
            Process? proc = null;

            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var name = Path.GetFileNameWithoutExtension(exePath);
                if (!string.IsNullOrWhiteSpace(name))
                    proc = Process.GetProcessesByName(name).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            }

            if (proc == null && !string.IsNullOrWhiteSpace(processName))
                proc = Process.GetProcessesByName(processName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

            return proc?.MainWindowHandle ?? IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}

