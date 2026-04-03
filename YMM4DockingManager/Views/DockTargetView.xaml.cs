using System;
using System.Windows;
using System.Windows.Input;
using WinPanel = System.Windows.Forms.Panel;
using YMM4DockingManager.Docking;
using YMM4DockingManager.Interop;
using YMM4DockingManager.ViewModels;

namespace YMM4DockingManager.Views;

/// <summary>
/// 1 つのドックスロットの UI。WPF 上に <see cref="System.Windows.Forms.Integration.WindowsFormsHost"/> で WinForms の <see cref="System.Windows.Forms.Panel"/> を置き、
/// 外部 HWND の親をその Panel にする。Loaded でレジストリ登録、非表示時は一時デタッチ、キーボードは子へフォーカスを渡す。
/// </summary>
public partial class DockTargetView : System.Windows.Controls.UserControl
{
    private WinPanel _hostPanel;
    private IDockTargetViewModel? _vm;
    private bool _subscribed;

    public DockTargetView()
    {
        InitializeComponent();

        // Airspace: 実際の埋め込み先は WinForms。WPF 要素はホストの周り（ヒント帯など）のみ。
        _hostPanel = new WinPanel { Dock = System.Windows.Forms.DockStyle.Fill };
        WfHost.Child = _hostPanel;

        DataContextChanged += OnDataContextChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        _hostPanel.SizeChanged += (_, _) => ResizeEmbeddedWindow();

        // 埋め込み子は別プロセスのため、フォーカスが WPF に残っているとキーが届かない。先に子 HWND へ渡す。
        PreviewMouseDown += (_, _) => FocusEmbeddedIfAny();
        GotKeyboardFocus += (_, _) => FocusEmbeddedIfAny();
        PreviewTextInput += (_, e) => { if (ShouldCaptureKeys()) { FocusEmbeddedIfAny(); e.Handled = true; } };
        PreviewKeyDown += (_, e) =>
        {
            if (!ShouldCaptureKeys()) return;
            FocusEmbeddedIfAny();
            e.Handled = true;
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _vm = DataContext as IDockTargetViewModel;
        EnsureStoreSubscriptions();
        UpdateEmptyHintVisibility();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WinEventAttachManager.EnsureStarted();

        _vm ??= DataContext as IDockTargetViewModel;
        if (_vm == null) return;

        EnsureStoreSubscriptions();

        DockTargetRegistry.Register(
            _vm.PanelIndex,
            isVisible: () => IsVisible,
            isCursorInside: IsCursorInsideHostPanel,
            embedIntoHost: EmbedToHost,
            tempDetachFromHost: TempDetach);

        DockingController.TryReattachIfNeeded(_vm.PanelIndex);
        // 終了時はユーザー意図の「解除」とは別。復元用 JSON は残したまま HWND だけ独立に戻す
        AutoDetachManager.RegisterDetachAction(() =>
            DockingController.TryFullDetach(_vm.PanelIndex, discardSavedRestoreTarget: false));

        UpdateParentTabTitle();
        UpdateEmptyHintVisibility();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed)
        {
            DockingStateStore.PanelStateChanged -= OnPanelStateChanged;
            DockingStateStore.GlobalDockingStateInvalidated -= OnGlobalDockingStateInvalidated;
            _subscribed = false;
        }

        if (_vm != null)
        {
            TempDetach(); // ツールウィンドウ破棄時に子がホストに張り付いたまま残らないようにする
            DockTargetRegistry.Unregister(_vm.PanelIndex);
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm == null) return;
        if ((bool)e.NewValue)
        {
            DockingController.TryReattachIfNeeded(_vm.PanelIndex);
            FocusEmbeddedIfAny();
            UpdateEmptyHintVisibility();
        }
        else
        {
            // タブを切り替えて非表示になった間は子を画面外へ（再表示で TryReattachIfNeeded）
            DockingController.TryTempDetach(_vm.PanelIndex);
        }
    }

    private void EnsureStoreSubscriptions()
    {
        if (_vm == null || _subscribed) return;
        DockingStateStore.PanelStateChanged += OnPanelStateChanged;
        DockingStateStore.GlobalDockingStateInvalidated += OnGlobalDockingStateInvalidated;
        _subscribed = true;
    }

