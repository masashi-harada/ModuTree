using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEngine;

namespace ModuTree.SubTreeSample
{
    /// <summary>
    /// 配置地点(PostPosition)を中心にランダムに巡回するアクションノード。
    /// 目標地点に到着するたびに次の地点をランダム選択する。
    /// ReactiveSelectorに割り込まれるまで常に Running を返す。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "ランダム巡回",
        category:    "Action",
        description: "配置地点周辺をランダムに歩き回る。",
        color:       "#2D7A3A")]
    public class RandomPatrolAction : ActionNodeData
    {
        [NodeField("巡回速度")]
        public float patrolSpeed = 2f;

        [NodeField("到着判定距離", "この距離以内に入ったら次の目標地点を選ぶ")]
        public float arriveDistance = 0.5f;

        private Vector3 _target;
        private bool    _hasTarget;

        protected override Task OnStartAsync(CancellationToken ct)
        {
            // 再開時は目標をリセット
            _hasTarget = false;
            return Task.CompletedTask;
        }

        protected override Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            var agent  = Blackboard.Get(SentryBBKeys.AgentTransform);
            var center = Blackboard.Get(SentryBBKeys.PostPosition);
            float radius = Blackboard.Get(SentryBBKeys.PatrolRadius);

            if (agent == null)
                return Task.FromResult(NodeState.Failure);

            // 目標未設定または到着済み → 新しい地点を選ぶ
            if (!_hasTarget || Vector3.Distance(agent.position, _target) <= arriveDistance)
            {
                _target    = PickRandomTarget(center, radius);
                _hasTarget = true;
            }

            var dir = _target - agent.position;
            dir.y = 0f;
            if (dir.magnitude > 0.01f)
            {
                Blackboard.Set(SentryBBKeys.MoveDirection, dir.normalized);
                Blackboard.Set(SentryBBKeys.MoveSpeed,     patrolSpeed);
            }

            return Task.FromResult(NodeState.Running);
        }

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