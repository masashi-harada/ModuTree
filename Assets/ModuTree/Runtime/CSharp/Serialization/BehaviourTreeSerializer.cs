using System;
using System.Collections.Generic;
using System.Linq;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;

namespace ModuTree.Runtime.Serialization
{
    /// <summary>
    /// BehaviourTreeData の JSON変換 と ノードグラフ構築を担うクラス。
    /// Pure C# 実装のため Unity 非依存。
    /// </summary>
    public static class BehaviourTreeSerializer
    {
        // ─── シリアライズ ────────────────────────────────────

        /// <summary>BehaviourTreeDataをJSON文字列に変換する</summary>
        public static string ToJson(BehaviourTreeData treeData)
            => MiniJson.Serialize(treeData);

        /// <summary>JSON文字列をBehaviourTreeDataに変換する</summary>
        public static BehaviourTreeData FromJson(string json)
            => MiniJson.Deserialize<BehaviourTreeData>(json);

        // ─── ノードインスタンス生成 ──────────────────────────

        /// <summary>
        /// BehaviourTreeDataからノードインスタンスを生成し
        /// エンジンに渡せる形に組み立てる。
        /// </summary>
        public static (BehaviourNodeData root,
                        Dictionary<string, BehaviourNodeData> nodeMap)
            Build(BehaviourTreeData treeData)
        {
            var nodeMap = new Dictionary<string, BehaviourNodeData>();

            // ノード生成
            foreach (var nd in treeData.nodes)
            {
                var node = CreateInstance(nd);
                if (node == null) continue;
                node.Guid = nd.guid;
                nodeMap[nd.guid] = node;
            }

            // 接続構築（X座標昇順でソート）
            foreach (var nd in treeData.nodes)
            {
                if (!nodeMap.TryGetValue(nd.guid, out var node)) continue;

                if (node is CompositeNodeData comp)
                {
                    comp.Children = nd.childrenGuids
                        .Where(g => nodeMap.ContainsKey(g))
                        .Select(g => nodeMap[g])
                        .OrderBy(n =>
                        {
                            // GUIDでNodeDataを検索してX座標を返す
                            var childData = treeData.nodes.Find(x => x.guid == n.Guid);
                            return childData?.positionX ?? 0f;
                        })
                        .ToList();
                }
                else if (node is DecoratorNodeData dec &&
                         !string.IsNullOrEmpty(nd.childGuid))
                {
                    nodeMap.TryGetValue(nd.childGuid, out var child);
                    dec.Child = child;
                }
            }

            nodeMap.TryGetValue(treeData.rootGuid ?? "", out var root);
            return (root, nodeMap);
        }

        /// <summary>
        /// BehaviourTreeDataを構築する（エディタ保存用）
        /// </summary>
        public static BehaviourTreeData Save(
            BehaviourNodeData rootNode,
            Dictionary<string, BehaviourNodeData> nodeMap,
            Dictionary<string, (float x, float y)> positions)
        {
            var data = new BehaviourTreeData { rootGuid = rootNode?.Guid ?? "" };

            foreach (var kv in nodeMap)
            {
                var node = kv.Value;
                var nd = new NodeData
                {
                    guid           = kv.Key,
                    typeName       = node.GetType().AssemblyQualifiedName,
                    parametersJson = MiniJson.SerializeFields(node)
                };

                if (positions.TryGetValue(kv.Key, out var pos))
                {
                    nd.positionX = pos.x;
                    nd.positionY = pos.y;
                }

                if (node is CompositeNodeData comp)
                    nd.childrenGuids = comp.Children
                        .Where(c => c.Guid != null)
                        .Select(c => c.Guid)
                        .ToList();
                else if (node is DecoratorNodeData dec && dec.Child != null)
                    nd.childGuid = dec.Child.Guid;

                data.nodes.Add(nd);
            }

            return data;
        }

        // ─── 内部処理 ────────────────────────────────────────

        private static BehaviourNodeData CreateInstance(NodeData nd)
        {
            var type = Type.GetType(nd.typeName);
            if (type == null)
            {
                Console.Error.WriteLine($"[ModuTree] 型が見つかりません: {nd.typeName}");
                return null;
            }

            var instance = Activator.CreateInstance(type) as BehaviourNodeData;
            if (instance == null)
            {
                Console.Error.WriteLine($"[ModuTree] インスタンス生成失敗: {nd.typeName}");
                return null;
            }

            // パラメータ復元
            if (!string.IsNullOrEmpty(nd.parametersJson))
                MiniJson.PopulateFields(nd.parametersJson, instance);

            return instance;
        }
    }
}