using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEngine;

namespace ModuTree.SubTreeSample
{
    /// <summary>
    /// プレイヤーが detectRange 以内にいれば Success を返すコンディションノード。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "プレイヤー発見",
        category:    "Condition",
        description: "プレイヤーが指定範囲内にいれば Success を返す。",
        color:       "#7A5A1A")]
    public class IsPlayerSpottedCondition : ConditionNodeData
    {
        [NodeField("発見距離")]
        public float detectRange = 5f;

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var agent  = Blackboard.Get(SentryBBKeys.AgentTransform);
            var player = Blackboard.Get(SentryBBKeys.PlayerTransform);

            if (agent == null || player == null)
                return Task.FromResult(NodeState.Failure);

            float dist = Vector3.Distance(agent.position, player.position);
            return Task.FromResult(dist <= detectRange
                ? NodeState.Success
                : NodeState.Failure);
        }
    }
}