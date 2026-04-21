# ModuTree プロジェクト ガイドライン

## プロジェクト概要

**ModuTree** は、Unity向けのモジュール式 Behaviour Tree（行動木）エディタ＆ランタイムシステムです。
- パッケージ名: `amp.modutree` / バージョン: `1.0.0`
- Unity 2021.3 以上対応
- **Pure C# ベース**で設計され、Unity とサーバーの両プラットフォームで動作可能
- ScriptableObject を使わず、**JSON ベース**でツリーを保存・読み込み

---

## ディレクトリ構造

```
Assets/
├── ModuTree/
│   ├── Runtime/                    # Pure C# — このフォルダをコピーするだけでサーバーでも動く
│   │   ├── ModuTree.Runtime.asmdef   (noEngineReferences: true)
│   │   ├── Core/                   # 基底クラス・インターフェース
│   │   ├── Engine/                 # BehaviourTreeEngine
│   │   ├── Nodes/                  # 抽象基底 + 組み込みコンポジットノード
│   │   └── Serialization/          # JSON シリアライズ（MiniJson、外部依存ゼロ）
│   │
│   ├── UnityIntegration/           # Unity専用（MonoBehaviour）
│   │   ├── ModuTree.UnityIntegration.asmdef
│   │   └── BehaviourTreeRunner.cs
│   │
│   └── Editor/                     # Unity エディター拡張
│       ├── ModuTree.Editor.asmdef
│       ├── NodeTypeRegistry.cs     # リフレクションによるノード型管理
│       ├── SelectionObserver.cs    # Project/Hierarchy選択監視
│       ├── Windows/
│       │   └── BehaviourTreeEditorWindow.cs
│       └── Inspectors/
│           ├── BehaviourNodeInspector.cs
│           └── BehaviourTreeRunnerInspector.cs
│
└── Samples/
    └── ModuTree/
        └── BasicSample/            # サンプル実装（敵AI）
            ├── ModuTree.Samples.asmdef
            ├── Keys/               # Blackboard 型安全キー定義
            ├── Nodes/              # サンプルノード
            ├── SampleEnemyRunner.cs
            ├── SamplePlayerController.cs
            └── EnemyBehaviourTree.json
```

---

## アーキテクチャ設計原則

### 核となる 5 つの原則

1. **Pure C#** — `Runtime/` 以下は Unity 非依存（`noEngineReferences: true`）
2. **非同期駆動** — `async Task` で `Running` 状態を自然に表現
3. **Blackboard 分離** — AI 判断と実行制御を疎結合に保つ
4. **JSON 永続化** — ScriptableObject に依存しない。`MiniJson` で外部依存ゼロ
5. **メタプログラミング** — `Attribute` でエディター UI を自動生成

### 実行フロー

```
JSON → BehaviourTreeSerializer.Build()
  ↓
BehaviourNodeData ツリー構築（Guid付き）
  ↓
BehaviourTreeEngine.UpdateAsync() を毎フレーム呼び出し
  ↓
Node: OnStartAsync() → OnUpdateAsync() → OnStopAsync()
  ↓
Blackboard に判断結果を書き込み
  ↓
外部システム（Runner）が Blackboard を読んで実際の動作を実行
```

### 明示的な再実行

ツリーが Success / Failure を返しても**自動でループしない**。再実行は呼び出し側が明示的に行う。

```csharp
var result = await Engine.UpdateAsync(ct);
if (result != NodeState.Running)
{
    Engine.Reset();             // 全ノードを Idle に戻す
    // 必要なら次のフレームで再度 UpdateAsync() を呼ぶ
}
```

### ホットリロード

実行中に JSON を差し替えると全ノードをリセットして即座に切り替わる。

```csharp
runner.ReloadJson(newJsonText);   // Runner側
// または
engine.Initialize(newJsonText);  // Engine直接
```

---

## クラス設計

### ノード型の階層

```
BehaviourNodeData（抽象基底）  ← Guid, State, Blackboard を持つ
├── ActionNodeData（抽象）              namespace: ModuTree.Runtime.Nodes
├── ConditionNodeData（抽象）           namespace: ModuTree.Runtime.Nodes
├── CompositeNodeData（抽象）           namespace: ModuTree.Runtime.Nodes
│   ├── SequenceNodeData               — 全子が成功すれば成功（AND）
│   ├── SelectorNodeData               — いずれかが成功すれば成功（OR）
│   └── ReactiveSelectorNodeData       — 毎フレーム先頭から再評価（優先度割り込み）
├── DecoratorNodeData（抽象）           namespace: ModuTree.Runtime.Nodes
└── SubTreeNodeData                    — 外部JSONを参照するサブツリー
```

