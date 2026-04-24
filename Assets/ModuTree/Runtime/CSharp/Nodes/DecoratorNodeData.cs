using System.Collections.Generic;
using ModuTree.Runtime.Core;

namespace ModuTree.Runtime.Nodes
{
    /// <summary>
    /// 単一の子ノードを装飾するDecoratorノードの抽象基底クラス
    /// </summary>
    public abstract class DecoratorNodeData : BehaviourNodeData
    {
        /// <summary>装飾対象の子ノード（エンジンからBuildで設定される）</summary>
        [NodeFieldHide]
        public BehaviourNodeData Child { get; set; }

        public override List<BehaviourNodeData> GetChildren()
            => Child != null ? new List<BehaviourNodeData> { Child } : new List<BehaviourNodeData>();
    }
}