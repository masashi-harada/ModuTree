using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEngine;

namespace ModuTree.Samples
{
    /// <summary>
    /// プレイヤーが検出距離内にいるか判定する条件ノード。
    /// Successなら上位のSequenceが追跡アクションへ進む。
    /// パトロール中はReactiveSelectorに毎フレームプローブされる。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "プレイヤー検出",
        category:    "Condition",
        description: "プレイヤーが detectRange 以内にいれば Success。",
        color:       "#8B6914")]
    public class IsPlayerDetectedCondition : ConditionNodeData
    {
        [NodeField("検出距離", "この距離以内にプレイヤーが入ったら追跡開始")]
        public float detectRange = 5f;

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var agent  = Blackboard.Get(SampleBBKeys.AgentTransform);
            var player = Blackboard.Get(SampleBBKeys.PlayerTransform);

            if (agent == null || player == null)
                return Task.FromResult(NodeState.Failure);

            float dist = Vector3.Distance(agent.position, player.position);
            return Task.FromResult(
                dist <= detectRange ? NodeState.Success : NodeState.Failure);
        }
    }
}