    private void OnGlobalDockingStateInvalidated()
    {
        if (Dispatcher.CheckAccess())
            UpdateEmptyHintVisibility();
        else
            Dispatcher.BeginInvoke(new Action(UpdateEmptyHintVisibility));
    }

    private void UpdateEmptyHintVisibility()
    {
        if (_vm == null)
        {
            EmptyHintBanner.Visibility = Visibility.Collapsed;
            return;
        }

        var attached = DockingStateStore.Get(_vm.PanelIndex).AttachedHwnd != IntPtr.Zero;
        EmptyHintBanner.Visibility = attached ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPanelStateChanged(int panelIndex)
    {
        if (_vm == null) return;
        if (panelIndex != _vm.PanelIndex) return;
        UpdateParentTabTitle();
        UpdateEmptyHintVisibility();
    }

    private bool IsCursorInsideHostPanel()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return false;
        var cursor = new System.Drawing.Point(pt.x, pt.y);

        var rect = _hostPanel.RectangleToScreen(_hostPanel.ClientRectangle);
        return rect.Contains(cursor);
    }

    private void EmbedToHost(IntPtr hwnd)
    {
        var s = DockingStateStore.Get(_vm!.PanelIndex);

        ExternalWindowEmbedder.EmbedIntoWinFormsPanel(hwnd, _hostPanel.Handle);
        ExternalWindowEmbedder.ResizeEmbeddedWindow(hwnd, _hostPanel.Width, _hostPanel.Height);

        // 最新の状態へ（表示名/エラーなど）
        DockingStateStore.Update(_vm.PanelIndex, st =>
        {
            st.AttachedHwnd = hwnd;
            st.OriginalStyle = st.OriginalStyle == 0 ? s.OriginalStyle : st.OriginalStyle;
            st.OriginalExStyle = st.OriginalExStyle == 0 ? s.OriginalExStyle : st.OriginalExStyle;
            st.LastError = null;
        });

        UpdateParentTabTitle();
        FocusEmbeddedIfAny();
    }

    private void TempDetach()
    {
        if (_vm == null) return;
        var state = DockingStateStore.Get(_vm.PanelIndex);
        ExternalWindowEmbedder.TempDetachToHidden(state.AttachedHwnd);
    }

    private void ResizeEmbeddedWindow()
    {
        if (_vm == null) return;
        var state = DockingStateStore.Get(_vm.PanelIndex);
        ExternalWindowEmbedder.ResizeEmbeddedWindow(state.AttachedHwnd, _hostPanel.Width, _hostPanel.Height);
    }

    private bool ShouldCaptureKeys()
    {
        if (_vm == null) return false;
        var state = DockingStateStore.Get(_vm.PanelIndex);
        if (state.AttachedHwnd == IntPtr.Zero) return false;
        if (!NativeMethods.IsWindow(state.AttachedHwnd)) return false;
        return IsMouseOver || IsKeyboardFocusWithin;
    }

    private void FocusEmbeddedIfAny()
    {
        if (_vm == null) return;
        var state = DockingStateStore.Get(_vm.PanelIndex);
        var hwnd = state.AttachedHwnd;
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return;

        try
        {
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.SetFocus(hwnd);
        }
        catch { }
    }

    private void UpdateParentTabTitle()
    {
        if (_vm == null) return;

        var state = DockingStateStore.Get(_vm.PanelIndex);
        var title = string.IsNullOrWhiteSpace(state.DisplayName) ? $"Dock {_vm.PanelIndex:00}" : state.DisplayName!;

        // YMM4内部はAvalonDockを使うため、親要素にTitle相当のプロパティがある場合は更新する
        // 参照アセンブリを増やさず、リフレクションで安全に試みる
        try
        {
            DependencyObject? current = this;
            for (int i = 0; i < 30 && current != null; i++)
            {
                var t = current.GetType();
                var p = t.GetProperty("Title");
                if (p != null && p.PropertyType == typeof(string) && p.CanWrite)
                {
                    p.SetValue(current, title);
                    return;
                }
                current = LogicalTreeHelper.GetParent(current);
            }
        }
        catch { }
    }
}

