using ModuTree.Runtime.Engine;
using ModuTree.Runtime.Serialization;
using ModuTree.UnityIntegration;
using UnityEditor;
using UnityEngine;

namespace ModuTree.Editor
{
    /// <summary>
    /// UnityのProjectビュー・HierarchyビューのSelection変化を監視し、
    /// BehaviourTreeEditorWindowに通知する。
    /// </summary>
    [InitializeOnLoad]
    public static class SelectionObserver
    {
        /// <summary>現在表示中のBehaviourTreeData</summary>
        public static BehaviourTreeData CurrentTreeData { get; private set; }

        /// <summary>ランタイム中のEngine（HierarchyでRunner選択時）</summary>
        public static BehaviourTreeEngine CurrentEngine { get; private set; }

        /// <summary>保存対象のTextAsset（JSONファイル）</summary>
        public static TextAsset SaveTarget { get; private set; }

        static SelectionObserver()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            CurrentTreeData = null;
            CurrentEngine   = null;
            SaveTarget      = null;

            var selected = Selection.activeObject;

            // Projectビュー: JSONファイルを直接選択
            if (selected is TextAsset textAsset && IsValidBehaviourTreeJson(textAsset.text))
            {
                LoadFromTextAsset(textAsset);
                NotifyEditor();
                return;
            }

            // Hierarchyビュー: BehaviourTreeRunnerを持つGameObjectを選択
            if (selected is GameObject go)
            {
                var runner = go.GetComponent<BehaviourTreeRunner>();
                if (runner == null) return;

                SaveTarget = runner.behaviourTreeJson;
                if (SaveTarget != null)
                    LoadFromTextAsset(SaveTarget);

                // ランタイム中はEngineも参照
                if (Application.isPlaying && runner.Engine != null)
                    CurrentEngine = runner.Engine;

                NotifyEditor();
            }
        }

        private static void LoadFromTextAsset(TextAsset asset)
        {
            if (asset == null) return;
            try
            {
                CurrentTreeData = BehaviourTreeSerializer.FromJson(asset.text);
                SaveTarget      = asset;
            }
            catch
            {
                CurrentTreeData = null;
            }
        }

        private static bool IsValidBehaviourTreeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("\"rootGuid\"") && text.Contains("\"nodes\"");
        }

        private static void NotifyEditor()
        {
            if (!EditorWindow.HasOpenInstances<Windows.BehaviourTreeEditorWindow>())
                return;
            var window = EditorWindow.GetWindow<Windows.BehaviourTreeEditorWindow>(
                false, "ModuTree Editor", false);
            window?.OnSelectionUpdated();
        }
    }
}