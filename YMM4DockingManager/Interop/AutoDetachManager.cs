using System;
using System.Collections.Generic;

namespace YMM4DockingManager.Interop;

/// <summary>
/// アタッチ成功時に登録したデリゲートを、YMM4 終了やセッション終了時にまとめて実行する。
/// 各デリゲートは通常 <see cref="YMM4DockingManager.Docking.DockingController.TryFullDetach"/>（復元ヒントは残す）を呼ぶ。
/// </summary>
public static class AutoDetachManager
{
    private static readonly List<Action> _detachActions = new();
    private static bool _hooked;

    /// <summary>同じ Action 参照の二重登録は避ける。初回のみ Application.Exit 等にフックする。</summary>
    public static void RegisterDetachAction(Action detachAction)
    {
        if (detachAction == null) throw new ArgumentNullException(nameof(detachAction));

        lock (_detachActions)
        {
            if (!_detachActions.Contains(detachAction))
                _detachActions.Add(detachAction);

            if (_hooked) return;

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Exit += (_, _) => FullDetachAll();
                System.Windows.Application.Current.SessionEnding += (_, _) => FullDetachAll();

                if (System.Windows.Application.Current.MainWindow != null)
                    System.Windows.Application.Current.MainWindow.Closing += (_, _) => FullDetachAll();
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => FullDetachAll();
            _hooked = true;
        }
    }

    public static void UnregisterDetachAction(Action detachAction)
    {
        if (detachAction == null) return;
        lock (_detachActions)
        {
            _detachActions.Remove(detachAction);
        }
    }

    /// <summary>ロック外で各デリゲートを実行し、再入を避ける。完了後リストはクリア。</summary>
    public static void FullDetachAll()
    {
        Action[] actionsToRun;
        lock (_detachActions)
        {
            actionsToRun = _detachActions.ToArray();
        }

        foreach (var action in actionsToRun)
        {
            try { action(); }
            catch { }
        }

        lock (_detachActions)
        {
            _detachActions.Clear();
        }
    }
}

