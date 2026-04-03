using System;

namespace YMM4DockingManager.Docking;

/// <summary>
/// 1 つのドックスロット（Dock 01〜10）に対応するメモリ上の状態。
/// JSON に永続化するのは主に ExePath / ProcessName / 各 Hint。AttachedHwnd とスタイルはセッション内のみ。
/// </summary>
public sealed class DockPanelState
{
    public int PanelIndex { get; }

    /// <summary>現在このパネルに SetParent されている外部ウィンドウ。未アタッチは Zero。</summary>
    public IntPtr AttachedHwnd { get; set; }
    /// <summary>埋め込み前の GWL_STYLE。フルデタッチで復元する。</summary>
    public int OriginalStyle { get; set; }
    /// <summary>埋め込み前の GWL_EXSTYLE。</summary>
    public int OriginalExStyle { get; set; }

    /// <summary>自動復元でプロセスを特定するための exe フルパス（可能なら）。</summary>
    public string? ExePath { get; set; }
    /// <summary>自動復元用のプロセス名。</summary>
    public string? ProcessName { get; set; }
    /// <summary>補助情報（現状の復元ロジックでは主に ExePath / ProcessName を使用）。</summary>
    public string? WindowTitleHint { get; set; }
    /// <summary>補助情報。拒否判定などに利用。</summary>
    public string? WindowClassHint { get; set; }

    /// <summary>タブ名・ツール名に出す短いラベル（プロセス名や exe ベース名）。</summary>
    public string? DisplayName { get; set; }
    /// <summary>ユーザー向けエラーメッセージ。成功時はクリア。</summary>
    public string? LastError { get; set; }

    public DockPanelState(int panelIndex)
    {
        PanelIndex = panelIndex;
    }
}

