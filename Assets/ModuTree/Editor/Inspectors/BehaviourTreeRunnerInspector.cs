using ModuTree.UnityIntegration;
using UnityEditor;
using UnityEngine;

namespace ModuTree.Editor.Inspectors
{
    /// <summary>
    /// BehaviourTreeRunnerのカスタムInspector。
    /// エディタウィンドウを開くボタンと、ランタイム状態の表示を行う。
    /// </summary>
    [CustomEditor(typeof(BehaviourTreeRunner), true)]
    public class BehaviourTreeRunnerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("BehaviourTree Editorを開く", GUILayout.Height(30)))
            {
                var window = EditorWindow.GetWindow<Windows.BehaviourTreeEditorWindow>(
                    false, "ModuTree Editor", true);
                window.Show();
            }

            // ランタイム中の状態表示
            if (Application.isPlaying)
            {
                var runner = (BehaviourTreeRunner)target;
                if (runner.Engine != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("ランタイム状態",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Engine State",
                        runner.Engine.State.ToString());
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}