### NodeState 列挙型

| 状態 | 意味 |
|------|------|
| `Idle` | 未実行の初期状態 |
| `Running` | 実行中（次フレームも実行継続） |
| `Success` | 成功終了 |
| `Failure` | 失敗終了 |

### ノードのライフサイクル

```
初回実行: OnStartAsync() → State = Running
毎フレーム: OnUpdateAsync() → State 更新
終了時: OnStopAsync() → State = Success / Failure
中断時: OnAbortAsync() → OnStopAsync() → State = Failure
```

### SubTreeNodeData

別のAIデータ（JSONファイル）を参照し、親ツリーからはActionノードのように振る舞う。

- `subTreeJsonPath` フィールドに**絶対パス**で参照JSONを指定
- 親ツリーの `Blackboard` を共有する
- エディタ上でダブルクリックするとサブツリーの中を表示できる

---

## 重要な実装パターン

### カスタムノードの作り方

`BehaviourNodeMetaAttribute` は必須。これがないとエディターに表示されない。

```csharp
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;   // ActionNodeData / ConditionNodeData はこちら

[BehaviourNodeMeta(
    displayName: "ノード表示名",
    category:    "Action",       // "Action", "Condition", "Composite", "Decorator", "SubTree"
    description: "ノードの説明",
    color:       "#1E3A6E")]
public class MyActionNode : ActionNodeData
{
    [NodeField("フィールド名", "ツールチップ説明")]
    public float speed = 2f;

    protected override async Task<NodeState> OnUpdateAsync(CancellationToken ct)
    {
        // Blackboard にアクセス
        var agent = Blackboard.Get(MyBBKeys.Agent);
        return NodeState.Success;
    }
}
```

### Blackboard の型安全な使い方

キーは必ず専用ファイルで `BlackboardKey<T>` として定義する。

```csharp
// キー定義ファイル（例: MyBBKeys.cs）
public static class MyBBKeys
{
    public static readonly BlackboardKey<Transform> Agent    = new("agent");
    public static readonly BlackboardKey<float>     Distance = new("distance");
}

// ノード内での使用
var agent = Blackboard.Get(MyBBKeys.Agent);
Blackboard.Set(MyBBKeys.Distance, Vector3.Distance(pos1, pos2));
```

### BehaviourTreeRunner の継承（Unity）

`BehaviourTreeRunner` を継承して `SetupBlackboard()` をオーバーライドする。
**ノード内では Unity API（Transform.Translate 等）を直接呼ばない**。
Blackboard に指示を書き込み、Runner 側で実行する。

```csharp
public class EnemyRunner : BehaviourTreeRunner
{
    public Transform player;
    public float patrolRadius = 8f;

    protected override void SetupBlackboard(Blackboard blackboard)
    {
        blackboard.Set(MyBBKeys.Agent,  transform);
        blackboard.Set(MyBBKeys.Player, player);
        blackboard.Set(MyBBKeys.PatrolCenter, transform.position);
        blackboard.Set(MyBBKeys.PatrolRadius, patrolRadius);
    }

    protected override async void Update()
    {
        if (Engine == null) return;
        await Engine.UpdateAsync(_cts.Token);
        ApplyMovement();   // Blackboardを読んで実際に動かす
    }
}
```

---

## コーディング規約

### コメント・ドキュメント

- XML ドキュメント: `/// <summary>日本語で説明</summary>`
- インラインコメント: `// 日本語で注釈`
- エラーログ: `Console.Error.WriteLine("日本語")` (Runtime) / `Debug.LogError("日本語")` (Unity)

### Attribute の使い方

| Attribute | 対象 | 用途 |
|-----------|------|------|
| `[BehaviourNodeMeta(...)]` | クラス | ノードをエディターに登録（必須） |
| `[NodeField("名前", "説明")]` | フィールド | エディターでラベルを付ける |
| `[NodeFieldHide]` | フィールド / プロパティ | エディターで非表示にする |

