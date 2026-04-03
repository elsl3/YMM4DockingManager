using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YMM4DockingManager.Commands;
using YMM4DockingManager.Docking;

namespace YMM4DockingManager.ViewModels;

/// <summary>管理パネル用。起動時に JSON をストアへ読み込み、一覧行と「全解除」「自動復元」を提供する。</summary>
public sealed class DockingManagerViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PanelRowViewModel> Panels { get; } = new();

    public ICommand DetachAllCommand { get; }
    public ICommand RestoreCommand { get; }

    public DockingManagerViewModel()
    {
        DockingPersistence.LoadIntoState();

        for (int i = 1; i <= 10; i++)
            Panels.Add(new PanelRowViewModel(i));

        DetachAllCommand = new RelayCommand(() =>
        {
            for (int i = 1; i <= 10; i++)
                DockingController.TryFullDetach(i);
            DockingPersistence.SaveFromState();
        });

        RestoreCommand = new RelayCommand(() =>
        {
            DockingPersistence.TryAutoRestoreAll();
        });

        DockingStateStore.PanelStateChanged += _ =>
        {
            foreach (var p in Panels)
                p.Refresh();
        };

        // 管理パネル表示時点で、保存済みヒントに基づき可能な限り再アタッチを試す
        DockingPersistence.TryAutoRestoreAll();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>一覧の 1 行。ストアの状態を <see cref="Refresh"/> で UI に反映する。</summary>
public sealed class PanelRowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int PanelIndex { get; }

    public string Name => $"Dock {PanelIndex:00}";

    public string AttachedName
    {
        get
        {
            var s = DockingStateStore.Get(PanelIndex);
            return string.IsNullOrWhiteSpace(s.DisplayName) ? "(なし)" : s.DisplayName!;
        }
    }

    public string Status
    {
        get
        {
            var s = DockingStateStore.Get(PanelIndex);
            if (!string.IsNullOrWhiteSpace(s.LastError)) return s.LastError!;
            return s.AttachedHwnd != IntPtr.Zero ? "アタッチ中" : "未アタッチ";
        }
    }

    public bool Enabled => true;

    public ICommand DetachCommand { get; }

    public PanelRowViewModel(int panelIndex)
    {
        PanelIndex = panelIndex;
        DetachCommand = new RelayCommand(() =>
        {
            DockingController.TryFullDetach(PanelIndex);
            DockingPersistence.SaveFromState();
            Refresh();
        }, () => DockingStateStore.Get(PanelIndex).AttachedHwnd != IntPtr.Zero);
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(AttachedName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Enabled));
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

