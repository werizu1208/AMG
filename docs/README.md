# 企画管理（docs）

AMGの企画・アイデア・TODOをMarkdownで管理する場所です。
ゲーム本体の情報はリポジトリ直下の [README](../README.md) を見てください。

## フォルダ構成

| フォルダ | 用途 |
| --- | --- |
| [`inbox/`](inbox/) | 思いついたことをとりあえず放り込む場所（スマホからのメモ向け） |
| [`ideas/`](ideas/) | 企画のアイデア。1アイデア1ファイル |
| [`projects/`](projects/) | 進行中の企画。1企画1フォルダ |
| [`meetings/`](meetings/) | 打ち合わせ・作業メモ |
| [`templates/`](templates/) | 新しいファイルを作るときのひな形 |
| [`TODO.md`](TODO.md) | 全体のやることリスト |

## 使い方

1. 思いつきは `inbox/` に書く（形式自由）
2. 育てたいものは [`templates/idea.md`](templates/idea.md) をコピーして `ideas/` へ
3. 動き出したら `projects/<企画名>/` を作り、[`templates/project.md`](templates/project.md) を `README.md` としてコピー
4. 終わった・やめた企画は、ファイル先頭の「状態」を更新する（削除しない）

ファイル名は `YYYY-MM-DD-短い名前.md`（例: `2026-10-02-新企画.md`）にすると並びが揃います。

## 複数機器での同期

同期の手順はリポジトリ直下の [README](../README.md#複数のpc機器で作業する) を見てください。
スマホからは `inbox/` に新しいファイルを追加する運用にすると、競合をほぼ避けられます。