### アセンブリ構成

| Assembly | 用途 |
|----------|------|
| `ModuTree.Runtime` | ランタイムコア（Unity 非依存、`noEngineReferences: true`） |
| `ModuTree.UnityIntegration` | Unity用 MonoBehaviour（`BehaviourTreeRunner`） |
| `ModuTree.Editor` | エディター拡張（Editor専用） |
| `ModuTree.Samples` | サンプル実装 |

---

## JSON ファイル形式（バージョン 2.0.0）

```json
{
  "version": "2.0.0",
  "rootGuid": "...",
  "nodes": [
    {
      "guid": "...",
      "typeName": "ModuTree.Runtime.Nodes.SequenceNodeData, ModuTree.Runtime",
      "positionX": 0.0,
      "positionY": 0.0,
      "childrenGuids": ["..."],
      "childGuid": null,
      "parametersJson": "{\"speed\":2.0}"
    }
  ]
}
```

- `typeName` は `FullTypeName, AssemblyName` の短縮形を使う（バージョン等は不要）
- `childrenGuids` は CompositeNodeData 用、`childGuid` は DecoratorNodeData 用
- `parametersJson` は `[NodeFieldHide]` のないパブリックフィールドを MiniJson でシリアライズしたもの

---

## エディターの操作

| 操作 | 内容 |
|------|------|
| `Window > ModuTree > BehaviourTree Editor` | エディターウィンドウを開く |
| 右クリック（空白） | ノード追加メニュー（Rootがない場合はComposite系のみ） |
| ノード中央をドラッグ | ノード移動 |
| ノードの上端/下端をドラッグ | 別ノードへの接続 |
| ノード単体クリック | Inspector にパラメータを表示 |
| 空白ドラッグ | ボックス選択（複数選択） |
| `⌘S` / `Ctrl+S` | 保存（元のJSONファイルを上書き） |
| 別名保存ボタン | 別ファイルとして保存（Variant作成に便利） |
| `⌘Z` / `Ctrl+Z` | Undo |
| `⌘C` / `⌘X` / `⌘V` | コピー / カット / ペースト |
| `Delete` / `Backspace` | 選択ノードを削除 |
| スクロール | ズームイン/アウト |
| SubTreeノードをダブルクリック | サブツリーの中を表示 |
| Play中にHierarchyでRunnerを選択 | 実行中ノードをリアルタイムで視覚確認 |

---

## 注意事項・既知の問題

- **ReactiveSelectorNodeData** は低優先度のノードが実行中の場合のみ高優先度ノードをプローブする。高優先度自体が実行中の場合は再プローブしない
- ノード生成は `Activator.CreateInstance(type)` によるリフレクションを使用。`typeName` を変更すると既存JSONが壊れる
- `DecoratorNodeData` は抽象クラスのみ。組み込み実装は存在しないためユーザーが実装する
- `SubTreeNodeData` は `subTreeJsonPath` に**絶対パス**を指定する。相対パスは未対応
- `[NodeFieldHide]` は `AttributeTargets.Field | Property` の両方に付与可能

---

## サンプルの動作確認

`Assets/Samples/ModuTree/BasicSample/EnemyBehaviourTree.json` に以下のツリーが定義されている。

```
ReactiveSelectorNodeData（ルート）
├── SequenceNodeData
│   ├── IsPlayerDetectedCondition（5m以内を検出 → 追跡開始）
│   └── ChasePlayerAction（4m/sで追跡 / パトロール範囲外でFailure）
└── PatrolAction（2m/sでランダムパトロール）
```

**動作ロジック:**
- パトロール中: ReactiveSelectorが毎フレームSequenceをプローブ → プレイヤーが5m以内に入ったら即追跡開始
- 追跡中: 追跡開始後はプレイヤーが離れても追い続ける
- 追跡終了: 敵がパトロール範囲（デフォルト8m）外に出たらChasePlayerActionがFailureを返し、パトロール再開

**シーンセットアップ:**
1. Plane（地面）、Capsule×2（プレイヤー・敵）を配置
2. プレイヤーに `SamplePlayerController` をアタッチ
3. 敵に `SampleEnemyRunner` をアタッチ、`EnemyBehaviourTree.json` と Player を設定
4. カメラを `(0, 15, 0)` / Rotation `(90, 0, 0)` の見下ろし配置