using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

namespace ModuTree.Editor.Inspectors
{
    /// <summary>
    /// BehaviourTreeEditorWindowで選択したノードの詳細をInspectorに表示する。
    /// ノードのパラメータ編集もここで行う。
    /// </summary>
    [CustomEditor(typeof(BehaviourNodeInspectorProxy))]
    public class BehaviourNodeInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var proxy = (BehaviourNodeInspectorProxy)target;
            if (proxy?.NodeData == null || proxy?.TreeData == null) return;

            var nd       = proxy.NodeData;
            var treeData = proxy.TreeData;

            // ノード型情報
            var type = Type.GetType(nd.typeName);
            var meta = type?.GetCustomAttributes(typeof(BehaviourNodeMetaAttribute), false)
                            .FirstOrDefault() as BehaviourNodeMetaAttribute;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(meta?.DisplayName ?? GetShortTypeName(nd.typeName),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(nd.typeName, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(meta?.Description))
            {
                EditorGUILayout.HelpBox(meta.Description, MessageType.None);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("GUID", nd.guid, EditorStyles.miniLabel);

            bool isRoot = nd.guid == treeData.rootGuid;
            EditorGUILayout.LabelField("Root", isRoot ? "Yes" : "No", EditorStyles.miniLabel);

            // ランタイム状態
            if (proxy.RuntimeNode != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("実行状態", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("State", proxy.RuntimeNode.State.ToString());
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("パラメータ", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // parametersJsonからインスタンスを復元して編集UI表示
            if (type != null)
            {
                var instance = Activator.CreateInstance(type);
                if (!string.IsNullOrEmpty(nd.parametersJson))
                    MiniJson.PopulateFields(nd.parametersJson, instance);

                bool changed = DrawFields(instance, type);

                if (changed)
                {
                    nd.parametersJson = MiniJson.SerializeFields(instance);
                    proxy.MarkDirty();
                    EditorUtility.SetDirty(target);
                }
            }
            else
            {
                EditorGUILayout.LabelField("型が見つかりません", EditorStyles.helpBox);
                EditorGUILayout.LabelField(nd.typeName, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private bool DrawFields(object instance, Type type)
        {
            bool changed = false;
            var  fields  = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<NodeFieldHideAttribute>() != null) continue;

                var nodeField = field.GetCustomAttribute<NodeFieldAttribute>();
                var label     = nodeField?.Label ?? ObjectNames.NicifyVariableName(field.Name);
                var tooltip   = nodeField?.Tooltip ?? "";
                var content   = new GUIContent(label, tooltip);

                var value    = field.GetValue(instance);
                var newValue = DrawField(content, value, field.FieldType);

                if (newValue != value && !Equals(newValue, value))
                {
                    field.SetValue(instance, newValue);
                    changed = true;
                }
            }

            return changed;
        }

        private object DrawField(GUIContent label, object value, Type type)
        {
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, value is int i ? i : 0);
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, value is float f ? f : 0f);
            if (type == typeof(double))
                return (double)EditorGUILayout.DoubleField(label, value is double d ? d : 0.0);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, value is bool b && b);
            if (type == typeof(string))
                return EditorGUILayout.TextField(label, value as string ?? "");
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, value is Enum e ? e : (Enum)Activator.CreateInstance(type));

            // SubTreeNodeDataのsubTreeJsonPath用ファイル選択
            if (type == typeof(string) && label.text.ToLower().Contains("path"))
            {
                EditorGUILayout.BeginHorizontal();
                var strVal = EditorGUILayout.TextField(label, value as string ?? "");
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFilePanel("BehaviourTree JSON", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(selected)) strVal = selected;
                }
                EditorGUILayout.EndHorizontal();
                return strVal;
            }

            // その他: 読み取り専用表示
            EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
            return value;
        }

        private static string GetShortTypeName(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName)) return "Unknown";
            var parts = assemblyQualifiedName.Split(',');
            var fullName = parts[0].Trim();
            var dotIdx = fullName.LastIndexOf('.');
            return dotIdx >= 0 ? fullName.Substring(dotIdx + 1) : fullName;
        }
    }

    /// <summary>
    /// BehaviourTreeEditorWindowからInspectorにノード情報を渡すためのScriptableObjectプロキシ。
    /// </summary>
    [CreateAssetMenu(menuName = "ModuTree/NodeInspectorProxy", order = 999)]
    public class BehaviourNodeInspectorProxy : ScriptableObject
    {
        [NonSerialized] public NodeData              NodeData;
        [NonSerialized] public BehaviourTreeData     TreeData;
        [NonSerialized] public BehaviourNodeData     RuntimeNode;
        [NonSerialized] public Action                OnDirty;

        public void MarkDirty() => OnDirty?.Invoke();
    }
}