using System;
using System.Collections.Generic;
using System.Linq;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Nodes;
using UnityEditor;

namespace ModuTree.Editor
{
    /// <summary>
    /// リフレクションでBehaviourNodeDataサブクラスを収集するレジストリ。
    /// エディタ起動時に自動で収集し、エディタウィンドウのノード追加メニューで使用する。
    /// </summary>
    [InitializeOnLoad]
    public static class NodeTypeRegistry
    {
        public class NodeEntry
        {
            public Type                       Type        { get; }
            public BehaviourNodeMetaAttribute Meta        { get; }
            public string                     Category    { get; }
            public string                     DisplayName { get; }

            public NodeEntry(Type type, BehaviourNodeMetaAttribute meta)
            {
                Type        = type;
                Meta        = meta;
                Category    = meta?.Category    ?? "Custom";
                DisplayName = meta?.DisplayName ?? type.Name;
            }
        }

        private static List<NodeEntry> _cache;

        static NodeTypeRegistry()
        {
            EditorApplication.projectChanged += Refresh;
        }

        public static IEnumerable<NodeEntry> GetEntries()
        {
            if (_cache != null) return _cache;
            BuildCache();
            return _cache;
        }

        /// <summary>カテゴリ別にグループ化して返す</summary>
        public static IEnumerable<IGrouping<string, NodeEntry>> GetEntriesByCategory()
            => GetEntries().GroupBy(e => e.Category);

        /// <summary>型からNodeEntryを取得する</summary>
        public static NodeEntry GetEntry(Type type)
            => GetEntries().FirstOrDefault(e => e.Type == type);

        /// <summary>ルートノードとして配置可能な型（Composite系のみ）</summary>
        public static IEnumerable<NodeEntry> GetRootCandidates()
            => GetEntries().Where(e =>
                typeof(CompositeNodeData).IsAssignableFrom(e.Type));

        public static void Refresh() => _cache = null;

        private static void BuildCache()
        {
            _cache = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try   { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t =>
                    !t.IsAbstract &&
                    typeof(BehaviourNodeData).IsAssignableFrom(t) &&
                    !IsAbstractBase(t))
                .Select(t => new NodeEntry(t,
                    t.GetCustomAttributes(typeof(BehaviourNodeMetaAttribute), false)
                     .FirstOrDefault() as BehaviourNodeMetaAttribute))
                .OrderBy(e => e.Category)
                .ThenBy(e => e.DisplayName)
                .ToList();
        }

        private static bool IsAbstractBase(Type t)
            => t == typeof(CompositeNodeData)
            || t == typeof(DecoratorNodeData)
            || t == typeof(ActionNodeData)
            || t == typeof(ConditionNodeData);
    }
}