using System;
using YMM4DockingManager.ViewModels;
using YMM4DockingManager.Views;
using YukkuriMovieMaker.Plugin;

namespace YMM4DockingManager.Plugins;

/// <summary>YMM4 ツールメニューに「Docking Manager」を出すエントリ。View / ViewModel を YMM4 に登録するだけ。</summary>
public sealed class DockingManagerToolPlugin : IToolPlugin
{
    public string Name => "Docking Manager";
    public Type ViewModelType => typeof(DockingManagerViewModel);
    public Type ViewType => typeof(DockingManagerView);
}

