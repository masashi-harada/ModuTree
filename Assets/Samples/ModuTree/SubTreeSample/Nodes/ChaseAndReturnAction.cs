using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEngine;

namespace ModuTree.SubTreeSample
{
    /// <summary>
    /// プレイヤーを追跡するアクションノード。
    /// セントリーが配置地点(PostPosition)から chaseRange を超えたら
    /// Failure を返して追跡を諦め、巡回に戻る。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "追跡して帰還",
        category:    "Action",
        description: "プレイヤーを追跡する。配置地点から一定距離を超えたら Failure を返す。",
        color:       "#7A1A1A")]
    public class ChaseAndReturnAction : ActionNodeData
    {
        [NodeField("追跡速度")]
        public float chaseSpeed = 3.5f;

        [NodeField("追跡限界距離", "配置地点からこの距離を超えたら追跡を諦める")]
        public float chaseRange = 10f;

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var agent  = Blackboard.Get(SentryBBKeys.AgentTransform);
            var player = Blackboard.Get(SentryBBKeys.PlayerTransform);
            var post   = Blackboard.Get(SentryBBKeys.PostPosition);

            if (agent == null || player == null)
                return Task.FromResult(NodeState.Failure);

            // 配置地点から限界距離を超えたら追跡終了
            if (Vector3.Distance(agent.position, post) > chaseRange)
                return Task.FromResult(NodeState.Failure);

            var dir = player.position - agent.position;
            dir.y = 0f;
            if (dir.magnitude > 0.01f)
            {
                Blackboard.Set(SentryBBKeys.MoveDirection, dir.normalized);
                Blackboard.Set(SentryBBKeys.MoveSpeed,     chaseSpeed);
            }

            return Task.FromResult(NodeState.Running);
        }
    }
}