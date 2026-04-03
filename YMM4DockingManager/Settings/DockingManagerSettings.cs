using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YMM4DockingManager.Settings;

/// <summary>
/// ユーザーの「ドキュメント\YMM4 Docking Manager\settings.json」に保存される設定のルート。
/// ランタイムの HWND は含めず、自動復元に使う文字列ヒントのみを保持する。
/// </summary>
public sealed class DockingManagerSettings
{
    private static readonly string _settingsPath;
    private static DockingManagerSettings? _instance;
    private static readonly object _lock = new();

    /// <summary>常に 10 要素を想定。不足時は Load 時に埋められる。</summary>
    public List<DockingPanelSettings> Panels { get; set; } = CreateDefaults();

    static DockingManagerSettings()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "YMM4 Docking Manager");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public static DockingManagerSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                    _instance ??= Load();
            }
            return _instance;
        }
    }

    private static DockingManagerSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<DockingManagerSettings>(json);
                if (loaded != null)
                {
                    loaded.Panels ??= CreateDefaults();
                    while (loaded.Panels.Count < 10)
                        loaded.Panels.Add(new DockingPanelSettings { PanelIndex = loaded.Panels.Count + 1 });
                    return loaded;
                }
            }
        }
        catch { }

        return new DockingManagerSettings();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private static List<DockingPanelSettings> CreateDefaults()
    {
        var list = new List<DockingPanelSettings>(10);
        for (int i = 1; i <= 10; i++)
            list.Add(new DockingPanelSettings { PanelIndex = i });
        return list;
    }
}

/// <summary>1 スロット分のシリアライズ可能フィールド。<see cref="YMM4DockingManager.Docking.DockingStateStore"/> の対応項目と同期される。</summary>
public sealed class DockingPanelSettings
{
    public int PanelIndex { get; set; }
    public string? ExePath { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitleHint { get; set; }
    public string? WindowClassHint { get; set; }
}

