using ModuTree.Runtime.Core;

namespace ModuTree.JankenSample
{
    /// <summary>ジャンケンの手を表す列挙型</summary>
    public enum JankenHand
    {
        Rock     = 0,  // グー
        Scissors = 1,  // チョキ
        Paper    = 2,  // パー
    }

    /// <summary>ジャンケンサンプル用 Blackboard キー定義</summary>
    public static class JankenBBKeys
    {
        /// <summary>プレイヤーが選んだ手</summary>
        public static readonly BlackboardKey<JankenHand> PlayerHand = new("playerHand");

        /// <summary>AIが選んだ手（ノードが書き込む）</summary>
        public static readonly BlackboardKey<JankenHand> AiHand = new("aiHand");

        /// <summary>AI思考中フラグ</summary>
        public static readonly BlackboardKey<bool> IsThinking = new("isThinking");
    }
}