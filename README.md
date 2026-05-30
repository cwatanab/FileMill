# FileMill

FileMill は Windows 向けの WPF バッチ処理アプリです。画像の一括変換と、Office Open XML ファイルや画像ファイルの軽量化を行います。

本アプリは、画像一括変換ソフト「Ralpha」のレイアウト・UI思想を踏襲して画像変換機能を提供し、Office Open XML最適化ツール「OptiOpenXML」の処理思想を取り入れ、それぞれを現代的な画像フォーマット（WebP、AVIF等）に対応させて統合したものです。

## 主な機能

- 画像ファイルまたはフォルダのドラッグ＆ドロップ登録
- 画像変換パイプライン（各ステップは右サイドバーのチェックボックスで有効化、歯車ボタンで詳細設定）
  - Exif 自動回転
  - リサイズ（幅・高さ・フィットモード・拡大許可）
  - グレースケール化
  - トリミング（幅・高さ指定）
  - 回転（90°/180°/270°、対象: 全体 or 縦横比に応じて自動）
  - 余白追加（サイズ・背景色 RGB）
  - アンシャープマスク（シグマ値）
  - 色調補正（明度・コントラスト）
  - トーンカーブ（ガンマ補正）
  - 減色（色数指定）
  - 画像合成 / ウォーターマーク（合成画像パス・XY オフセット）
  - 最適化（メタデータ削除、WebP 変換品質など）
  - JPEG / PNG / WebP / AVIF / TIFF への出力形式変換
- ファイル最適化
  - `.docx` / `.xlsx` / `.pptx` のメタデータ削除、再パック、埋め込み画像圧縮・WebP 変換
  - JPEG / PNG / WebP などの画像再圧縮
  - 画像ファイルの WebP 変換
  - 動画ファイル (`.mp4`, `.mkv` 等) の圧縮最適化 (`ffmpeg` 連携)
- 設定の永続化と外部ツール連携
  - ウィンドウ位置・サイズ・タブ、全変換パラメータ（Crop / Rotate / Padding / Sharpen / ColorAdjust / ToneCurve / Composite など）を `settings.ini` に自動保存
  - `oxipng` (PNG ロスレス最適化), `jpegli` (高品質 JPEG エンコード), `ffmpeg` (動画圧縮) との連携
    - デフォルトでは実行ファイルと同じディレクトリの `tools/` フォルダ以下 (`tools/oxipng.exe`, `tools/cjpegli.exe`, `tools/ffmpeg.exe`) を参照
    - パスは `設定 > オプション` の「外部ツール パス」セクションで変更可能

## 動作環境

- Windows
- .NET 10 SDK
- x64 環境

画像処理には `NetVips` と `NetVips.Native.win-x64` を使用しています。NuGet パッケージはビルド時に復元されます。

## ビルド

リポジトリ直下で次を実行します。

```bat
.\build.bat
```

バッチファイルは次の処理を行います。

1. `FileMill.csproj` の NuGet パッケージを復元する
2. `Release` 構成でビルドする
3. 出力先を表示する

手動で実行する場合は次のコマンドでもビルドできます。

```bat
dotnet restore FileMill.csproj
dotnet build FileMill.csproj --configuration Release
```

出力先は次のディレクトリです。

```text
bin\Release\net10.0-windows
```

## 実行

ビルド後、次の実行ファイルを起動します。

```text
bin\Release\net10.0-windows\FileMill.exe
```

開発中は次のコマンドでも起動できます。

```bat
dotnet run --project FileMill.csproj
```

## 画像変換の使い方

1. `画像変換` タブを開く
2. 画像ファイルまたはフォルダを追加する
3. 出力先フォルダを選択する (ツールバー中央)
4. 必要に応じて `設定 > オプション` メニューからファイル名生成規則などを変更する
5. 有効にする処理オプションのチェックを入れ、歯車ボタンから詳細設定を調整する
6. `変換` を実行する

ファイル名生成規則では `<old>` が元ファイル名に置き換えられます。既定値は `resize_<old>-s` です。`<old>` を含まない場合は、入力した文字列が元ファイル名の前に付与されます。

出力ファイル名が既存ファイルと重複する場合は、末尾に `_1`, `_2` のような連番が付きます。

## ファイル最適化の使い方

1. `ファイル最適化` タブを開く
2. Office ファイル、画像ファイル、または動画ファイルを追加する
3. 出力先フォルダを選択する (ツールバー中央)
4. 有効にする最適化オプション（Office / 画像 / 動画）のチェックを入れ、歯車ボタンから詳細設定（メタデータ削除、WebP変換、品質、動画CRF等）を調整する
5. `最適化実行` を押す

