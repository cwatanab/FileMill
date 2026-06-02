# FileMill 詳細仕様

FileMill は Windows 向けの WPF バッチ処理アプリです。画像の一括変換と、Office Open XML ファイルの軽量化、PDF 変換、PDF 最適化を行います。

本アプリは、画像一括変換ソフト「Ralpha」のレイアウト・UI 思想を踏襲して画像変換機能を提供し、Office Open XML 最適化ツール「OptiOpenXML」の処理思想を取り入れ、それぞれを現代的な画像フォーマットに対応させて統合したものです。

## 主な機能

- 画像ファイルまたはフォルダのドラッグ＆ドロップ登録
- 画像変換パイプライン
  - Exif 自動回転
  - リサイズ
  - グレースケール化
  - トリミング
  - 回転
  - 余白追加
  - アンシャープマスク
  - 色調補正
  - トーンカーブ
  - 減色
  - 画像合成 / ウォーターマーク
  - メタデータ削除と画像最適化
  - JPEG / PNG / WebP / AVIF / TIFF への出力形式変換
- Office ファイル変換
  - `.docx` / `.xlsx` / `.pptx` のメタデータ削除、再パック、埋め込み画像圧縮
  - 埋め込み画像の PPI 制限によるリサイズ
  - 埋め込み画像の WebP 変換
  - 埋め込み動画の圧縮最適化
  - `.docx` / `.xlsx` / `.pptx` の PDF 変換
  - Word / PowerPoint の PDF/A 変換
- PDF ファイル最適化
  - qpdf を使用した PDF の画像最適化、ストリーム圧縮、オブジェクトストリーム生成、リニアライズ
- 設定とプリセット
  - General 設定は `settings.ini` に保存
  - 画像変換、Office 変換、PDF 最適化はセクションごとにプリセット保存
  - 最後に使ったプリセットを起動時に自動ロード
- 外部ツール連携
  - `oxipng`
  - `jpegli`
  - `ffmpeg`
  - `qpdf`

## 動作環境

- Windows
- .NET 10 SDK
- x64 環境

画像処理には `NetVips` と `NetVips.Native.win-x64` を使用しています。NuGet パッケージはビルド時に復元されます。

## ビルド

リポジトリ直下で次を実行します。

```bash
make build
```

Makefile の `build` ターゲットは次の処理を行います。

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

## リリース

ZIP パッケージだけを作成する場合は次を実行します。

```bash
make package TAG=0.2
```

GitHub Release まで公開する場合は次を実行します。

```bash
make release TAG=0.2
```

`release` ターゲットは次の処理を行います。

1. GitHub CLI (`gh`) のインストールと認証状態を確認する
2. 未コミット変更がある場合は続行確認を行う
3. リリース内容を表示し、公開前の確認を行う
4. `Release` 構成でビルドする
5. `FileMill-<TAG>.zip` を作成する
6. Git タグを作成する
7. 現在のブランチとタグを GitHub に push する
8. `gh release create` で GitHub Release を作成し、ZIP をアップロードする

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
3. 出力先フォルダを選択する
4. 必要に応じてプリセットを選択する
5. 有効にする処理オプションのチェックを入れ、詳細設定を調整する
6. `変換` を実行する

ファイル名生成規則では、入力した文字列が元のファイル名の末尾に付与されます。既定値は `_converted` です。

出力ファイル名が既存ファイルと重複する場合は、末尾に `_1`, `_2` のような連番が付きます。

## Office ファイル変換の使い方

1. `Office ファイル変換` タブを開く
2. Office ファイル (`.docx` / `.xlsx` / `.pptx`) を追加する
3. 出力先フォルダを選択する
4. 必要に応じてプリセットを選択する
5. 有効にする変換オプションのチェックを入れ、詳細設定を調整する
6. PDF に変換する場合は `PDFへ変換` をオンにする
7. `変換実行` を押す

最適化後のファイルは、元ファイルと同じフォルダに `_optimized` を付けて保存されます。出力先を指定した場合は、そのフォルダへ出力されます。重複する場合は `_optimized_1`, `_optimized_2` のような連番が付きます。

PDF 変換後のファイルは `_converted.pdf` として保存されます。Microsoft Office Interop が利用できない環境では、PDF 変換オプションは無効になります。

PDF/A 変換は Word と PowerPoint で有効です。Excel の Microsoft Office Interop API には PDF/A 指定がないため、Excel ファイルで `PDF/A 準拠` をオンにするとエラーになります。

## Office ファイル変換の詳細

### メタデータ削除

Office Open XML パッケージ内の `docProps/core.xml`, `docProps/app.xml`, `docProps/custom.xml` を対象に、作成者、最終保存者、編集履歴などの不要な情報を削除します。

### 再パック

Office Open XML パッケージを ZIP として再作成し、不要なデータや圧縮効率の悪い状態を減らします。

### 埋め込み画像の圧縮

Office ファイル内の画像パーツを再エンコードします。JPEG / PNG / WebP などを対象に、設定された品質や外部ツール設定に応じて最適化します。

### 埋め込み画像の PPI 制限

Office 上の表示サイズから実効 PPI を計算し、指定 PPI を超える画像だけ縮小します。

- UI 表記は `PPI`
- 指定範囲は 72 から 600
- 表示サイズが判定できない画像は縮小しない
- 同じ画像が複数箇所で使われる場合は最大表示サイズを採用する
- PPI 制限だけが有効な場合、縮小対象外の画像は再圧縮しない

### Office ファイル内画像の WebP 変換

