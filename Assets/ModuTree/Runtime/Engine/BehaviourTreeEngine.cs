using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Serialization;

namespace ModuTree.Runtime.Engine
{
    /// <summary>
    /// BehaviourTreeを実行するエンジン。
    /// Pure C# 実装のため Unity 非依存。
    /// Unityでは毎フレームUpdateAsync()を呼び、
    /// サーバではTickループで呼ぶ。
    /// </summary>
    public class BehaviourTreeEngine
    {
        // ─── プロパティ ──────────────────────────────────────

        public Blackboard Blackboard { get; }
        public NodeState  State      { get; private set; } = NodeState.Idle;

        /// <summary>GUID→ノードインスタンスのマップ（エディタのモニタリング用）</summary>
        public IReadOnlyDictionary<string, BehaviourNodeData> NodeMap => _nodeMap;

        // ─── 内部状態 ────────────────────────────────────────

        private BehaviourNodeData                     _rootNode;
        private Dictionary<string, BehaviourNodeData> _nodeMap = new Dictionary<string, BehaviourNodeData>();
        private CancellationTokenSource               _cts;

        // ─── コンストラクタ ──────────────────────────────────

        /// <summary>新しいBlackboardを自動生成するコンストラクタ</summary>
        public BehaviourTreeEngine() => Blackboard = new Blackboard();

        /// <summary>既存のBlackboardを共有するコンストラクタ（サブツリー用）</summary>
        public BehaviourTreeEngine(Blackboard bb) => Blackboard = bb;

        // ─── 初期化 ──────────────────────────────────────────

        /// <summary>JSON文字列からツリーを初期化する</summary>
        public void Initialize(string json)
            => Initialize(BehaviourTreeSerializer.FromJson(json));

        /// <summary>BehaviourTreeDataからツリーを初期化する</summary>
        public void Initialize(BehaviourTreeData treeData)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _nodeMap.Clear();
            _rootNode = null;
            State     = NodeState.Idle;

            var (root, nodeMap) = BehaviourTreeSerializer.Build(treeData);
            _rootNode = root;
            _nodeMap  = new Dictionary<string, BehaviourNodeData>(nodeMap);

            BindBlackboard();

            // 全ノードをIdleに初期化
            _rootNode?.ResetState();

            if (_rootNode == null)
                Console.Error.WriteLine("[ModuTree] Root ノードが設定されていません");
        }

        // ─── 非同期実行（基本）──────────────────────────────

        /// <summary>
        /// 1ステップ非同期実行。
        /// Unityでは毎フレームawaitして呼ぶ。
        /// サーバではTickループで呼ぶ。
        /// </summary>
        public async Task<NodeState> UpdateAsync(CancellationToken ct = default)
        {
            if (_rootNode == null) return NodeState.Failure;

            using var linked = CancellationTokenSource
                .CreateLinkedTokenSource(ct, _cts.Token);

            if (_rootNode.State == NodeState.Running ||
                _rootNode.State == NodeState.Idle)
                State = await _rootNode.UpdateAsync(linked.Token);

            return State;
        }

        /// <summary>
        /// ツリーが完了するまで非同期で実行し続ける。
        /// tickIntervalはデフォルト16ms（約60fps相当）。
        /// </summary>
        public async Task<NodeState> RunToCompletionAsync(
            TimeSpan          tickInterval = default,
            CancellationToken ct           = default)
        {
            if (tickInterval == default)
                tickInterval = TimeSpan.FromMilliseconds(16);

            while (!ct.IsCancellationRequested)
            {
                var result = await UpdateAsync(ct);
                if (result != NodeState.Running) return result;
                await Task.Delay(tickInterval, ct);
            }

            ct.ThrowIfCancellationRequested();
            return NodeState.Failure;
        }

        /// <summary>実行中のツリーを強制中断する</summary>
        public async Task AbortAsync(CancellationToken ct = default)
        {
            if (_rootNode != null)
                await _rootNode.AbortAsync(ct);
            State = NodeState.Failure;
        }

        // ─── リセット ────────────────────────────────────────

        /// <summary>全ノードをIdleにリセットする</summary>
        public void Reset()
        {
            _rootNode?.ResetState();
            State = NodeState.Idle;
        }

        /// <summary>CancellationTokenSourceを破棄する</summary>
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ─── 内部処理 ────────────────────────────────────────

        /// <summary>全ノードにBlackboardをバインドする</summary>
        private void BindBlackboard()
        {
            foreach (var node in _nodeMap.Values)
                node.Blackboard = Blackboard;
        }
    }
}