using System;
using System.Collections.Generic;

namespace ModuTree.Runtime.Serialization
{
    /// <summary>
    /// BehaviourTreeのJSON保存形式。
    /// ScriptableObjectを使わずこのクラスをJSONで保存する。
    /// Unity/サーバ共通で使用できる Pure C# クラス。
    /// </summary>
    [Serializable]
    public class BehaviourTreeData
    {
        public string          version  = "2.0.0";
        public string          rootGuid;
        public List<NodeData>  nodes    = new List<NodeData>();
    }

    /// <summary>
    /// 1ノード分のシリアライズ用データ
    /// </summary>
    [Serializable]
    public class NodeData
    {
        public string       guid;
        public string       typeName;
        public float        positionX;
        public float        positionY;
        public List<string> childrenGuids  = new List<string>();
        public string       childGuid;
        /// <summary>ノード固有パラメータ（フラットなJSONオブジェクト）</summary>
        public string       parametersJson;
    }
}