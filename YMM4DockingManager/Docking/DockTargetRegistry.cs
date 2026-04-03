using System;
using System.Collections.Generic;
using System.Linq;

namespace YMM4DockingManager.Docking;

/// <summary>
/// 各 <see cref="Views.DockTargetView"/> が Loaded 時に自分の PanelIndex 用コールバックを登録し、
/// <see cref="Interop.WinEventAttachManager"/> が <see cref="FindPanelIndexUnderCursor"/> で「どのパネル上でドロップしたか」を解決するためのレジストリ。
/// </summary>
internal static class DockTargetRegistry
{
    /// <param name="IsVisible">タブで隠れているなどのとき false にしてヒットテストから外す。</param>
    /// <param name="IsCursorInside">スクリーン座標がホスト Panel のクライアント矩形内か。</param>
    /// <param name="EmbedIntoHost">SetParent とリサイズを実行（通常は View 内で Embedder を呼ぶ）。</param>
    /// <param name="TempDetachFromHost">非表示時の一時デタッチ。</param>
    private sealed record Entry(
        int PanelIndex,
        Func<bool> IsVisible,
        Func<bool> IsCursorInside,
        Action<IntPtr> EmbedIntoHost,
        Action TempDetachFromHost);

    private static readonly object _lock = new();
    private static readonly Dictionary<int, Entry> _entries = new();

    /// <summary>同じ PanelIndex で再登録すると上書きされる（View の再生成に対応）。</summary>
    public static void Register(
        int panelIndex,
        Func<bool> isVisible,
        Func<bool> isCursorInside,
        Action<IntPtr> embedIntoHost,
        Action tempDetachFromHost)
    {
        if (panelIndex < 1 || panelIndex > 10) throw new ArgumentOutOfRangeException(nameof(panelIndex));
        if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));
        if (isCursorInside == null) throw new ArgumentNullException(nameof(isCursorInside));
        if (embedIntoHost == null) throw new ArgumentNullException(nameof(embedIntoHost));
        if (tempDetachFromHost == null) throw new ArgumentNullException(nameof(tempDetachFromHost));

        lock (_lock)
        {
            _entries[panelIndex] = new Entry(panelIndex, isVisible, isCursorInside, embedIntoHost, tempDetachFromHost);
        }
    }

    public static void Unregister(int panelIndex)
    {
        lock (_lock)
        {
            _entries.Remove(panelIndex);
        }
    }

    public static bool TryGet(int panelIndex, out Action<IntPtr>? embedIntoHost, out Action? tempDetachFromHost, out Func<bool>? isVisible)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(panelIndex, out var e))
            {
                embedIntoHost = e.EmbedIntoHost;
                tempDetachFromHost = e.TempDetachFromHost;
                isVisible = e.IsVisible;
                return true;
            }
        }

        embedIntoHost = null;
        tempDetachFromHost = null;
        isVisible = null;
        return false;
    }

    /// <summary>表示中かつカーソルがホスト内にあるエントリのうち、番号が最小のパネルを返す。</summary>
    public static int? FindPanelIndexUnderCursor()
    {
        Entry[] snapshot;
        lock (_lock)
        {
            snapshot = _entries.Values.ToArray();
        }

        // 複数一致は稀だが、常に同じ優先順位にすると挙動が予測しやすい
        foreach (var e in snapshot.OrderBy(x => x.PanelIndex))
        {
            if (!e.IsVisible()) continue;
            if (e.IsCursorInside()) return e.PanelIndex;
        }

        return null;
    }
}

