using ModuTree.Runtime.Core;
using ModuTree.UnityIntegration;
using UnityEngine;

namespace ModuTree.SubTreeSample
{
    /// <summary>
    /// AlertGuardサンプル用のRunner。
    /// BehaviourTreeRunnerを継承し、Blackboardの初期化と移動適用を担う。
    ///
    /// ツリー構造（SubTreeを使った分割の例）:
    ///   AlertGuardBehaviourTree.json
    ///   └── ReactiveSelectorNodeData
    ///       ├── Sequence → SubTree(ChaseUntilSafe.json)  ← プレイヤー発見時
    ///       └── SubTree(Patrol.json)                      ← 通常巡回
    /// </summary>
    public class SentryRunner : BehaviourTreeRunner
    {
        [Header("参照")]
        public Transform player;

        [Header("設定")]
        public float patrolRadius = 6f;

        // ─── Blackboard初期化 ────────────────────────────────

        protected override void SetupBlackboard(Blackboard blackboard)
        {
            blackboard.Set(SentryBBKeys.AgentTransform,  transform);
            blackboard.Set(SentryBBKeys.PlayerTransform, player);
            // 配置地点は起動時の自分の位置
            blackboard.Set(SentryBBKeys.PostPosition,    transform.position);
            blackboard.Set(SentryBBKeys.PatrolRadius,    patrolRadius);
        }

        // ─── 毎フレーム更新 ──────────────────────────────────

        protected override async void Update()
        {
            if (Engine == null) return;

            await Engine.UpdateAsync(_cts.Token);
            ApplyMovement();
        }

        // ─── 移動適用 ────────────────────────────────────────

        private void ApplyMovement()
        {
            var dir   = Engine.Blackboard.Get(SentryBBKeys.MoveDirection);
            float speed = Engine.Blackboard.Get(SentryBBKeys.MoveSpeed);

            if (dir != Vector3.zero && speed > 0f)
            {
                transform.position += dir * speed * Time.deltaTime;

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 10f);
            }

            // 毎フレームリセット
            Engine.Blackboard.Set(SentryBBKeys.MoveDirection, Vector3.zero);
            Engine.Blackboard.Set(SentryBBKeys.MoveSpeed,     0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 巡回範囲（緑）
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);

            // 追跡限界（赤）※ ChaseAndReturnAction の chaseRange と合わせること
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, 10f);

            // 発見範囲（黄）※ IsPlayerSpottedCondition の detectRange と合わせること
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, 5f);
        }
#endif
    }
}