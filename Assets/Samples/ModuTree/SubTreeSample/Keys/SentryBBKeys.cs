using ModuTree.Runtime.Core;
using UnityEngine;

namespace ModuTree.SubTreeSample
{
    /// <summary>
    /// AlertGuardサンプル用のBlackboardキー定義。
    /// </summary>
    public static class SentryBBKeys
    {
        /// <summary>セントリー自身のTransform</summary>
        public static readonly BlackboardKey<Transform> AgentTransform  = new("agentTransform");

        /// <summary>プレイヤーのTransform</summary>
        public static readonly BlackboardKey<Transform> PlayerTransform = new("playerTransform");

        /// <summary>セントリーの初期配置位置（巡回の中心）</summary>
        public static readonly BlackboardKey<Vector3>   PostPosition    = new("postPosition");

        /// <summary>巡回半径</summary>
        public static readonly BlackboardKey<float>     PatrolRadius    = new("patrolRadius");

        /// <summary>今フレームの移動方向（Runnerが毎フレーム読み取り後にリセット）</summary>
        public static readonly BlackboardKey<Vector3>   MoveDirection   = new("moveDirection");

        /// <summary>今フレームの移動速度（Runnerが毎フレーム読み取り後にリセット）</summary>
        public static readonly BlackboardKey<float>     MoveSpeed       = new("moveSpeed");
    }
}