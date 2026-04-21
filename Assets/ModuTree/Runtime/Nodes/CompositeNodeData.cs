using System.Collections.Generic;
using ModuTree.Runtime.Core;

namespace ModuTree.Runtime.Nodes
{
    /// <summary>
    /// 複数の子ノードを持つコンポジットノードの抽象基底クラス
    /// </summary>
    public abstract class CompositeNodeData : BehaviourNodeData
    {
        /// <summary>子ノードリスト（エンジンからBuildで設定される）</summary>
        [NodeFieldHide]
        public List<BehaviourNodeData> Children { get; set; } = new List<BehaviourNodeData>();

        public override List<BehaviourNodeData> GetChildren() => Children;
    }
}