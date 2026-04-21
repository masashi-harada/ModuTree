using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEngine;

namespace ModuTree.Samples
{
    /// <summary>
    /// プレイヤーを追跡するアクションノード。
    /// 敵がパトロール範囲外に出た瞬間 Failure を返して追跡を終了する。
    /// それ以外は Running を返して追跡を継続する。
    ///
    /// 追跡開始後はプレイヤーが検出範囲外に出ても継続する仕様。
    /// 追跡終了条件は「敵がパトロール範囲外に出た時」のみ。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "プレイヤー追跡",
        category:    "Action",
        description: "プレイヤーへ移動。パトロール範囲外に出たら Failure を返す。",
        color:       "#1E4F8C")]
    public class ChasePlayerAction : ActionNodeData
    {
        [NodeField("追跡速度")]
        public float chaseSpeed = 4f;

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var agent  = Blackboard.Get(SampleBBKeys.AgentTransform);
            var player = Blackboard.Get(SampleBBKeys.PlayerTransform);
            var center = Blackboard.Get(SampleBBKeys.PatrolCenter);
            float radius = Blackboard.Get(SampleBBKeys.PatrolRadius);

            if (agent == null || player == null)
                return Task.FromResult(NodeState.Failure);

            // パトロール範囲外に出たら追跡を諦めてFailureを返す
            if (Vector3.Distance(agent.position, center) > radius)
                return Task.FromResult(NodeState.Failure);

            // プレイヤーへの方向をBlackboardに書き込む（Y軸無視）
            var dir = player.position - agent.position;
            dir.y = 0f;
            if (dir.magnitude > 0.01f)
            {
                Blackboard.Set(SampleBBKeys.MoveDirection, dir.normalized);
                Blackboard.Set(SampleBBKeys.MoveSpeed,     chaseSpeed);
            }

            return Task.FromResult(NodeState.Running);
        }
    }
}