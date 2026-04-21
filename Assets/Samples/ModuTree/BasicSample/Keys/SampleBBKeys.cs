using ModuTree.Runtime.Core;
using UnityEngine;

namespace ModuTree.Samples
{
    /// <summary>
    /// サンプルで使用するBlackboardキー定義。
    /// 型安全なBlackboardKey<T>を使うことでキーのタイプミスをコンパイル時に検出できる。
    /// </summary>
    public static class SampleBBKeys
    {
        // 敵自身のTransform
        public static readonly BlackboardKey<Transform> AgentTransform
            = new("agentTransform");

        // プレイヤーのTransform
        public static readonly BlackboardKey<Transform> PlayerTransform
            = new("playerTransform");

        // パトロール中心座標（敵の初期位置）
        public static readonly BlackboardKey<Vector3> PatrolCenter
            = new("patrolCenter");

        // パトロール半径
        public static readonly BlackboardKey<float> PatrolRadius
            = new("patrolRadius");

        // BT → Runner 出力: 移動方向（毎フレームRunnerがゼロにリセット）
        public static readonly BlackboardKey<Vector3> MoveDirection
            = new("moveDirection");

        // BT → Runner 出力: 移動速度
        public static readonly BlackboardKey<float> MoveSpeed
            = new("moveSpeed");
    }
}