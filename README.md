# AMG（A・M・G）

Unity 6 で開発中のTPSアクションゲームです。
現在はα版として、ステージ1のボス戦「植物に埋もれた村の魔法少女」が遊べます。

> ⚠️ このリポジトリは **public** です。未公開の企画を置く場合は、
> GitHubの Settings → General → Danger Zone から **private** への変更を検討してください。

## フォルダ構成

| 場所 | 内容 |
| --- | --- |
| [`Assets/AMG/`](Assets/AMG/) | ゲーム本体（スクリプト・シーン・マテリアル） |
| [`docs/`](docs/) | 企画・アイデア・TODO・打ち合わせメモ（[使い方](docs/README.md)） |
| `Packages/`, `ProjectSettings/` | Unityのプロジェクト設定 |

## Unityで開く

1. Unity Hub で **Unity 6000.0.32f1** をインストール
2. Unity Hub の「Add」→ このリポジトリのフォルダを選択
3. 初回は `Library/` の生成に時間がかかります（Gitには含まれません）
4. `Assets/AMG/Scenes/Alpha_Boss1.unity` を開いて再生

シーンはメニュー **A・M・G → α版シーンを生成** で作り直せます。
作り直すとシーンファイルの差分が大きくなるので、コミットは必要なときだけにしてください。

## 操作（α版）

| キー | 操作 |
| --- | --- |
| WASD / Shift / Space / Ctrl | 移動 / ダッシュ / ジャンプ / 回避 |
| 右クリック / 左クリック | エイム / 射撃 |
| 1 / 2 | 武器切り替え（出撃前はウルト選択） |
| R | リロード（死亡・撃破後はリトライ） |
| E / F | グレネード / 回復キット |
| Q | ウルト（空気の層 / 人類の英知） |
| Esc | カーソル解放 |

## 複数のPC・機器で作業する

```sh
# 初回
git clone https://github.com/werizu1208/AMG.git

# 作業前に必ず最新を取得
git pull

# 作業後
git add .
git commit -m "変更内容"
git push
```

- **作業前に `git pull`、作業後に `git push`** を習慣にすると競合が起きにくくなります。
- Unityのシーン（`.unity`）は手で競合を直すのが難しいので、**同じシーンを2台で同時に編集しない**でください。
- `.meta` ファイルは必ず元ファイルと一緒にコミットしてください。
- スマホ・タブレットからは github.com や github.dev（リポジトリページで `.` キー）で `docs/` の企画メモを編集できます。
