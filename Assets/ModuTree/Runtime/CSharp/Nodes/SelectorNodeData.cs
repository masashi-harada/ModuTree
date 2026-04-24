using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;

namespace ModuTree.Runtime.Nodes
{
    /// <summary>
    /// Selectorコンポジットノード。
    /// いずれかの子が成功すれば成功。
    /// 子がRunning中は同じ子を実行し続ける。
    /// 全ての子がFailureならFailureになる。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "Selector",
        category:    "Composite",
        description: "いずれかの子が成功すれば成功。\n" +
                     "子がRunning中は同じ子を実行し続ける。\n" +
                     "全ての子がFailureならFailureになる。",
        color:       "#7A4F2D")]
    public class SelectorNodeData : CompositeNodeData
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
            if (_current >= Children.Count) return NodeState.Failure;

            var result = await Children[_current].UpdateAsync(ct);

            switch (result)
            {
                case NodeState.Running:
                    return NodeState.Running;

                case NodeState.Success:
                    return NodeState.Success;

                default:
                    _current++;
                    // 次のノードはまだIdleのまま
                    return _current >= Children.Count
                        ? NodeState.Failure
                        : NodeState.Running;
            }
        }
    }
}