using System;
using YMM4DockingManager.Docking;
using YMM4DockingManager.ViewModels;
using YMM4DockingManager.Views;
using YukkuriMovieMaker.Plugin;

namespace YMM4DockingManager.Plugins;

// IToolPlugin は 1 クラス = 1 ツールのため、Dock 01〜10 を 10 個の型で登録する。表示名は保存済み DisplayName があれば動的に変わる。

public sealed class GenericDockTargetPlugin01 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(1);
    public Type ViewModelType => typeof(DockTarget01ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin02 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(2);
    public Type ViewModelType => typeof(DockTarget02ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin03 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(3);
    public Type ViewModelType => typeof(DockTarget03ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin04 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(4);
    public Type ViewModelType => typeof(DockTarget04ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin05 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(5);
    public Type ViewModelType => typeof(DockTarget05ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin06 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(6);
    public Type ViewModelType => typeof(DockTarget06ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin07 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(7);
    public Type ViewModelType => typeof(DockTarget07ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin08 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(8);
    public Type ViewModelType => typeof(DockTarget08ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin09 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(9);
    public Type ViewModelType => typeof(DockTarget09ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

public sealed class GenericDockTargetPlugin10 : IToolPlugin
{
    public string Name => GenericDockTargetPluginTitles.PluginTitle(10);
    public Type ViewModelType => typeof(DockTarget10ViewModel);
    public Type ViewType => typeof(DockTargetView);
}

/// <summary>各 GenericDockTargetPlugin のツール名（IToolPlugin.Name）。ストアの表示名があればそれを返す。</summary>
file static class GenericDockTargetPluginTitles
{
    public static string PluginTitle(int panelIndex)
    {
        var s = DockingStateStore.Get(panelIndex);
        if (!string.IsNullOrWhiteSpace(s.DisplayName))
            return s.DisplayName!;
        return $"Dock {panelIndex:00}";
    }
}

