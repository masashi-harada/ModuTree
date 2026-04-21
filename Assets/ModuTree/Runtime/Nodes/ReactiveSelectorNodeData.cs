using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;

namespace ModuTree.Runtime.Nodes
{
    /// <summary>
    /// ReactiveSelectorコンポジットノード。
    /// 毎フレーム先頭の子から再評価するSelector。
    /// 優先度の高い行動が割り込めるようになる。
    /// 例: 巡回中でもプレイヤーを検出したら即座に追跡に切り替わる。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "Reactive Selector",
        category:    "Composite",
        description: "毎フレーム先頭の子から再評価する Selector。\n" +
                     "優先度の高い行動が割り込めるようになる。\n" +
                     "例: 巡回中でもプレイヤーを検出したら即座に追跡に切り替わる。",
        color:       "#7A3A2D")]
    public class ReactiveSelectorNodeData : CompositeNodeData
    {
        private int _current;

        protected override Task OnStartAsync(CancellationToken ct)
        {
            _current = 0;
            foreach (var child in Children)
                child.ResetState();
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken ct)
            => Task.CompletedTask;

        protected override async Task OnAbortAsync(CancellationToken ct)
        {
            if (_current < Children.Count)
                await Children[_current].AbortAsync(ct);
        }

        protected override async Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            if (Children.Count == 0) return NodeState.Failure;

            // 毎フレーム先頭から再評価する
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                // 現在実行中のノードより優先度が高いノードが
                // 実行可能になったら現在のノードを中断する
                if (i < _current)
                {
                    // 優先度の高いノードを試し実行（1フレームだけ評価）
                    // Idleにリセットしてから評価する
                    child.ResetState();
                    var probe = await child.UpdateAsync(ct);

                    if (probe == NodeState.Success)
                    {
                        // 現在実行中のノードを中断
                        if (_current < Children.Count)
                            await Children[_current].AbortAsync(ct);

                        _current = i;
                        return NodeState.Success;
                    }
                    else if (probe == NodeState.Running)
                    {
                        // より優先度の高いノードに切り替え
                        if (_current != i && _current < Children.Count)
                            await Children[_current].AbortAsync(ct);

                        _current = i;
                        return NodeState.Running;
                    }
                    // Failureなら次のノードへ
                    child.ResetState();
                    continue;
                }

                // 現在のインデックス以降は通常のSelector処理
                var result = await child.UpdateAsync(ct);

                switch (result)
                {
                    case NodeState.Running:
                        _current = i;
                        return NodeState.Running;

                    case NodeState.Success:
                        _current = i;
                        return NodeState.Success;

                    case NodeState.Failure:
                        child.ResetState();
                        continue;
                }
            }

            return NodeState.Failure;
        }
    }
}