namespace YMM4DockingManager.ViewModels;

/// <summary><see cref="YMM4DockingManager.Views.DockTargetView"/> が DataContext からパネル番号だけ型安全に取り出すためのマーカー。</summary>
internal interface IDockTargetViewModel
{
    int PanelIndex { get; }
}

