using ModuTree.Runtime.Core;
using ModuTree.UnityIntegration;
using UnityEngine;

namespace ModuTree.Samples
{
    /// <summary>
    /// サンプル敵AIのRunner。
    /// BehaviourTreeRunner を継承し、Blackboardの初期化と
    /// BehaviourTreeの判断結果の適用を担う。
    ///
    /// ノード内でTransformを直接操作せず、Blackboardを介してRunnerに渡す。
    /// </summary>
    public class SampleEnemyRunner : BehaviourTreeRunner
    {
        [Header("参照")]
        public Transform player;

        [Header("設定")]
        public float patrolRadius = 8f;

        // ─── Blackboard初期化 ────────────────────────────────

        protected override void SetupBlackboard(Blackboard blackboard)
        {
            blackboard.Set(SampleBBKeys.AgentTransform,  transform);
            blackboard.Set(SampleBBKeys.PlayerTransform, player);
            // パトロール中心は起動時の自分の位置
            blackboard.Set(SampleBBKeys.PatrolCenter,    transform.position);
            blackboard.Set(SampleBBKeys.PatrolRadius,    patrolRadius);
        }

        // ─── 毎フレーム更新 ──────────────────────────────────

        protected override async void Update()
        {
            if (Engine == null) return;

            // BehaviourTreeを1ステップ実行
            await Engine.UpdateAsync(_cts.Token);

            // 判断結果を実際の移動に適用
            ApplyMovement();
        }

        // ─── 移動適用 ────────────────────────────────────────

        private void ApplyMovement()
        {
            var dir   = Engine.Blackboard.Get(SampleBBKeys.MoveDirection);
            float speed = Engine.Blackboard.Get(SampleBBKeys.MoveSpeed);

            if (dir != Vector3.zero && speed > 0f)
            {
                transform.position += dir * speed * Time.deltaTime;

                // 移動方向に向く
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 10f);
            }

            // 毎フレームリセット（ノードが毎フレーム書き込む）
            Engine.Blackboard.Set(SampleBBKeys.MoveDirection, Vector3.zero);
            Engine.Blackboard.Set(SampleBBKeys.MoveSpeed,     0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // パトロール範囲（緑）
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);

            // 検出範囲（黄）※ IsPlayerDetectedCondition の detectRange と合わせること
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, 5f);
        }
#endif
    }
}