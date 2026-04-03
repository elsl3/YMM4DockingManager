using System;
using System.Collections.Generic;

namespace YMM4DockingManager.Docking;

/// <summary>
/// 10 パネル分のランタイム状態をプロセス内で唯一のソースとして保持する。
/// YMM4 の AvalonDock はタブ操作で UserControl を再生成しうるため、HWND や表示名は View に持たせずここに置く。
/// </summary>
public static class DockingStateStore
{
    // View インスタンスと寿命が一致しないので、ロック付き static 配列が正本
    private static readonly object _lock = new();
    private static readonly DockPanelState[] _panels = CreatePanels();

    public static event Action<int>? PanelStateChanged;

    /// <summary>
    /// いずれかのパネル状態が変わったときに通知する。各 Dock ビューが自パネルの表示だけ更新するために使う。
    /// </summary>
    public static event Action? GlobalDockingStateInvalidated;

    public static IReadOnlyList<DockPanelState> Panels => _panels;

    public static DockPanelState Get(int panelIndex)
    {
        if (panelIndex < 1 || panelIndex > 10) throw new ArgumentOutOfRangeException(nameof(panelIndex));
        return _panels[panelIndex - 1];
    }

    public static void Update(int panelIndex, Action<DockPanelState> update)
    {
        if (update == null) throw new ArgumentNullException(nameof(update));

        lock (_lock)
        {
            update(Get(panelIndex));
        }
        PanelStateChanged?.Invoke(panelIndex);
        GlobalDockingStateInvalidated?.Invoke();
    }

    private static DockPanelState[] CreatePanels()
    {
        var list = new DockPanelState[10];
        for (int i = 0; i < 10; i++)
            list[i] = new DockPanelState(i + 1);
        return list;
    }
}

