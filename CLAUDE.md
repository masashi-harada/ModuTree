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
        ├── BasicSample/            # リアルタイムAIサンプル（敵の追跡・パトロール）
        │   ├── ModuTree.Samples.asmdef
        │   ├── Keys/
        │   ├── Nodes/
        │   ├── SampleEnemyRunner.cs
        │   ├── SamplePlayerController.cs
        │   └── EnemyBehaviourTree.json
        │
        ├── SubTreeSample/          # サブツリーサンプル（警備員AI）
        │   ├── ModuTree.SubTreeSample.asmdef
        │   ├── Keys/               # SentryBBKeys.cs
        │   ├── Nodes/              # IsPlayerSpottedCondition / RandomPatrolAction / ChaseAndReturnAction
        │   ├── SentryRunner.cs
        │   ├── SentryPlayerController.cs
        │   ├── AlertGuardBehaviourTree.json
        │   └── SubTrees/
        │       ├── Patrol.json
        │       └── ChaseUntilSafe.json
        │
        └── JankenSample/           # ワンショット実行サンプル（ジャンケンAI）
            ├── ModuTree.JankenSample.asmdef
            ├── Keys/               # JankenBBKeys.cs（JankenHand enum を含む）
            ├── Nodes/              # IsPlayerChoseCondition / SelectHandAction
            ├── JankenRunner.cs
            └── JankenBehaviourTree.json
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
BehaviourTreeEngine.UpdateAsync() を呼び出し
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

### SequenceNodeData の1ステップ進行

`SequenceNodeData` は子ノードが Success を返すたびに **Running を返して次フレームへ委ねる**設計。
1回の `UpdateAsync()` 呼び出しで1子ノード分しか進まない。

- **リアルタイム用途（毎フレーム実行）**: `Update()` で毎フレーム `UpdateAsync()` を呼ぶ → 自然に動作
- **ワンショット用途（1回で完結させたい場合）**: `Running` でなくなるまでループする

```csharp
// ワンショット実行パターン
Engine.Reset();
NodeState state;
do
{
    state = await Engine.UpdateAsync(ct);
}
while (state == NodeState.Running && !ct.IsCancellationRequested);
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
BehaviourNodeData（抽象基底）  ← Guid, State, Blackboard, BaseDirectory を持つ
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

- `subTreeJsonPath` フィールドに**親JSONからの相対パス**で参照JSONを指定（例: `"SubTrees/Patrol.json"`）
- 親ツリーの `Blackboard` を共有する
- `BaseDirectory` は Engine が初期化時に全ノードへ自動バインドする
- エディタ上でダブルクリックするとサブツリーの中を表示できる
- エディタのノード名には参照ファイル名（拡張子なし）が表示される

### BehaviourTreeEngine の BaseDirectory

サブツリーの相対パス解決のため、エンジンにベースディレクトリを渡す。

```csharp
// JSON ファイルのあるディレクトリを渡す
engine.Initialize(jsonText, baseDirectory);
```

`BehaviourTreeRunner` は `OnValidate()` で TextAsset のパスから自動計算して保持する。

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

### BehaviourTreeRunner の継承（リアルタイム用途）

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

### BehaviourTreeRunner の継承（ワンショット用途）

コマンドバトルのように「1ターンに1回だけAIを動かす」場合は `Update()` を無効化し、
任意のタイミングでループ実行する。

```csharp
public class MyTurnRunner : BehaviourTreeRunner
{
    protected override void SetupBlackboard(Blackboard blackboard)
    {
        // 初期値をセット
    }

    /// <summary>毎フレームの自動実行を無効化</summary>
    protected override void Update() { }

