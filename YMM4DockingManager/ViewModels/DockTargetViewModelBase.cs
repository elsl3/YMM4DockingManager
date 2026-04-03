using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YMM4DockingManager.Commands;
using YMM4DockingManager.Docking;

namespace YMM4DockingManager.ViewModels;

/// <summary>
/// Dock 01〜10 共通の ViewModel。表示文字列はすべて <see cref="YMM4DockingManager.Docking.DockingStateStore"/> を読む（View の寿命と切り離す）。
/// </summary>
internal abstract class DockTargetViewModelBase : INotifyPropertyChanged, IDockTargetViewModel
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int PanelIndex { get; }

    public string PanelTitle
    {
        get
        {
            var s = DockingStateStore.Get(PanelIndex);
            if (!string.IsNullOrWhiteSpace(s.DisplayName))
                return s.DisplayName!;
            return $"Dock {PanelIndex:00}";
        }
    }

    public string Status
    {
        get
        {
            var s = DockingStateStore.Get(PanelIndex);
            if (!string.IsNullOrWhiteSpace(s.LastError)) return s.LastError!;
            if (s.AttachedHwnd != IntPtr.Zero) return "アタッチ中";
            return "未アタッチ";
        }
    }

    /// <summary>ヘッダー2行目。アプリ名は <see cref="PanelTitle"/> に集約し、ここはスロット表示のみ。</summary>
    public string AttachedName
    {
        get
        {
            var s = DockingStateStore.Get(PanelIndex);
            if (s.AttachedHwnd == IntPtr.Zero) return "(なし)";
            return $"Dock {PanelIndex:00}";
        }
    }

    public bool IsAttached => DockingStateStore.Get(PanelIndex).AttachedHwnd != IntPtr.Zero;

    public ICommand DetachCommand { get; }

    protected DockTargetViewModelBase(int panelIndex)
    {
        PanelIndex = panelIndex;
        DetachCommand = new RelayCommand(() => DockingController.TryFullDetach(PanelIndex), () => IsAttached);

        DockingStateStore.PanelStateChanged += OnPanelStateChanged;
    }

    private void OnPanelStateChanged(int panelIndex)
    {
        if (panelIndex != PanelIndex) return;

        OnPropertyChanged(nameof(PanelTitle));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(AttachedName));
        OnPropertyChanged(nameof(IsAttached));
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

