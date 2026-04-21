namespace ModuTree.Runtime.Core
{
    /// <summary>ノードの実行状態</summary>
    public enum NodeState
    {
        /// <summary>未実行の初期状態</summary>
        Idle,
        /// <summary>実行中（次フレームも実行継続）</summary>
        Running,
        /// <summary>成功終了</summary>
        Success,
        /// <summary>失敗終了</summary>
        Failure
    }
}