# YMM4 Docking Manager — 開発者向けドキュメント

本ドキュメントは、ソースを取得して改修・ビルド・配布する**開発者**向けです。エンドユーザー向けのインストールと使い方は [README.md](../README.md) を参照してください（`.ymme` の導入のみでよい旨を README に記載しています）。

## 参照先

- **YMM4 公式**: プラグインの作成方法、参照 DLL、`.ymme` 形式の扱いなど
- **アタッチ処理の入門**: [ATTACH_GUIDE.md](ATTACH_GUIDE.md)（Ctrl＋タイトルバー移動から `SetParent` までの流れ）

## 技術スタック

- **言語**: C#
- **UI**: WPF（`UseWPF`）＋ 埋め込みホストに **Windows Forms** の `Panel`（`UseWindowsForms`、`WindowsFormsHost`）
- **フレームワーク**: `net10.0-windows10.0.19041.0`（プロジェクトファイルの `TargetFramework` に準拠）
- **YMM4 連携**: `YukkuriMovieMaker.Plugin.dll` の `IToolPlugin` 等

## リポジトリ構成（概要）

```
YMM4DockingManager/
  README.md
  LICENSE
  docs/
    ATTACH_GUIDE.md
    DEVELOPER.md
  YMM4DockingManager.sln
  YMM4DockingManager/
    YMM4DockingManager.csproj
    Plugins/          # IToolPlugin 実装（Docking Manager + Dock 01〜10）
    Views/            # WPF UserControl（XAML）
    ViewModels/
    Docking/          # 状態ストア、埋め込み、永続化、コントローラ
    Interop/          # P/Invoke、WinEventHook、終了時デタッチ登録
    Settings/         # JSON 設定
    Commands/
  publish/            # Release ビルドで生成される .ymme（リポジトリにコミットしない想定でも可）
```

## ビルド手順

1. **Visual Studio**（.NET デスクトップ開発ワークロード推奨）または **.NET SDK** をインストールする。
2. YMM4 インストールフォルダから次を取得し、プロジェクト参照として解決できるようにする。
   - `YukkuriMovieMaker.Plugin.dll`
   - `YukkuriMovieMaker.Controls.dll`
