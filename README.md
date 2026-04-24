# ModuTree

Design your game AI visually. Run it anywhere — C#, PHP, and beyond.  
A modular Behaviour Tree editor for Unity with cross-platform runtimes.

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)
![PHP](https://img.shields.io/badge/PHP-8.1%2B-777BB4?logo=php&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-blue)

---

## ModuTree とは

**「AIのロジックを、グラフで描く。どこでも動かす。」**

ModuTree は、Unity向けのビジュアル Behaviour Tree エディタ＆マルチプラットフォーム・ランタイムシステムです。

従来の Behaviour Tree 実装では、AIロジックが Unity の MonoBehaviour やシーンに強く結びついてしまい、「設計の見通しが悪い」「サーバーや他の環境では動かない」という問題がありました。
ModuTree はこの課題を、**エディタ・データ・ランタイムの3層分離**という設計で解決します。

- **グラフィカルなAIデザイン** — ノードをつなぐだけで AI の行動ロジックを視覚的に組み立てられます。コードを書かずにツリーを構築・調整でき、企画者とエンジニアが同じ画面で議論できます。
- **クロスプラットフォーム実行** — AIデータは JSON ファイルとして保存されます。**Pure C# Runtime**（Unity 非依存のサーバー・CLI 向け）と **PHP 8.1+ Runtime**（Web API 向け）の両方が付属しており、Unity で作ったツリーをそのまま別環境で動かせます。
- **非同期駆動で直感的な `Running` 表現** — `async / await` を活用することで、「処理中」という状態を自然なコードで表現できます。フレームをまたぐ複雑な行動も、シンプルに記述できます。
- **高速な実装サイクル** — ノード単位でロジックをカプセル化し、Blackboard でデータを共有する設計により、AIの追加・変更・テストが素早く行えます。Play 中にツリーを編集するとその場で動作に反映される**ホットリロード**対応です。
- **Blackboard によるロジック分離** — AIの「判断」と「実行」を Blackboard 経由で疎結合に保ちます。ノード内では Unity API を呼ばず判断結果を書き込むだけ。テストしやすく、移植もしやすい設計です。

ゲームAIの実装を、**速く・見やすく・どこでも動く**ものにするのが ModuTree のコンセプトです。

---

## Features

- **Visual node editor** — drag-and-drop graph editor built on Unity IMGUI
- **Pure C# runtime** — `Runtime/CSharp/` has no Unity dependencies (`noEngineReferences: true`), so it can run on a server too
- **PHP 8.1+ runtime** — `Runtime/PHP~/` provides a synchronous PHP port; use the same JSON files from Unity in a Web API
- **Async-driven** — `Running` state expressed naturally with `async Task` (C#) / synchronous loop (PHP)
- **JSON persistence** — no ScriptableObjects; zero external dependencies via MiniJson (C#) / `json_decode` (PHP)
- **Blackboard** — type-safe key/value store decoupling AI logic from execution
- **Hot reload** — swap JSON at runtime and the tree reloads instantly
- **Play-mode editing** — edit the tree while the game is running; changes save and hot-reload immediately
- **Real-time debug view** — select a `BehaviourTreeRunner` GameObject in the Hierarchy during Play to see node states highlighted in the editor

---

## Built-in Node Types

| Type | Class | Description |
|------|-------|-------------|
| Sequence | `SequenceNodeData` | Runs children left-to-right; succeeds only if all succeed (AND) |
| Selector | `SelectorNodeData` | Runs children left-to-right; succeeds as soon as one succeeds (OR) |
| Reactive Selector | `ReactiveSelectorNodeData` | Re-evaluates from the first child every tick; enables priority interrupts |
| Sub-Tree | `SubTreeNodeData` | References an external JSON file; shares the parent Blackboard |

`ActionNodeData`, `ConditionNodeData`, and `DecoratorNodeData` are abstract base classes for your own nodes.

---

## Getting Started

### Installation (Unity)

Copy the `Assets/ModuTree/` folder into your Unity project. That's it — no package manager required.

### Installation (PHP)

Copy `Assets/ModuTree/Runtime/PHP~/` to your server as `Runtime/`, then `require_once` the autoloader:

```php
require_once 'Runtime/autoload.php';
```

### Writing a Custom Node

```csharp
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using System.Threading;
using System.Threading.Tasks;

[BehaviourNodeMeta(
    displayName: "My Action",
    category:    "Action",
    description: "Does something useful",
    color:       "#1E3A6E")]
public class MyAction : ActionNodeData
{
    [NodeField("Speed", "Movement speed")]
    public float speed = 2f;

    protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
    {
        var agent = Blackboard.Get(MyBBKeys.Agent);
        // ... do work ...
        return Task.FromResult(NodeState.Success);
    }
}
```

`[BehaviourNodeMeta]` is required — without it the node won't appear in the editor.

### Defining Blackboard Keys

```csharp
public static class MyBBKeys
{
    public static readonly BlackboardKey<Transform> Agent    = new("agent");
    public static readonly BlackboardKey<float>     Distance = new("distance");
}
```

### Creating a Runner (real-time)

```csharp
public class EnemyRunner : BehaviourTreeRunner
{
    public Transform player;

    protected override void SetupBlackboard(Blackboard blackboard)
    {
        blackboard.Set(MyBBKeys.Agent,  transform);
        blackboard.Set(MyBBKeys.Player, player);
    }

    protected override async void Update()
    {
        if (Engine == null) return;
        await Engine.UpdateAsync(_cts.Token);
        ApplyMovement(); // read Blackboard and move the character
    }
}
```

### Creating a Runner (one-shot / turn-based)

```csharp
public class TurnRunner : BehaviourTreeRunner
{
    protected override void Update() { } // disable auto-run

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

### Using the PHP Runtime (Web API)

Design your tree in Unity, export the JSON, then run it from PHP:

```php
<?php
require_once 'Runtime/autoload.php';
require_once 'Keys/MyBBKeys.php';
require_once 'Nodes/MyConditionNode.php';
require_once 'Nodes/MyActionNode.php';

$engine = new BehaviourTreeEngine();
$engine->blackboard->set(MyBBKeys::$playerInput, $requestData['input']);

$jsonPath = 'AIData/MyTree.json';
$engine->initialize(file_get_contents($jsonPath), dirname($jsonPath));

// Runs synchronously until Success or Failure
$state = $engine->runToCompletion();
$result = $engine->blackboard->get(MyBBKeys::$aiDecision);

echo json_encode(['result' => $result]);
```

PHP class names must match the C# class names (e.g. `MyConditionNode`) — the serializer resolves them automatically. For `enum`, use PHP 8.1 int-backed enums with the same integer values as C#:

```php
enum JankenHand: int {
    case Rock     = 0;
    case Scissors = 1;
    case Paper    = 2;
}
```

---

## Editor

Open via **Window > ModuTree > BehaviourTree Editor**.

| Action | Result |
|--------|--------|
| Right-click canvas | Add node menu |
| Right-click node | Delete menu |
| Right-click connection line | Remove connection |
| Drag node center | Move (snaps to 20 px grid; auto-avoids occupied cells) |
| Drag node top/bottom edge | Connect to another node |
| Click node | Show parameters in Inspector panel |
| Drag canvas | Box-select multiple nodes |
| `Alt` + drag canvas | Pan |
| Scroll | Zoom in/out |
| Triple-click background | Reset zoom to 1.0, center on root |
| Double-click SubTree node | Navigate into sub-tree |
| `⌘S` / `Ctrl+S` | Save (applies pending Inspector changes too) |
| `⌘Z` / `Ctrl+Z` | Undo |
| `⌘C` / `⌘X` / `⌘V` | Copy / Cut / Paste (collision-aware) |

### Inspector Panel

- Inline parameter editing for the selected node
- **"Apply Parameters" button** at the bottom — parameter changes are not saved until this is clicked (or `⌘S` is pressed)
- Unsaved changes are highlighted in orange; switching to another node discards them automatically
- SubTree node's JSON path can be set by dragging a `.json` asset from the Project view

### Auto-save Behaviour

| Action | When saved |
|--------|-----------|
| Add / delete node | Immediately |
| Connect / disconnect | Immediately |
| Move node | On drop (MouseUp) |
| Parameter change | On "Apply Parameters" click |
| Undo | Immediately |

Play-mode edits save to disk and hot-reload the running engine instantly.

---

## JSON Format (v2.0.0)

```json
{
  "version": "2.0.0",
  "rootGuid": "<guid>",
  "nodes": [
    {
      "guid": "<guid>",
      "typeName": "ModuTree.Runtime.Nodes.SequenceNodeData, ModuTree.Runtime",
      "positionX": 0.0,
      "positionY": 0.0,
      "childrenGuids": ["<child-guid>"],
      "childGuid": null,
      "parametersJson": "{\"speed\":2.0}"
    }
  ]
}
```

- `typeName` — `FullTypeName, AssemblyName` (no version suffix needed)
- `childrenGuids` — for `CompositeNodeData`; left-to-right order = execution order
- `childGuid` — for `DecoratorNodeData`
- `parametersJson` — public fields without `[NodeFieldHide]`, serialized by MiniJson
- `positionX` / `positionY` — automatically snapped to the 20 px grid

---

## Samples

### BasicSample — Real-time Enemy AI

Enemy that patrols randomly and chases the player when detected.

```
ReactiveSelectorNodeData (root)
├── SequenceNodeData
│   ├── IsPlayerDetectedCondition  (detect range: 5 m)
│   └── ChasePlayerAction          (speed: 4 m/s, gives up outside patrol range)
└── PatrolAction                   (speed: 2 m/s, random waypoints)
```

**Scene setup:**
1. Add a Plane, two Capsules (player / enemy)
2. Attach `SamplePlayerController` to the player
3. Attach `SampleEnemyRunner` to the enemy; assign `EnemyBehaviourTree.json` and the Player reference
4. Position the camera at `(0, 15, 0)` with rotation `(90, 0, 0)` for a top-down view

---

### SubTreeSample — Guard AI with Sub-trees

Demonstrates splitting behaviour across multiple JSON files.

```
ReactiveSelectorNodeData (root)            AlertGuardBehaviourTree.json
├── SequenceNodeData
│   ├── IsPlayerSpottedCondition (5 m)
│   └── SubTreeNodeData ──────────────────→ SubTrees/ChaseUntilSafe.json
└── SubTreeNodeData ──────────────────────→ SubTrees/Patrol.json
```

Sub-trees share the parent Blackboard. Paths are relative to the parent JSON file.

---

### JankenSample — One-shot Execution (Rock-Paper-Scissors)

AI that always beats the player; demonstrates one-shot BT execution and the debug view.

```
SelectorNodeData (root)
├── Sequence: IsPlayerChoseCondition(Rock)     → SelectHandAction(Paper)
├── Sequence: IsPlayerChoseCondition(Scissors) → SelectHandAction(Rock)
└── SelectHandAction(Scissors)                 (default: beats Paper)
```

`JankenRunner` disables the auto-`Update()` loop and runs the tree to completion on each button press. Select the GameObject in the Hierarchy during Play to watch the node execution in the ModuTree Editor.

A PHP version is included at `Samples/ModuTree/JankenSample/PHP/`. Run it from the **project root** with PHP CLI to verify the runtime against the same JSON:

```bash
# Rock (0) → AI plays Paper (2) → player loses
php -r "\$_GET['hand']=0; ob_start(); include 'Assets/Samples/ModuTree/JankenSample/PHP/janken_api.php'; echo ob_get_clean();"

# Scissors (1) → AI plays Rock (0) → player loses
php -r "\$_GET['hand']=1; ob_start(); include 'Assets/Samples/ModuTree/JankenSample/PHP/janken_api.php'; echo ob_get_clean();"

# Paper (2) → AI plays Scissors (1) → player loses
php -r "\$_GET['hand']=2; ob_start(); include 'Assets/Samples/ModuTree/JankenSample/PHP/janken_api.php'; echo ob_get_clean();"
```

Expected response (Rock):
```json
{"playerHand":0,"playerHandLabel":"グー","aiHand":2,"aiHandLabel":"パー","result":"lose"}
```

---

## Project Structure

```
Assets/
├── ModuTree/
│   ├── Runtime/
│   │   ├── CSharp/        # Pure C# runtime (no Unity references) — copy this for C# servers
│   │   └── PHP~/          # PHP 8.1+ runtime — copy this for PHP servers (~ = Unity ignores it)
│   ├── UnityIntegration/  # BehaviourTreeRunner (MonoBehaviour)
│   └── Editor/            # IMGUI editor window
└── Samples/
    └── ModuTree/
        ├── BasicSample/
        ├── SubTreeSample/
        └── JankenSample/
            └── PHP/       # PHP version of the Janken sample (janken_api.php)
```

---

## License

MIT