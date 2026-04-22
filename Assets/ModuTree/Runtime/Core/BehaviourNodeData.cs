using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModuTree.Runtime.Core
{
    /// <summary>
    /// 全ノードの抽象基底クラス。
    /// Pure C# 実装のため Unity 非依存。
    /// </summary>
    public abstract class BehaviourNodeData
    {
        /// <summary>ノードの一意識別子（JSONから復元）</summary>
        [NodeFieldHide] public string Guid { get; internal set; }

        /// <summary>現在の実行状態</summary>
        [NodeFieldHide] public NodeState State { get; internal set; } = NodeState.Idle;

        /// <summary>OnStartAsyncが呼ばれた後にtrueになる</summary>
        [NodeFieldHide] public bool Started { get; private set; } = false;

        /// <summary>共有Blackboard（エンジンから注入）</summary>
        [NodeFieldHide] public Blackboard Blackboard { get; internal set; }

        /// <summary>
        /// このノードが属するツリーJSONのベースディレクトリ（相対パス解決用）。
        /// エンジンから注入される。
        /// </summary>
        [NodeFieldHide] internal string BaseDirectory { get; set; } = "";

        // ─── 非同期実行API（基本）────────────────────────────

        /// <summary>
        /// 非同期でノードを1ステップ実行する。
        /// 全ての実行はこちらが基本。
        /// Running中は同ノードが実行され続ける。
        /// </summary>
        public async Task<NodeState> UpdateAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!Started)
            {
                State   = NodeState.Running;
                Started = true;
                await OnStartAsync(ct);
            }

            State = await OnUpdateAsync(ct);

            if (State != NodeState.Running)
            {
                await OnStopAsync(ct);
                Started = false;
            }

            return State;
        }

        /// <summary>強制中断する</summary>
        public async Task AbortAsync(CancellationToken ct = default)
        {
            if (!Started) return;
            await OnAbortAsync(ct);
            await OnStopAsync(ct);
            State   = NodeState.Failure;
            Started = false;
        }

        // ─── 継承API（非同期）───────────────────────────────

        /// <summary>ノード開始時に1度だけ呼ばれる</summary>
        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>毎フレーム呼ばれる主処理</summary>
        protected abstract Task<NodeState> OnUpdateAsync(CancellationToken ct);

        /// <summary>ノード終了時（成功/失敗問わず）に呼ばれる</summary>
        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>強制中断時にOnStopAsyncの前に呼ばれる</summary>
        protected virtual Task OnAbortAsync(CancellationToken ct) => Task.CompletedTask;

        // ─── 子ノード ────────────────────────────────────────

        /// <summary>子ノードリストを返す（デフォルトは空リスト）</summary>
        public virtual List<BehaviourNodeData> GetChildren() => new List<BehaviourNodeData>();

        /// <summary>自身と全子孫ノードのStateをIdleにリセット</summary>
        public void ResetState()
        {
            State   = NodeState.Idle;
            Started = false;
            foreach (var child in GetChildren())
                child.ResetState();
        }
    }
}