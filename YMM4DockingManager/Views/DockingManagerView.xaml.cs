using YMM4DockingManager.ViewModels;

namespace YMM4DockingManager.Views;

/// <summary>Dock 01〜10 の一覧と一括操作。DataContext 未設定時だけ <see cref="YMM4DockingManager.ViewModels.DockingManagerViewModel"/> を生成する。</summary>
public partial class DockingManagerView : System.Windows.Controls.UserControl
{
    public DockingManagerView()
    {
        InitializeComponent();
        if (DataContext == null)
            DataContext = new DockingManagerViewModel();
    }
}

