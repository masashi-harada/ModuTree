using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;

namespace ModuTree.JankenSample
{
    /// <summary>
    /// プレイヤーが指定の手を選んでいるかチェックするコンディション。
    /// targetHand と Blackboard の playerHand が一致すれば Success を返す。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "プレイヤーの手チェック",
        category:    "Condition",
        description: "プレイヤーが targetHand を選んでいれば Success",
        color:       "#2E5E2E")]
    public class IsPlayerChoseCondition : ConditionNodeData
    {
        [NodeField("対象の手", "この手を選んでいれば Success")]
        public JankenHand targetHand = JankenHand.Rock;

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var playerHand = Blackboard.Get(JankenBBKeys.PlayerHand);
            var result = playerHand == targetHand ? NodeState.Success : NodeState.Failure;
            return Task.FromResult(result);
        }
    }
}