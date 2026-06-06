# FileMill

FileMill は、画像・Office ファイル・PDF をまとめて変換、軽量化する Windows 向けデスクトップアプリです。

ドラッグ＆ドロップでファイルを並べ、必要な処理を選び、まとめて実行できます。日々の資料整理、画像の一括加工、Office 文書の軽量化、PDF 配布前の変換をひとつの画面で扱えるようにしています。

## 特徴

- 画像の一括変換、リサイズ、回転、色調補正、ウォーターマーク、メタデータ削除
- Word / Excel / PowerPoint ファイルの軽量化、PDF 変換、埋め込み画像・動画の最適化
- PDF の画像最適化、ストリーム圧縮、構造変換、リニアライズ
- 画像変換・Office 変換・PDF 変換ごとのプリセット保存
- 最後に使ったプリセットを起動時に自動ロード
- WebP / AVIF などの現代的な画像形式に対応
- oxipng、jpegli、ffmpeg、qpdf などの外部ツール連携

## こんな用途に

- 会議資料や提案書を配布前に軽くしたい
- 大量の画像を同じ条件でリサイズ、変換したい
- PowerPoint や Word を PDF / PDF/A として出力したい
- PDF を Web 配布向けに圧縮、変換したい
- 毎回同じ変換条件をプリセットとして使い回したい

## 3 つの作業モード

### 画像変換

JPEG / PNG / WebP / AVIF / TIFF などの画像を対象に、リサイズ、トリミング、回転、余白追加、色調補正、シャープ、減色、合成、形式変換をまとめて実行できます。

### Office ファイル変換

`.docx` / `.xlsx` / `.pptx` を対象に、メタデータ削除、再パック、埋め込み画像の圧縮、画像解像度の PPI 制限、WebP 変換、動画圧縮、PDF 変換を行えます。

### PDF ファイル変換

qpdf を使って、PDF の画像再圧縮、ストリーム圧縮、オブジェクトストリーム、未参照リソース、PDF バージョン、リニアライズを調整できます。Web 配布や共有前の軽量化、構造整理に向いています。

## 動作環境

- Windows
- .NET 10
- x64 環境

Office ファイルの PDF 変換には、対象形式に対応した Microsoft Office が必要です。PDF 変換には qpdf、動画圧縮には ffmpeg を使用します。

## ビルド

```bash
make build
```

手動で実行する場合:

```bat
dotnet restore FileMill.csproj
dotnet build FileMill.csproj --configuration Release
```

## 詳細仕様

詳しい使い方、対応形式、外部ツール、リリース手順、開発メモは次のドキュメントに分けています。

- [詳細仕様](docs/specification.md)

## 謝辞

FileMill は、画像一括変換ソフト **Ralpha / RalphaPlus** の軽快な画面設計と、Office Open XML 最適化ツール **OptiOpenXML** の処理思想を参考にしています。

- [Ralpha / RalphaPlus](http://nilposoft.info/ralpha/ralphaplus64.html)
- [OptiOpenXML](https://www.hiskip.com/free/freesoft/doc/office/14978.html)

また、以下の外部ツールを利用しています。

| ツール | ライセンス |
|--------|-----------|
| [qpdf](https://github.com/qpdf/qpdf) | Apache 2.0 |
| [oxipng](https://github.com/oxipng/oxipng) | MIT |
| [cjpegli / libjxl](https://github.com/libjxl/libjxl) | BSD 3-Clause |
| [FFmpeg](https://ffmpeg.org) | LGPL v3 |
| [NetVips / libvips](https://github.com/kleisauke/net-vips) | MIT / LGPL v2.1+ |