3. [YMM4DockingManager.csproj](../YMM4DockingManager/YMM4DockingManager.csproj) の `<HintPath>` が、**あなたの環境の YMM4 パス** を指すように編集する（既定は `C:\YMM4\YMM4 Lite\` 想定の例）。
4. ソリューションをビルドする。

```text
dotnet build YMM4DockingManager.sln -c Release
```

成果物は通常の `bin\Debug\...` / `bin\Release\...` に出力されます（SDK標準）。

加えて、ビルド後に次のフォルダへ **配布用にコピー** されます。

- `YMM4DockingManager\Output\Debug\`
- `YMM4DockingManager\Output\Release\`

## Output フォルダへのコピー

`YMM4DockingManager.csproj` の `CopyArtifactsToOutputFolder` ターゲットが、`$(TargetPath)`（dll）と `pdb` を `Output\<Configuration>\` にコピーします。

## 一般配布用 `.ymme`（publish）

`.ymme` は、プラグイン DLL を **zip で固めたうえで拡張子を `.ymme` にしたもの**です（手順の詳細は YMM4 公式のプラグイン配布ガイドに従ってください）。

Release 構成でビルドすると、`YMM4DockingManager.csproj` の `PackageYmme` ターゲットが、ビルド済みの `$(TargetPath)`（DLL）を zip 化し、`publish\YMM4DockingManager.ymme` として出力します（上書き）。

```text
dotnet build YMM4DockingManager.sln -c Release
```

出力先: `publish\YMM4DockingManager.ymme`

## アーキテクチャ概要

### プラグインエントリ

YMM4 はツールパネルを `IToolPlugin` として列挙する仕様のため、メニューに出すパネル数を実行時に自由に増やす API に頼らず、**Dock 01〜10 を個別の `IToolPlugin` 実装として静的に定義**しています（`GenericDockTargetPlugin01` 〜 `10`）。管理用の `DockingManagerToolPlugin` が一覧・操作の入口です。

| クラス | 役割 |
|--------|------|
| `DockingManagerToolPlugin` | 管理パネル（一覧・全解除・自動復元） |
| `GenericDockTargetPlugin01` 〜 `10` | 各ドック用サブパネル（View/ViewModel はインデックス別） |

`Name` プロパティは、アタッチ状態に応じて表示名（アプリ名）を返す実装になっており、タブ表示の更新に利用されます。

### 外部ウィンドウの埋め込み

- **SetParent** で対象 HWND を WinForms `Panel` の子にする。
- **GetWindowLong / SetWindowLong** でキャプション等を落としてパネル内に馴染ませる。
- **GWLP_HWNDPARENT でウィンドウ所有権を書き換えない**（モダンアプリ等でフォーカス時に描画が止まる事例があるため）。

実装の中心は `Docking/ExternalWindowEmbedder.cs` と `Docking/DockingController.cs` です。

### アタッチ操作の検知（Ctrl + タイトルバー移動終了）

WPF の D&D では別プロセスのウィンドウを検知できないため、**SetWinEventHook** で `EVENT_SYSTEM_MOVESIZEEND` を購読し、**カーソルがどの Dock パネル上か** と **Ctrl 押下** を満たしたときだけアタッチします。

- `Interop/WinEventAttachManager.cs`
- `Docking/DockTargetRegistry.cs`（パネルごとのヒットテスト登録）

### AvalonDock による View の再生成

タブ切替等で View が破棄されても HWND と元スタイルを失わないよう、**パネルインデックス単位の状態は static** に保持します。

- `Docking/DockingStateStore.cs`
- `Docking/DockPanelState.cs`

### 終了時の一括デタッチ

`AutoDetachManager` が `Application` の終了系イベントと `ProcessExit` にフックし、登録されたデタッチ処理を実行します。**ループ中にコレクションが変わる**と解放が止まるため、実行対象は **スナップショット（`ToArray()`）** で回します。

### 設定の永続化

- `Settings/DockingManagerSettings.cs` が JSON を `Documents\YMM4 Docking Manager\settings.json` に保存します。
- `Docking/DockingPersistence.cs` が状態ストアと設定の読み書き・自動復元のトリガーを担います。

ユーザーが明示的に解除した場合は復元用のプロセス／パス情報を消し、YMM4 終了時の自動デタッチでは復元用情報を残す、といった切り分けは `DockingController.TryFullDetach` の引数で行っています。

### フォーカス

埋め込み先アプリにキー入力を届けやすくするため、`DockTargetView` で **SetForegroundWindow / SetFocus** や、パネル内でのキーイベントの扱いを行っています。アプリや YMM4 のバージョンによっては、IME やグローバルホットキーと干渉する場合があります。

### 案内 UI と WindowsFormsHost

`WindowsFormsHost` はエアスペースの都合で、その上に重ねた WPF 要素が隠れやすいです。未アタッチ時の案内テキストは **ホストと別行** に置き、常に見えるようにしています。

## 改修時のチェックリスト

- エクスプローラー等、**アタッチ拒否**の対象を誤って許可していないか
- **static ストア**でパネルごとの HWND／スタイルが維持されているか（View 再生成で失わないか）
- 終了時デタッチで **コレクションを走査中に変更**しないか（必ずコピーしてから実行）
- 新しい P/Invoke を追加する場合は、既存の `Interop/NativeMethods.cs` に集約し、呼び出し元を明確にする
- `IToolPlugin` を増やす場合は、YMM4 側の列挙仕様を確認のうえ、01〜10 と同様のパターンで追加する

## テストの目安（手動）

1. メモ帳など単純なアプリで Ctrl + タイトルバードラッグ → Dock パネルへドロップ → 埋め込み表示
2. タブを切り替えたりドッキングを動かした後も、再表示で埋め込みが復帰するか
3. YMM4 終了後、外部アプリが独立ウィンドウに戻っているか（バックグラウンドに取り残されないか）
4. エクスプローラーを対象にしたとき、拒否メッセージになるか

## ライセンス・コントリビューション

MIT License（詳細は [LICENSE](../LICENSE) を参照）
