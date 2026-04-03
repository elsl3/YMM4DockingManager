using System;
using System.Diagnostics;
using System.IO;
using YMM4DockingManager.Interop;

namespace YMM4DockingManager.Docking;

/// <summary>
/// アタッチ／デタッチのオーケストレーション。
/// WinEvent からの <see cref="TryAttach"/>、View からの再アタッチ、ユーザー解除、YMM4 終了時の親だけ外しまで、
/// 状態は <see cref="DockingStateStore"/>、永続化は <see cref="DockingPersistence"/>、実際の Win32 は <see cref="ExternalWindowEmbedder"/> に委譲する。
/// </summary>
public static class DockingController
{
    /// <summary>いずれかのパネルでアタッチ状態が変わったとき。各 Dock の View が UI を同期する。</summary>
    public static event Action<int>? PanelChanged;

    /// <summary>外部 HWND を指定パネルに埋め込む。View 未生成時でもストアに状態を残し、表示後に <see cref="TryReattachIfNeeded"/> で接続できる。</summary>
    public static bool TryAttach(int panelIndex, IntPtr hwnd, out string? error)
    {
        error = null;
        if (panelIndex < 1 || panelIndex > 10)
        {
            error = "パネル番号が不正です。";
            return false;
        }

        hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
        {
            error = "ウィンドウが無効です。";
            return false;
        }

        var processName = (string?)null;
        var exePath = (string?)null;
        ExternalWindowEmbedder.TryGetWindowProcessInfo(hwnd, out processName, out exePath);
        var className = ExternalWindowEmbedder.GetWindowClass(hwnd);

        if (ExternalWindowEmbedder.IsExplorerWindow(processName, className))
        {
            error = "Windows標準エクスプローラーは相性問題があるためアタッチできません。";
            return false;
        }

        // 同じスロットに別ウィンドウを載せる前に、既存の子を完全に元に戻し復元用メタもクリアする
        TryFullDetach(panelIndex, discardSavedRestoreTarget: true);

        // デタッチ時に元の見た目へ戻すため、埋め込み前のスタイルを必ず保存する
        var originalStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
        var originalExStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

        DockingStateStore.Update(panelIndex, s =>
        {
            s.AttachedHwnd = hwnd;
            s.OriginalStyle = originalStyle;
            s.OriginalExStyle = originalExStyle;
            s.ProcessName = processName;
            s.ExePath = exePath;
            s.WindowTitleHint = ExternalWindowEmbedder.GetWindowTitle(hwnd);
            s.WindowClassHint = className;
            s.DisplayName = GuessAppDisplayName(processName, exePath);
            s.LastError = null;
        });

        if (DockTargetRegistry.TryGet(panelIndex, out var embed, out _, out _))
        {
            try
            {
                embed!(hwnd);
                // YMM4 終了時は「独立ウィンドウに戻す」だけで、次回自動復元用の ExePath 等は残す
                AutoDetachManager.RegisterDetachAction(() => TryFullDetach(panelIndex, discardSavedRestoreTarget: false));
                DockingPersistence.SaveFromState();
                Raise(panelIndex);
                return true;
            }
            catch (Exception ex)
            {
                DockingStateStore.Update(panelIndex, s => s.LastError = $"アタッチ失敗: {ex.Message}");
                error = ex.Message;
                Raise(panelIndex);
                return false;
            }
        }

        // AvalonDock で View がまだ無いタイミングでもストアと JSON は更新済み。Loaded で TryReattachIfNeeded される。
        DockingPersistence.SaveFromState();
        Raise(panelIndex);
        return true;
    }

    /// <summary>ストアに HWND が残っていて実ウィンドウが生きていれば、登録済みホストへ再 SetParent する。</summary>
    public static void TryReattachIfNeeded(int panelIndex)
    {
        var state = DockingStateStore.Get(panelIndex);
        if (state.AttachedHwnd == IntPtr.Zero) return;

        if (!NativeMethods.IsWindow(state.AttachedHwnd))
        {
            DockingStateStore.Update(panelIndex, s =>
            {
                s.AttachedHwnd = IntPtr.Zero;
                s.LastError = "ウィンドウが終了しました。";
            });
            Raise(panelIndex);
            return;
        }

        if (!DockTargetRegistry.TryGet(panelIndex, out var embed, out _, out _)) return;

        try
        {
            embed!(state.AttachedHwnd);
        }
        catch
        {
            // 表示復帰時に自然回復するケースもあるため握りつぶす
        }
        Raise(panelIndex);
    }

    /// <summary>タブ切り替えでパネルが非表示になるときなど。子を親から外し画面外へ（完全復元はしない）。</summary>
    public static void TryTempDetach(int panelIndex)
    {
        var state = DockingStateStore.Get(panelIndex);
        if (state.AttachedHwnd == IntPtr.Zero) return;
        if (!NativeMethods.IsWindow(state.AttachedHwnd)) return;

        if (DockTargetRegistry.TryGet(panelIndex, out _, out var tempDetach, out _))
        {
            try { tempDetach?.Invoke(); } catch { }
        }
        else
        {
            ExternalWindowEmbedder.TempDetachToHidden(state.AttachedHwnd);
        }
    }

    /// <param name="discardSavedRestoreTarget">
    /// true: ユーザー解除など。JSON の復元ヒント（ExePath 等）も消す。
    /// false: プロセス終了時の後片付け。HWND は独立に戻すが、次回の自動復元用データは残す。
    /// </param>
    public static void TryFullDetach(int panelIndex, bool discardSavedRestoreTarget = true)
    {
        var state = DockingStateStore.Get(panelIndex);
        var hwnd = state.AttachedHwnd;

        if (hwnd == IntPtr.Zero && !discardSavedRestoreTarget)
            return;

        if (hwnd != IntPtr.Zero)
        {
            var originalStyle = state.OriginalStyle;
            var originalExStyle = state.OriginalExStyle;
            try
            {
                ExternalWindowEmbedder.FullDetachRestore(hwnd, originalStyle, originalExStyle);
            }
            catch { }
        }

        DockingStateStore.Update(panelIndex, s =>
        {
            if (hwnd != IntPtr.Zero)
            {
                s.AttachedHwnd = IntPtr.Zero;
                s.OriginalStyle = 0;
                s.OriginalExStyle = 0;
                s.DisplayName = null;
                s.LastError = null;
            }

            if (discardSavedRestoreTarget)
            {
                s.ExePath = null;
                s.ProcessName = null;
                s.WindowTitleHint = null;
                s.WindowClassHint = null;
            }
        });

        DockingPersistence.SaveFromState();
        Raise(panelIndex);
    }

    private static void Raise(int panelIndex)
    {
        PanelChanged?.Invoke(panelIndex);
    }

    /// <summary>タブ等に出す名前。ウィンドウタイトルは長くなりがちなので使わず、プロセス名または exe 名のみ。</summary>
    private static string? GuessAppDisplayName(string? processName, string? exePath)
    {
        if (!string.IsNullOrWhiteSpace(processName)) return processName;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            try { return Path.GetFileNameWithoutExtension(exePath); } catch { }
        }
        return null;
    }
}