`.docx`, `.xlsx`, `.pptx` は ZIP ベースの Office Open XML パッケージとして処理します。埋め込み画像の WebP 変換を行う場合、単に画像ファイルだけを置き換えるのではなく、パッケージ内の参照情報も更新します。

- 画像パーツを WebP に変換し、拡張子を `.webp` に変更する
- `[Content_Types].xml` に `image/webp` の ContentType を追加または修正する
- `.rels` の画像参照先を `.webp` に更新する
- `.rels` の参照先に `#fragment` や `?query` が含まれる場合は、その末尾情報を維持する
- 作成者や更新者などのメタデータを削除する場合も、Open XML の型付き値を壊さない形で処理する

PowerPoint で `コンテンツに問題が見つかりました` と表示される場合は、古い最適化結果ではなく、現在のビルドで再度最適化してください。既存の最適化済みファイルを再処理するより、元の PPTX から作り直すほうが安全です。

WebP に対応していない古い Office 環境では、ファイル自体が正常でも画像が表示されない可能性があります。PowerPoint での利用を前提にする場合は、配布先の Office バージョンで開けることを確認してください。

### 埋め込みメディアの圧縮

Office ファイル内の動画・音声パーツを `ffmpeg` で圧縮します。動画品質は CRF、映像・音声コーデックは UI から指定できます。

## PDF ファイル最適化の使い方

1. `PDF ファイル最適化` タブを開く
2. PDF ファイルを追加する
3. 出力先フォルダを選択する
4. 必要に応じてプリセットを選択する
5. 右側の PDF 最適化オプションを調整する
6. `最適化実行` を押す

最適化後のファイルは、元ファイルと同じフォルダに `_optimized.pdf` を付けて保存されます。出力先を指定した場合は、そのフォルダへ出力されます。重複する場合は `_optimized_1.pdf`, `_optimized_2.pdf` のような連番が付きます。

PDF 最適化には `qpdf.exe` が必要です。既定では `tools/qpdf.exe` を参照し、見つからない場合は PATH 上の `qpdf.exe` と `C:\Program Files\qpdf\bin\qpdf.exe` も探します。パスは `設定 > オプション` の「外部ツール パス」から変更できます。

### PDF の画像最適化

PDF 最適化の `画像を最適化` は qpdf の機能です。FileMill 独自の画像リサイズ処理ではなく、qpdf に次の引数を渡します。

```text
--optimize-images
--jpeg-quality=<品質>
--oi-min-width=<最小幅>
--oi-min-height=<最小高さ>
--oi-min-area=<最小面積>
--keep-inline-images
```

これは PDF 内画像を表示サイズに合わせて PPI 指定で下げる機能ではありません。条件に合う画像を JPEG / DCT 圧縮へ再圧縮し、サイズが小さくなる場合に反映する qpdf の最適化です。

## プリセット

General 以外の設定は、用途ごとにプリセットとして保存できます。

- 画像変換: `presets/image`
- Office 変換: `presets/office`
- PDF 最適化: `presets/pdf`

アプリケーション設定としては、ユーザーごとの LocalAppData 配下にもプリセットが保存されます。プロジェクト同梱のおすすめプリセットは `presets/` 配下に置きます。

最後に選択していたプリセット名は `settings.ini` の `[General]` に保存され、起動時に存在する場合だけ自動でロードされます。

```ini
LastImagePresetName=...
LastOfficePresetName=...
LastPdfPresetName=...
```

## 対応形式

画像変換の入力は、UI では次の拡張子を対象にしています。

```text
.jpg .jpeg .png .webp .avif .tif .tiff .gif .svg .bmp .heic .heif
```

実際に読み込める形式は libvips の対応状況に依存します。

画像変換の出力形式は次のとおりです。

```text
JPEG PNG WebP AVIF TIFF
```

Office ファイル変換では、Office Open XML 形式の `.docx`, `.xlsx`, `.pptx` を処理対象にしています。

PDF ファイル最適化では `.pdf` を処理対象にしています。

## 外部ツール

既定では実行ファイルと同じディレクトリの `tools/` フォルダ以下を参照します。

```text
tools/oxipng.exe
tools/cjpegli.exe
tools/ffmpeg.exe
tools/qpdf.exe
```

パスは `設定 > オプション` の「外部ツール パス」セクションで変更できます。

## プロジェクト構成

```text
FileMill.csproj                 WPF アプリのプロジェクト定義
App.xaml / App.xaml.cs          アプリケーション定義
MainWindow.xaml                 メイン画面の UI
MainWindow.xaml.cs              ドラッグ＆ドロップ、ソート、画面イベント
ViewModels/MainViewModel.cs     画面状態、コマンド、変換・最適化フロー
Services/ImageProcessingService.cs
                                画像処理、Office パッケージ最適化、Office PDF 変換
Services/PdfOptimizationService.cs
                                qpdf による PDF 最適化
Models/                         画面表示用モデルと処理ステップ定義
Converters/                     WPF バインディング用コンバーター
Themes/                         ライト・ダークテーマ
presets/                        同梱プリセット
```

## 開発メモ

- UI は WPF / XAML で構成されています。
- MVVM 形式をベースに、画面操作は `MainViewModel` のコマンドへ集約しています。
- 画像処理と Office Open XML パッケージ処理は `ImageProcessingService` に集約しています。
- PDF 最適化は `PdfOptimizationService` で qpdf を呼び出します。
- 設定は `SettingsService` で INI 形式として読み書きします。
- `bin/` と `obj/` はビルド生成物のため Git 管理対象外です。

## テスト

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
