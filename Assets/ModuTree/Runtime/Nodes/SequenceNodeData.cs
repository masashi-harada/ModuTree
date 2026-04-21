using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;

namespace ModuTree.Runtime.Nodes
{
    /// <summary>
    /// Sequenceコンポジットノード。
    /// 全ての子が成功すれば成功。
    /// 子がRunning中は同じ子を実行し続ける。
    /// 子がFailureを返した時点でFailureになる。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "Sequence",
        category:    "Composite",
        description: "全ての子が成功すれば成功。\n" +
                     "子がRunning中は同じ子を実行し続ける。\n" +
                     "子がFailureを返した時点でFailureになる。",
        color:       "#2D7A3A")]
    public class SequenceNodeData : CompositeNodeData
    {
        private int _current;

        protected override Task OnStartAsync(CancellationToken ct)
        {
            _current = 0;
            // 全子ノードをIdleにリセット
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
            if (_current >= Children.Count) return NodeState.Success;

            var result = await Children[_current].UpdateAsync(ct);

            switch (result)
            {
                case NodeState.Running:
                    return NodeState.Running;

                case NodeState.Success:
                    _current++;
                    // 次のノードはまだIdleのまま（実行されていない）
                    return _current >= Children.Count
                        ? NodeState.Success
                        : NodeState.Running;

                default:
                    return NodeState.Failure;
            }
        }
    }
}