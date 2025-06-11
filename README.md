# Pixel MP Player

Google Pixel の Motion Photo（MP）を Windows でプレビューするアプリケーションです。

## Motion Photo について

Motion Photo は Google Pixel で撮影される、JPEG画像内にMP4動画を埋め込んだファイル形式です。
- JPEG画像の後ろにMP4動画データが付加されている
- ファイル名に "MP" が含まれることが多い
- 通常の画像ビューアーでは静止画のみ表示される

## 技術仕様

- **フレームワーク**: WPF (.NET 8)
- **言語**: C#
- **対応フォーマット**: Motion Photo JPEG files
- **機能**: 
  - 静止画と動画の切り替え表示
  - ドラッグ&ドロップ対応
  - 自動再生/手動制御

## 使い方

1. アプリケーションを起動
2. Motion Photo ファイルをドラッグ&ドロップするか「ファイルを開く」で選択
3. 「静止画」「動画」ボタンで表示を切り替え
4. 動画モードでは再生/一時停止/停止が可能

## ビルド・実行

### 必要な環境
- .NET 8.0 SDK
- Windows 10/11

### ビルド方法
`build.bat` を実行

### インストーラー作成

1. [Inno Setup](https://jrsoftware.org/isinfo.php) をインストール
2. `installer/setup.iss` をInno Setup Compilerで開く
3. コンパイル実行

配布用インストーラーは `installer/output/` フォルダに生成されます。

## 対応形式と制限

### 対応形式
- **Motion Photo**: JPEG+MP4が一つのファイルに埋め込まれた形式

### トラブルシューティング
- 動画が再生されない場合は「🔧 デバッグ」ボタンで詳細情報を確認

## 技術的詳細

Motion Photoの構造:
- **従来形式**: JPEG画像データ + MP4動画データ (一つのファイル)
- **新形式**: JPEG画像データ (カバー) + 動画データ (メタデータ)