最適化後のファイルは、元ファイルと同じフォルダに `_optimized` を付けて保存されます（出力先が空欄の場合）。重複する場合は `_optimized_1`, `_optimized_2` のような連番が付きます。

画像を WebP 形式に変換する設定をオンにすると、Office ファイル内の埋め込み画像も WebP に差し替えます。

### Office ファイル内画像の WebP 変換

`.docx`, `.xlsx`, `.pptx` は ZIP ベースの Office Open XML パッケージとして処理します。埋め込み画像の WebP 変換を行う場合、単に画像ファイルだけを置き換えるのではなく、パッケージ内の参照情報も更新します。

- 画像パーツを WebP に変換し、拡張子を `.webp` に変更する
- `[Content_Types].xml` に `image/webp` の ContentType を追加または修正する
- `.rels` の画像参照先を `.webp` に更新する
- `.rels` の参照先に `#fragment` や `?query` が含まれる場合は、その末尾情報を維持する
- 作成者や更新者などのメタデータを削除する場合も、Open XML の型付き値を壊さない形で処理する

PowerPoint で `コンテンツに問題が見つかりました` と表示される場合は、古い最適化結果ではなく、現在のビルドで再度最適化してください。既存の最適化済みファイルを再処理するより、元の PPTX から作り直すほうが安全です。

WebP に対応していない古い Office 環境では、ファイル自体が正常でも画像が表示されない可能性があります。PowerPoint での利用を前提にする場合は、配布先の Office バージョンで開けることを確認してください。

## 対応形式

画像変換の入力は、UI では次の拡張子を対象にしています。

```text
.jpg .jpeg .png .webp .avif .tif .tiff .gif .svg .bmp .heic .heif
```

実際に読み込める形式は libvips の対応状況に依存します。画像変換の出力形式は次のとおりです。

```text
JPEG PNG WebP AVIF TIFF
```

ファイル最適化では、Office Open XML 形式の `.docx`, `.xlsx`, `.pptx` と、JPEG / PNG / WebP / AVIF / TIFF / BMP などの画像を処理対象にしています。

## プロジェクト構成

```text
FileMill.csproj                 WPF アプリのプロジェクト定義
build.bat                       Release ビルド用バッチファイル
App.xaml / App.xaml.cs          アプリケーション定義 (設定永続化テストを含む)
MainWindow.xaml                 メイン画面の UI (設定モーダル・サイドバーバインディング)
MainWindow.xaml.cs              ドラッグ＆ドロップ、ソート、画面イベント
ViewModels/MainViewModel.cs     画面状態、コマンド、変換・最適化フロー
Services/ImageProcessingService.cs
                                画像処理、Office パッケージ最適化
Models/                         画面表示用モデルと処理ステップ定義
Converters/                     WPF バインディング用コンバーター
                                (BooleanToVisibility, StringEqualsToVisibility,
                                 EnumToBoolean)
```

## 開発メモ

- UI は WPF / XAML で構成されています。
- MVVM 形式をベースに、画面操作は `MainViewModel` のコマンドへ集約しています。
- 画像処理と Office Open XML パッケージ処理は `ImageProcessingService` に集約しています。
- 設定は `App.xaml.cs` の `LoadSettings` / `SaveSettings` で `settings.ini` に読み書きします。
- 回転ターゲットの RadioButton バインドには `EnumToBooleanConverter` を使用しています。
- `bin/` と `obj/` はビルド生成物のため Git 管理対象外です。

### テスト

次のコマンドで設定永続化・モーダル状態・リスト管理の自動テストを実行できます。

```bat
dotnet run --project FileMill.csproj -- --test-settings
```

3 つのスイートすべてが `passed` と表示されれば正常です。

## 謝辞

本アプリの開発にあたり、以下の素晴らしいソフトウェアとその制作者様に深く感謝の意を表します。

- **Ralpha / RalphaPlus** (にるぽ / Nilposoft 氏)
  - 軽快で使いやすい画像変換の画面レイアウトおよび機能デザインの参考にさせていただきました。
  - 公式サイト: [Nilposoft](http://nilposoft.info/ralpha/ralphaplus64.html)
- **OptiOpenXML**
  - Word, Excel, PowerPoint などの Office Open XML 文書の軽量化・最適化処理の設計・アイデアの参考にさせていただきました。
  - 紹介・解説ページ: [OptiOpenXML](https://www.hiskip.com/free/freesoft/doc/office/14978.html)
