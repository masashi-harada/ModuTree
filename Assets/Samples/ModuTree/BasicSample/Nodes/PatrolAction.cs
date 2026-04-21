using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEngine;

namespace ModuTree.Samples
{
    /// <summary>
    /// パトロール範囲内をランダムに歩き回るアクションノード。
    /// 目標地点に到着したら次のランダム地点を選ぶ。
    /// 常に Running を返す（ReactiveSelectorに割り込まれるまで継続）。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "パトロール",
        category:    "Action",
        description: "パトロール範囲内をランダムに移動し続ける。",
        color:       "#2D7A3A")]
    public class PatrolAction : ActionNodeData
    {
        [NodeField("移動速度")]
        public float patrolSpeed = 2f;

        [NodeField("到着判定距離", "この距離以内に入ったら次の目標地点を選ぶ")]
        public float arriveDistance = 0.5f;

        private Vector3 _target;
        private bool    _hasTarget;

        protected override Task OnStartAsync(CancellationToken ct)
        {
            // 再開時は目標をリセットして新しい地点を選ばせる
            _hasTarget = false;
            return Task.CompletedTask;
        }

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var agent  = Blackboard.Get(SampleBBKeys.AgentTransform);
            var center = Blackboard.Get(SampleBBKeys.PatrolCenter);
            float radius = Blackboard.Get(SampleBBKeys.PatrolRadius);

            if (agent == null)
                return Task.FromResult(NodeState.Failure);

            // 目標未設定または到着済み → 新しい目標地点を選ぶ
            if (!_hasTarget || Vector3.Distance(agent.position, _target) <= arriveDistance)
            {
                _target    = PickRandomTarget(center, radius);
                _hasTarget = true;
            }

            // 目標方向をBlackboardに書き込む（Y軸無視）
            var dir = _target - agent.position;
            dir.y = 0f;
            if (dir.magnitude > 0.01f)
            {
                Blackboard.Set(SampleBBKeys.MoveDirection, dir.normalized);
                Blackboard.Set(SampleBBKeys.MoveSpeed,     patrolSpeed);
            }

            return Task.FromResult(NodeState.Running);
        }

        /// <summary>パトロール範囲内のランダムな地点を返す</summary>
        private static Vector3 PickRandomTarget(Vector3 center, float radius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist  = Random.Range(0f, radius);
            return center + new Vector3(
                Mathf.Cos(angle) * dist,
                0f,
                Mathf.Sin(angle) * dist);
        }
    }
}