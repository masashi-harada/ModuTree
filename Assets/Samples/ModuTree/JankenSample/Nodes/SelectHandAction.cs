using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;

namespace ModuTree.JankenSample
{
    /// <summary>
    /// AIが出す手を Blackboard に書き込むアクション。
    /// hand フィールドで指定した手を aiHand キーにセットして Success を返す。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "手を選択",
        category:    "Action",
        description: "指定した hand を aiHand に書き込んで Success",
        color:       "#1E3A6E")]
    public class SelectHandAction : ActionNodeData
    {
        [NodeField("選ぶ手", "AIが出す手")]
        public JankenHand hand = JankenHand.Rock;

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            Blackboard.Set(JankenBBKeys.AiHand, hand);
            return Task.FromResult(NodeState.Success);
        }
    }
}