    /// <summary>ターン開始時に呼ぶ</summary>
    public async void ExecuteTurn()
    {
        Engine.Reset();
        NodeState state;
        do
        {
            state = await Engine.UpdateAsync(_cts.Token);
        }
        while (state == NodeState.Running && !_cts.Token.IsCancellationRequested);
    }
}
```

`BehaviourTreeRunner` を継承することで、Play中に Hierarchy で GameObject を選択すると
ModuTree Editor ウィンドウでノードの実行状態をリアルタイムに確認できる。

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
| `ModuTree.Samples` | BasicSample |
| `ModuTree.SubTreeSample` | SubTreeSample |
| `ModuTree.JankenSample` | JankenSample |

### MiniJson の enum シリアライズ

enum は**整数値**として保存・復元される。`parametersJson` に書く際も数値で指定する。

```json
{ "targetHand": 0 }   // JankenHand.Rock = 0
{ "hand": 2 }         // JankenHand.Paper = 2
```

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
- `positionX` / `positionY` はグリッドサイズ（20px）の倍数に自動スナップされる

---

## エディターの操作

| 操作 | 内容 |
|------|------|
| `Window > ModuTree > BehaviourTree Editor` | エディターウィンドウを開く |
| 右クリック（空白） | ノード追加メニュー（Rootがない場合はComposite系のみ） |
| 右クリック（接続ライン上） | 接続を解除 |
| 右クリック（ノード上） | 削除メニュー（単体または複数選択まとめて削除） |
| ノード中央をドラッグ | ノード移動（グリッドスナップ付き、左右位置が子の実行順に反映） |
| `Alt` + ドラッグ | キャンバスのパン（スライド移動） |
| ノードの上端/下端をドラッグ | 別ノードへの接続 |
| ノード単体クリック | 右パネル（Inspector）にパラメータを表示・編集 |
| 空白ドラッグ | ボックス選択（複数選択） |
| `⌘S` / `Ctrl+S` | Inspector の未適用変更を含めて保存 |
| 別名保存ボタン | 別ファイルとして保存（Variant作成に便利） |
| `⌘Z` / `Ctrl+Z` | Undo（ノード追加・削除・移動・接続変更／Inspector未適用変更の破棄） |
| `⌘C` / `⌘X` / `⌘V` | コピー / カット / ペースト（重なり回避付き） |
| スクロール | ズームイン/アウト |
| 背景をトリプルクリック | Rootノードを中心にズーム1.0でリセット |
| SubTreeノードをダブルクリック | サブツリーの中を表示 |
| Play中にHierarchyでRunnerを選択 | 実行中ノードをリアルタイムで視覚確認 |

### Inspector パネル（右側 280px）

- ノードのスクリプト参照（クリックで選択）
- パラメータのインライン編集
- SubTree ノードの参照ファイルは Project ビューからドラッグ＆ドロップでセット可能
- **「パラメータを適用」ボタン**（常に最下部に表示）を押すまでパラメータ変更はファイルに保存されない
  - 未適用変更がある場合、パラメータセクションのヘッダーがオレンジ色になりボタンが強調表示される
  - 別ノードを選択すると未適用変更は自動破棄される
  - `⌘S` / `Ctrl+S` でも適用＆保存できる

### 自動保存の挙動

| 操作 | 保存タイミング |
|------|--------------|
| ノード追加・削除・接続・切断 | 即時 |
| ノード移動 | ドロップ時（MouseUp） |
| パラメータ変更 | 「パラメータを適用」ボタン押下時 |
| Undo | 即時（巻き戻し後の状態を保存） |

Play中の編集も即時ファイルに保存され、実行中エンジンにホットリロードされる。

### グリッドスナップと衝突回避

- ノード移動はグリッド（20px）にスナップ
- ドロップ先に別ノードが同一グリッドセルにある場合、右下方向に1グリッドずつずらして自動配置
- ペースト時も同じルールで衝突回避される

---

## 注意事項・既知の問題

- **ReactiveSelectorNodeData** は低優先度のノードが実行中の場合のみ高優先度ノードをプローブする。高優先度自体が実行中の場合は再プローブしない
- **SequenceNodeData の Running 返却**: 子が Success を返した次の子はその同フレームでは実行されない。ワンショット実行ではループが必要（前述）
- ノード生成は `Activator.CreateInstance(type)` によるリフレクションを使用。`typeName` を変更すると既存JSONが壊れる
- `DecoratorNodeData` は抽象クラスのみ。組み込み実装は存在しないためユーザーが実装する
- `SubTreeNodeData` の `subTreeJsonPath` は親JSONファイルからの**相対パス**を指定する
- `[NodeFieldHide]` は `AttributeTargets.Field | Property` の両方に付与可能
- Delete/Backspace キーによるノード削除は無効。削除は右クリックメニューから行う
- Play中に `_runtimeEngine.Initialize()` を呼ぶと全ノードが Idle にリセットされる（リアルタイムAIの実行状態が途切れる）

---

## サンプル一覧

### BasicSample — リアルタイム敵AI

`Assets/Samples/ModuTree/BasicSample/EnemyBehaviourTree.json`

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

---

### SubTreeSample — サブツリーによる警備員AI

`Assets/Samples/ModuTree/SubTreeSample/AlertGuardBehaviourTree.json`

```
ReactiveSelectorNodeData（ルート）
├── SequenceNodeData
│   ├── IsPlayerSpottedCondition（detectRange: 5m）
│   └── SubTreeNodeData → SubTrees/ChaseUntilSafe.json
│         └── ChaseAndReturnAction（chaseSpeed: 3.5 / chaseRange: 10m）
└── SubTreeNodeData → SubTrees/Patrol.json
      └── RandomPatrolAction（patrolSpeed: 2.0 / arriveDistance: 0.5）
```

**ポイント:**
- サブツリーのパスは親JSONからの相対パス（`"SubTrees/Patrol.json"` など）
- 親の Blackboard をサブツリーと共有
- `SentryRunner` が `BehaviourTreeRunner` を継承し Blackboard 経由で移動を適用

**シーンセットアップ:**
1. Plane（地面）、Capsule×2（プレイヤー・警備員）を配置
2. プレイヤーに `SentryPlayerController` をアタッチ
3. 警備員に `SentryRunner` をアタッチ、`AlertGuardBehaviourTree.json` と Player を設定

---

### JankenSample — ワンショット実行（ジャンケンAI）

`Assets/Samples/ModuTree/JankenSample/JankenBehaviourTree.json`

```
SelectorNodeData（ルート）
├── SequenceNodeData
│   ├── IsPlayerChoseCondition（targetHand: Rock）
│   └── SelectHandAction（hand: Paper）   → グーにはパーで勝ち
├── SequenceNodeData
│   ├── IsPlayerChoseCondition（targetHand: Scissors）
│   └── SelectHandAction（hand: Rock）    → チョキにはグーで勝ち
└── SelectHandAction（hand: Scissors）    → パーにはチョキで勝ち（デフォルト）
```

**ポイント:**
- `JankenRunner` は `BehaviourTreeRunner` を継承し `Update()` を空でオーバーライド
- ボタン押下時に `Engine.Reset()` → Running でなくなるまで `UpdateAsync()` をループ
- Play中に Hierarchy で JankenRunner の GameObject を選択すると ModuTree Editor でノード遷移を確認できる

**シーンセットアップ:**
1. Canvas に Button×3（グー/チョキ/パー）、Text×2（ResultText/ThinkingText）を配置
2. 空の GameObject に `JankenRunner` をアタッチ
3. `JankenBehaviourTree.json`、各ボタン、各テキストを Inspector でセット