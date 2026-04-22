using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Engine;
using ModuTree.Runtime.Nodes;
using ModuTree.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

namespace ModuTree.Editor.Windows
{
    /// <summary>
    /// ModuTree BehaviourTreeの視覚的なエディタウィンドウ。
    /// 上→下レイアウトでノードグラフを表示・編集できる。
    /// </summary>
    public class BehaviourTreeEditorWindow : EditorWindow
    {
        // ─── 定数 ────────────────────────────────────────────
        private const float NodeWidth     = 200f;
        private const float NodeHeight    = 60f;
        private const float HeaderHeight  = 22f;
        private const float GridSize      = 20f;
        private const float InspectorWidth = 280f;

        // ノード状態別カラー
        private static readonly Color ColorIdle    = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color ColorRunning = new Color(0.15f, 0.55f, 0.15f);
        private static readonly Color ColorSuccess = new Color(0.15f, 0.35f, 0.75f);
        private static readonly Color ColorFailure = new Color(0.65f, 0.15f, 0.15f);
        private static readonly Color ColorSelected = new Color(0.9f, 0.7f, 0.1f);

        // ─── ウィンドウメニュー ──────────────────────────────
        [MenuItem("Window/ModuTree/BehaviourTree Editor")]
        public static void Open()
            => GetWindow<BehaviourTreeEditorWindow>("ModuTree Editor");

        // ─── 状態 ────────────────────────────────────────────

        // グラフデータ
        private BehaviourTreeData    _treeData;
        private BehaviourTreeEngine  _runtimeEngine;

        // ノード表示用（GUI座標）
        private Dictionary<string, Rect>             _nodeRects   = new();
        private Dictionary<string, BehaviourNodeData> _nodeInstances = new();

        // 選択
        private HashSet<string> _selected    = new();
        private string          _connectingFrom; // 接続ドラッグ中のノードGUID
        private bool            _connectingFromBottom; // 下端からドラッグ中

        // パン・ズーム
        private Vector2 _panOffset = Vector2.zero;
        private float   _zoom      = 1f;

        // ドラッグ
        private bool    _isDraggingNodes;
        private bool    _isDraggingCanvas;
        private Vector2 _dragStart;
        private Dictionary<string, Vector2> _dragStartPositions = new();

        // ボックス選択
        private bool    _isBoxSelecting;
        private Vector2 _boxSelectStart;
        private Rect    _boxSelectRect;

        // 接続ドラッグ
        private Vector2 _connectDragPos;

        // 未保存変更
        private bool    _isDirty;
        private string  _savePath; // 保存先ファイルパス

        // Undo履歴
        private Stack<string> _undoStack = new();

        // コピーバッファ
        private List<NodeData> _clipboard = new();

        // サブツリー表示スタック（ダブルクリック対応）
        private Stack<(BehaviourTreeData data, string path)> _subTreeStack = new();

        // インスペクタパネル
        private Vector2 _inspectorScrollPos;

        // ─── ライフサイクル ───────────────────────────────────

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            // ランタイム中はリアルタイム更新
            if (Application.isPlaying && _runtimeEngine != null)
                Repaint();
        }

        /// <summary>Selectionが変化したときに呼ばれる（SelectionObserverから）</summary>
        public void OnSelectionUpdated()
        {
            if (SelectionObserver.CurrentTreeData != null)
            {
                LoadTree(SelectionObserver.CurrentTreeData,
                         SelectionObserver.SaveTarget != null
                             ? AssetDatabase.GetAssetPath(SelectionObserver.SaveTarget)
                             : null);
            }
            _runtimeEngine = SelectionObserver.CurrentEngine;
            Repaint();
        }

        // ─── GUI描画 ──────────────────────────────────────────

        private void OnGUI()
        {
            DrawBackground();
            DrawToolbar();

            float graphWidth  = position.width - InspectorWidth;
            float graphHeight = position.height - HeaderHeight;

            // グラフ領域（左側）
            var graphRect = new Rect(0, HeaderHeight, graphWidth, graphHeight);
            GUI.BeginClip(graphRect);
            {
                DrawGrid(graphRect);
                DrawConnections();
                DrawNodes();
                DrawBoxSelection();
                DrawConnectingLine();
            }
            GUI.EndClip();

            HandleEvents(graphRect);

            // セパレーター
            EditorGUI.DrawRect(new Rect(graphWidth, HeaderHeight, 1f, graphHeight),
                new Color(0.08f, 0.08f, 0.08f, 1f));

            // インスペクタパネル（右側）
            var inspRect = new Rect(graphWidth + 1f, HeaderHeight, InspectorWidth - 1f, graphHeight);
            DrawInspectorPanel(inspRect);

            DrawStatusBar();
        }

        // ─── 背景 ────────────────────────────────────────────

        private void DrawBackground()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                new Color(0.15f, 0.15f, 0.15f));
        }

        private void DrawGrid(Rect area)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.05f);
            float scaledGrid = GridSize * _zoom;

            // 細グリッド
            float startX = _panOffset.x % scaledGrid;
            float startY = _panOffset.y % scaledGrid;

            for (float x = startX; x < area.width;  x += scaledGrid)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, area.height));
            for (float y = startY; y < area.height; y += scaledGrid)
                Handles.DrawLine(new Vector3(0, y), new Vector3(area.width, y));

            // 大グリッド
            Handles.color = new Color(1f, 1f, 1f, 0.1f);
            scaledGrid = GridSize * 5f * _zoom;
            startX = _panOffset.x % scaledGrid;
            startY = _panOffset.y % scaledGrid;
            for (float x = startX; x < area.width;  x += scaledGrid)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, area.height));
            for (float y = startY; y < area.height; y += scaledGrid)
                Handles.DrawLine(new Vector3(0, y), new Vector3(area.width, y));

            Handles.color = Color.white;
        }

        // ─── ツールバー ──────────────────────────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 別名保存
            if (GUILayout.Button("別名保存", EditorStyles.toolbarButton, GUILayout.Width(70)))
                SaveAs();

            // 保存
            GUI.enabled = _isDirty;
            if (GUILayout.Button(_isDirty ? "保存*" : "保存", EditorStyles.toolbarButton, GUILayout.Width(50)))
                Save();
            GUI.enabled = true;

            GUILayout.Space(10);

            // サブツリーから戻るボタン
            if (_subTreeStack.Count > 0)
            {
                if (GUILayout.Button("← 戻る", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    PopSubTree();
                GUILayout.Label(_savePath != null ? Path.GetFileName(_savePath) : "サブツリー",
                    EditorStyles.toolbarButton);
            }
            else
            {
                GUILayout.Label(_savePath != null ? Path.GetFileName(_savePath) : "未選択",
                    EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            // ズーム表示
            GUILayout.Label($"Zoom: {_zoom:F1}x", EditorStyles.miniLabel, GUILayout.Width(80));

            // ズームリセット
            if (GUILayout.Button("1:1", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _zoom      = 1f;
                _panOffset = Vector2.zero;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─── ノード描画 ──────────────────────────────────────

        private void DrawNodes()
        {
            if (_treeData == null) return;

            foreach (var nd in _treeData.nodes)
            {
                if (!_nodeRects.TryGetValue(nd.guid, out var rect)) continue;

                var screenRect = GraphToScreen(rect);
                DrawNode(nd, screenRect);
            }
        }

        private void DrawNode(NodeData nd, Rect screenRect)
        {
            bool isSelected = _selected.Contains(nd.guid);
            bool isRoot     = nd.guid == _treeData?.rootGuid;

            // ランタイム状態
            NodeState? runtimeState = null;
            if (_runtimeEngine != null &&
                _runtimeEngine.NodeMap.TryGetValue(nd.guid, out var runtimeNode))
                runtimeState = runtimeNode.State;

            // 背景色
            Color bgColor = GetNodeColor(nd, runtimeState);
            if (isSelected) bgColor = Color.Lerp(bgColor, ColorSelected, 0.4f);

            // 影
            EditorGUI.DrawRect(new Rect(screenRect.x + 3, screenRect.y + 3, screenRect.width, screenRect.height),
                new Color(0, 0, 0, 0.4f));

            // 本体
            EditorGUI.DrawRect(screenRect, bgColor);

            // ボーダー
            var borderColor = isSelected ? ColorSelected : new Color(0.1f, 0.1f, 0.1f, 0.8f);
            DrawBorder(screenRect, borderColor, isSelected ? 2f : 1f);

            // Rootラベル
            if (isRoot)
            {
                var rootRect = new Rect(screenRect.x, screenRect.y - 16f * _zoom, screenRect.width, 16f * _zoom);
                GUI.color = new Color(1f, 0.8f, 0.2f);
                GUI.Label(rootRect, "▼ ROOT", GetCenteredStyle(10));
                GUI.color = Color.white;
            }

            // ノード名
            var meta      = GetMeta(nd);
            var typeName  = GetShortTypeName(nd.typeName);
            var dispName  = meta?.DisplayName ?? typeName;
            var category  = meta?.Category ?? "?";

            float fontSize = Mathf.Clamp(_zoom * 11f, 8f, 14f);

            // カテゴリ帯
            var categoryRect = new Rect(screenRect.x, screenRect.y, screenRect.width, 18f * _zoom);
            EditorGUI.DrawRect(categoryRect, new Color(0, 0, 0, 0.3f));

            var catStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = (int)Mathf.Clamp(_zoom * 9f, 7f, 11f),
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
            GUI.Label(categoryRect, category, catStyle);

            // 表示名
            var nameRect = new Rect(screenRect.x, screenRect.y + 18f * _zoom,
                screenRect.width, screenRect.height - 18f * _zoom);
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = (int)fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                normal    = { textColor = Color.white }
            };
            GUI.Label(nameRect, dispName, nameStyle);

            // ランタイム状態インジケータ
            if (runtimeState.HasValue)
            {
                var stateColor = runtimeState.Value switch
                {
                    NodeState.Running => Color.green,
                    NodeState.Success => Color.cyan,
                    NodeState.Failure => Color.red,
                    _                 => Color.gray
                };
                var dotRect = new Rect(screenRect.xMax - 12f * _zoom, screenRect.y + 4f * _zoom,
                    8f * _zoom, 8f * _zoom);
                EditorGUI.DrawRect(dotRect, stateColor);
            }

            // 上端コネクタ（親接続用）
            DrawConnector(screenRect, top: true, nd.guid);
            // 下端コネクタ（子接続用）
            DrawConnector(screenRect, top: false, nd.guid);
        }

        private void DrawConnector(Rect nodeRect, bool top, string guid)
        {
            float cy = top ? nodeRect.y : nodeRect.yMax;
            float cx = nodeRect.x + nodeRect.width / 2f;
            float r  = 5f * _zoom;
            var dot  = new Rect(cx - r, cy - r, r * 2f, r * 2f);
            EditorGUI.DrawRect(dot, new Color(0.7f, 0.7f, 0.7f, 0.8f));
        }

        private Color GetNodeColor(NodeData nd, NodeState? state)
        {
            if (state == NodeState.Running) return ColorRunning;
            if (state == NodeState.Success) return ColorSuccess;
            if (state == NodeState.Failure) return ColorFailure;

            var meta = GetMeta(nd);
            if (meta != null && !string.IsNullOrEmpty(meta.Color))
            {
                if (ColorUtility.TryParseHtmlString(meta.Color, out var c))
                    return c * 0.75f;
            }
            return ColorIdle;
        }

        // ─── 接続線描画 ──────────────────────────────────────

        private void DrawConnections()
        {
            if (_treeData == null) return;

            foreach (var nd in _treeData.nodes)
            {
                if (!_nodeRects.TryGetValue(nd.guid, out var parentRect)) continue;
                var parentScreen = GraphToScreen(parentRect);

                // CompositeNodeの子接続
                for (int i = 0; i < nd.childrenGuids.Count; i++)
                {
                    var childGuid = nd.childrenGuids[i];
                    if (!_nodeRects.TryGetValue(childGuid, out var childRect)) continue;
                    var childScreen = GraphToScreen(childRect);

                    var from = new Vector2(parentScreen.center.x, parentScreen.yMax);
                    var to   = new Vector2(childScreen.center.x,  childScreen.y);
                    DrawBezier(from, to, Color.gray);

                    // 評価順番号
                    if (nd.childrenGuids.Count > 1)
                    {
                        var mid = (from + to) / 2f;
                        var numStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize  = (int)(10f * _zoom),
                            alignment = TextAnchor.MiddleCenter,
                            normal    = { textColor = Color.yellow }
                        };
                        GUI.Label(new Rect(mid.x - 10f, mid.y - 10f, 20f, 20f),
                            (i + 1).ToString(), numStyle);
                    }
                }

                // DecoratorNodeの子接続
                if (!string.IsNullOrEmpty(nd.childGuid) &&
                    _nodeRects.TryGetValue(nd.childGuid, out var decChildRect))
                {
                    var decChildScreen = GraphToScreen(decChildRect);
                    var from = new Vector2(parentScreen.center.x, parentScreen.yMax);
                    var to   = new Vector2(decChildScreen.center.x, decChildScreen.y);
                    DrawBezier(from, to, new Color(0.6f, 0.5f, 0.9f));
                }
            }
        }

        private void DrawBezier(Vector2 from, Vector2 to, Color color)
        {
            float dist   = Mathf.Abs(to.y - from.y) * 0.5f;
            var   fromTan = from + Vector2.up   * dist;
            var   toTan   = to   + Vector2.down * dist;

            Handles.DrawBezier(from, to, fromTan, toTan, color, null, 2f);
        }

        private void DrawConnectingLine()
        {
            if (_connectingFrom == null) return;
            if (!_nodeRects.TryGetValue(_connectingFrom, out var rect)) return;

            var screenRect = GraphToScreen(rect);
            var from = _connectingFromBottom
                ? new Vector2(screenRect.center.x, screenRect.yMax)
                : new Vector2(screenRect.center.x, screenRect.y);

            Handles.DrawBezier(from, _connectDragPos,
                from + Vector2.up * 50f * _zoom,
                _connectDragPos + Vector2.down * 50f * _zoom,
                new Color(1f, 1f, 0f, 0.8f), null, 2f);
            Repaint();
        }

        // ─── ボックス選択描画 ────────────────────────────────

        private void DrawBoxSelection()
        {
            if (!_isBoxSelecting) return;

            var rect = NormalizeRect(_boxSelectRect);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.6f, 1f, 0.15f));
            DrawBorder(rect, new Color(0.3f, 0.6f, 1f, 0.6f), 1f);
        }

        // ─── インスペクタパネル ──────────────────────────────

        private void DrawInspectorPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            GUILayout.BeginArea(rect);
            _inspectorScrollPos = EditorGUILayout.BeginScrollView(_inspectorScrollPos);

            if (_treeData == null || _selected.Count == 0)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("ノードを選択してください",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            if (_selected.Count > 1)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField($"{_selected.Count} ノードを選択中",
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            var guid = _selected.First();
            var nd   = _treeData.nodes.Find(n => n.guid == guid);
            if (nd == null)
            {
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            var type = Type.GetType(nd.typeName);
            var meta = type?.GetCustomAttributes(typeof(BehaviourNodeMetaAttribute), false)
                            .FirstOrDefault() as BehaviourNodeMetaAttribute;

            EditorGUILayout.Space(6);

            // ── ノード名・カテゴリ ─────────────────────────
            EditorGUILayout.LabelField(meta?.DisplayName ?? GetShortTypeName(nd.typeName),
                new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 13 });

            if (meta != null)
            {
                EditorGUILayout.LabelField(
                    meta.Category + (nd.guid == _treeData.rootGuid ? "  [Root]" : ""),
                    EditorStyles.miniLabel);
            }

            // ── スクリプト参照 ────────────────────────────
            if (type != null)
            {
                var script = Resources.FindObjectsOfTypeAll<MonoScript>()
                                      .FirstOrDefault(s => s.GetClass() == type);
                if (script != null)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
                    EditorGUI.EndDisabledGroup();
                }
            }

            // ── 説明 ──────────────────────────────────────
            if (!string.IsNullOrEmpty(meta?.Description))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(meta.Description, MessageType.None);
            }

            // ── ランタイム状態 ────────────────────────────
            if (_runtimeEngine != null &&
                _runtimeEngine.NodeMap.TryGetValue(guid, out var rtNode))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("実行状態", EditorStyles.boldLabel);
                var stateColor = rtNode.State switch
                {
                    NodeState.Running => new Color(0.2f, 1f, 0.2f),
                    NodeState.Success => new Color(0.4f, 0.8f, 1f),
                    NodeState.Failure => new Color(1f, 0.3f, 0.3f),
                    _                 => Color.gray
                };
                var prev = GUI.color;
                GUI.color = stateColor;
                EditorGUILayout.LabelField("● " + rtNode.State.ToString(), EditorStyles.boldLabel);
                GUI.color = prev;
            }

            EditorGUILayout.Space(6);

            // ── パラメータ ────────────────────────────────
            EditorGUILayout.LabelField("パラメータ", EditorStyles.boldLabel);

            if (type != null)
            {
                var instance = Activator.CreateInstance(type);
                if (!string.IsNullOrEmpty(nd.parametersJson))
                    MiniJson.PopulateFields(nd.parametersJson, instance);

                EditorGUI.BeginChangeCheck();
                DrawInspectorFields(instance, type, _savePath ?? "");
                if (EditorGUI.EndChangeCheck())
                {
                    PushUndo();
                    nd.parametersJson = MiniJson.SerializeFields(instance);
                    MarkDirty();
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"型が見つかりません:\n{nd.typeName}", MessageType.Warning);
            }

            // ── GUID（折りたたみ不要な補足情報） ─────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GUID", nd.guid, EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawInspectorFields(object instance, Type type, string basePath = "")
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<NodeFieldHideAttribute>() != null) continue;

                var nodeField = field.GetCustomAttribute<NodeFieldAttribute>();
                var label     = nodeField?.Label ?? ObjectNames.NicifyVariableName(field.Name);
                var tooltip   = nodeField?.Tooltip ?? "";
                var content   = new GUIContent(label, tooltip);

                var value    = field.GetValue(instance);
                var newValue = DrawInspectorField(content, value, field.FieldType, field.Name, basePath);
                field.SetValue(instance, newValue);
            }
        }

        private static object DrawInspectorField(GUIContent label, object value, Type type, string fieldName, string basePath = "")
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
            {
                // パス・JSONフィールドはObjectField（D&D対応）で表示
                var lower = fieldName.ToLower();
                if (lower.Contains("path") || lower.Contains("json"))
                {
                    var strVal = value as string ?? "";

                    // 相対パス → TextAsset に変換（表示用）
                    TextAsset currentAsset = RelativePathToTextAsset(strVal, basePath);

                    EditorGUILayout.LabelField(label);
                    var newAsset = EditorGUILayout.ObjectField(
                        currentAsset, typeof(TextAsset), false) as TextAsset;

                    if (newAsset != currentAsset)
                        strVal = newAsset != null ? TextAssetToRelativePath(newAsset, basePath) : "";

                    // 相対パスを補足表示
                    if (!string.IsNullOrEmpty(strVal))
                    {
                        var pathStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                        EditorGUILayout.LabelField(strVal, pathStyle);
                    }

                    return strVal;
                }
                return EditorGUILayout.TextField(label, value as string ?? "");
            }
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label,
                    value is Enum e ? e : (Enum)Activator.CreateInstance(type));

            // その他: 読み取り専用
            EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
            return value;
        }

        /// <summary>
        /// 相対パス文字列からTextAssetを取得する。
        /// basePath（親JSONの絶対パス）を基準に解決する。
        /// 絶対パスの場合は後方互換としてそのまま使用する。
        /// </summary>
        private static TextAsset RelativePathToTextAsset(string relativePath, string basePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            string absolutePath;
            if (Path.IsPathRooted(relativePath))
            {
                absolutePath = relativePath;
            }
            else if (!string.IsNullOrEmpty(basePath))
            {
                var baseDir = Path.GetDirectoryName(basePath) ?? "";
                absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
            }
            else return null;

            // 絶対パス → "Assets/..." 形式に変換
            var normalized  = absolutePath.Replace("\\", "/");
            var projectRoot = Application.dataPath.Replace("\\", "/").Replace("/Assets", "/");
            if (!normalized.StartsWith(projectRoot)) return null;
            var assetPath = normalized.Substring(projectRoot.Length);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        /// <summary>
        /// TextAsset を basePath（親JSONの絶対パス）からの相対パスに変換する。
        /// </summary>
        private static string TextAssetToRelativePath(TextAsset asset, string basePath)
        {
            var assetRelPath = AssetDatabase.GetAssetPath(asset);
            var absolutePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetRelPath));

            if (string.IsNullOrEmpty(basePath))
                return absolutePath; // basePath未設定時は後方互換として絶対パス

            var baseDir = Path.GetDirectoryName(basePath) ?? "";
            if (string.IsNullOrEmpty(baseDir)) return absolutePath;

            // Uri を使って相対パスを計算する
            var fromUri = new Uri(baseDir.TrimEnd('/', '\\') + Path.DirectorySeparatorChar);
            var toUri   = new Uri(absolutePath);
            var rel     = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(rel.ToString())
                      .Replace('/', Path.DirectorySeparatorChar);
        }

        // ─── ステータスバー ───────────────────────────────────

        private void DrawStatusBar()
        {
            var barRect = new Rect(0, position.height - 20f, position.width, 20f);
            EditorGUI.DrawRect(barRect, new Color(0.1f, 0.1f, 0.1f, 0.9f));

            string status = _treeData == null ? "未選択"
                : $"ノード数: {_treeData.nodes.Count}  |  選択: {_selected.Count}";
            if (Application.isPlaying) status += "  |  [実行中]";

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(new Rect(8f, position.height - 20f, position.width - 8f, 20f), status, style);
        }

        // ─── イベント処理 ─────────────────────────────────────

        private void HandleEvents(Rect graphRect)
        {
            var e = Event.current;
            // グラフ領域内のマウス座標（ツールバー分をオフセット）
            var mouseInGraph = e.mousePosition - new Vector2(0, HeaderHeight);

            switch (e.type)
            {
                case EventType.KeyDown:
                    HandleKeyDown(e);
                    break;

                case EventType.ScrollWheel:
                    HandleScroll(e, mouseInGraph);
                    break;

                case EventType.MouseDown:
                    HandleMouseDown(e, mouseInGraph);
                    break;

                case EventType.MouseDrag:
                    HandleMouseDrag(e, mouseInGraph);
                    break;

                case EventType.MouseUp:
                    HandleMouseUp(e, mouseInGraph);
                    break;

                case EventType.ContextClick:
                    if (graphRect.Contains(e.mousePosition))
                        ShowContextMenu(mouseInGraph);
                    e.Use();
                    break;
            }
        }

        private void HandleKeyDown(Event e)
        {
            // Cmd/Ctrl + S: 保存
            if ((e.command || e.control) && e.keyCode == KeyCode.S)
            {
                Save();
                e.Use();
                return;
            }

            // Cmd/Ctrl + Z: Undo
            if ((e.command || e.control) && e.keyCode == KeyCode.Z)
            {
                Undo();
                e.Use();
                return;
            }

            // Cmd/Ctrl + C: コピー
            if ((e.command || e.control) && e.keyCode == KeyCode.C)
            {
                CopySelected();
                e.Use();
                return;
            }

            // Cmd/Ctrl + X: カット
            if ((e.command || e.control) && e.keyCode == KeyCode.X)
            {
                CutSelected();
                e.Use();
                return;
            }

            // Cmd/Ctrl + V: ペースト
            if ((e.command || e.control) && e.keyCode == KeyCode.V)
            {
                PasteNodes();
                e.Use();
                return;
            }

        }

        private void HandleScroll(Event e, Vector2 mousePos)
        {
            float delta = e.delta.y * -0.05f;
            float prevZoom = _zoom;
            _zoom = Mathf.Clamp(_zoom + delta, 0.3f, 3f);

            // マウス位置を中心にズーム
            float scale   = _zoom / prevZoom;
            _panOffset = mousePos - scale * (mousePos - _panOffset);

            e.Use();
            Repaint();
        }

        private void HandleMouseDown(Event e, Vector2 mousePos)
        {
            // インスペクタパネル内のクリックはグラフ操作を行わない
            if (mousePos.x >= position.width - InspectorWidth) return;

            if (e.button == 0) // 左クリック
            {
                var hitGuid = HitTestNode(mousePos);

                if (hitGuid != null)
                {
                    // ダブルクリック: サブツリーを開く
                    if (e.clickCount == 2)
                    {
                        TryOpenSubTree(hitGuid);
                        e.Use();
                        return;
                    }

                    // コネクタ付近をクリック → 接続ドラッグ開始
                    if (IsNearConnector(mousePos, hitGuid, bottom: true))
                    {
                        _connectingFrom       = hitGuid;
                        _connectingFromBottom = true;
                        _connectDragPos       = mousePos;
                        e.Use();
                        return;
                    }
                    if (IsNearConnector(mousePos, hitGuid, bottom: false))
                    {
                        _connectingFrom       = hitGuid;
                        _connectingFromBottom = false;
                        _connectDragPos       = mousePos;
                        e.Use();
                        return;
                    }

                    // ノード選択
                    if (!e.shift && !_selected.Contains(hitGuid))
                        _selected.Clear();
                    if (e.shift && _selected.Contains(hitGuid))
                        _selected.Remove(hitGuid);
                    else
                        _selected.Add(hitGuid);

                    // ドラッグ準備
                    _isDraggingNodes  = true;
                    _dragStart        = mousePos;
                    _dragStartPositions.Clear();
                    foreach (var guid in _selected)
                    {
                        if (_nodeRects.TryGetValue(guid, out var r))
                            _dragStartPositions[guid] = r.position;
                    }

                    NotifyInspector();
                    e.Use();
                }
                else
                {
                    // トリプルクリック（背景）: Rootノードを中心にビューをリセット
                    if (e.clickCount >= 3)
                    {
                        FocusRootNode();
                        e.Use();
                        return;
                    }

                    if (e.alt)
                    {
                        // Alt + 左ドラッグ: キャンバスパン
                        _isDraggingCanvas = true;
                        _dragStart        = mousePos;
                    }
                    else
                    {
                        // 空白クリック → 選択解除 + ボックス選択開始
                        if (!e.shift) _selected.Clear();
                        _isBoxSelecting   = true;
                        _boxSelectStart   = mousePos;
                        _boxSelectRect    = new Rect(mousePos, Vector2.zero);
                        _isDraggingCanvas = false;
                    }
                    e.Use();
                }
            }
            else if (e.button == 2) // 中ボタン: パン開始
            {
                _isDraggingCanvas = true;
                _dragStart        = mousePos;
                e.Use();
            }
        }

        private void HandleMouseDrag(Event e, Vector2 mousePos)
        {
            if (_connectingFrom != null)
            {
                _connectDragPos = mousePos;
                Repaint();
                e.Use();
                return;
            }

            if (_isDraggingNodes && _selected.Count > 0 && !e.alt)
            {
                var delta = ScreenToGraph(mousePos) - ScreenToGraph(_dragStart);
                foreach (var guid in _selected)
                {
                    if (!_dragStartPositions.TryGetValue(guid, out var startPos)) continue;
                    var nd = _treeData?.nodes.Find(n => n.guid == guid);
                    if (nd == null) continue;

                    var newPos = startPos + delta;
                    nd.positionX = newPos.x;
                    nd.positionY = newPos.y;
                    _nodeRects[guid] = new Rect(newPos, new Vector2(NodeWidth, NodeHeight));
                }
                SortChildrenByPosition();
                MarkDirty();
                Repaint();
                e.Use();
                return;
            }

            if (_isBoxSelecting)
            {
                _boxSelectRect = new Rect(_boxSelectStart,
                    mousePos - _boxSelectStart);
                // ボックス内のノードを選択
                _selected.Clear();
                var box = NormalizeRect(_boxSelectRect);
                foreach (var kv in _nodeRects)
                {
                    if (box.Overlaps(GraphToScreen(kv.Value)))
                        _selected.Add(kv.Key);
                }
                Repaint();
                e.Use();
                return;
            }

            if (_isDraggingCanvas)
            {
                _panOffset += e.delta;
                Repaint();
                e.Use();
            }
        }

        private void HandleMouseUp(Event e, Vector2 mousePos)
        {
            // 接続ドラッグ終了
            if (_connectingFrom != null)
            {
                var targetGuid = HitTestNode(mousePos);
                if (targetGuid != null && targetGuid != _connectingFrom)
                    ConnectNodes(_connectingFrom, targetGuid, _connectingFromBottom);

                _connectingFrom = null;
                Repaint();
                e.Use();
                return;
            }

            _isDraggingNodes  = false;
            _isBoxSelecting   = false;
            _isDraggingCanvas = false;
        }

        // ─── コンテキストメニュー ─────────────────────────────

        private void ShowContextMenu(Vector2 graphMousePos)
        {
            var menu      = new GenericMenu();
            var graphPos  = ScreenToGraph(graphMousePos);
            var hitGuid   = HitTestNode(graphMousePos);
            var hitConn   = hitGuid == null ? HitTestConnection(graphMousePos) : null;

            if (hitGuid != null)
            {
                // ノード上での右クリック
                var nd = _treeData?.nodes.Find(n => n.guid == hitGuid);

                menu.AddItem(new GUIContent("Rootに設定"), false, () =>
                {
                    PushUndo();
                    _treeData.rootGuid = hitGuid;
                    MarkDirty();
                    Repaint();
                });

                // 複数選択中は選択全体を削除、単体の場合はそのノードを削除
                bool isMultiSelect = _selected.Count > 1 && _selected.Contains(hitGuid);
                var deleteLabel = isMultiSelect ? $"選択中の {_selected.Count} ノードを削除" : "削除";
                menu.AddItem(new GUIContent(deleteLabel), false, () =>
                {
                    if (!isMultiSelect) _selected.Clear();
                    _selected.Add(hitGuid);
                    DeleteSelected();
                });

                // サブツリーなら「中を開く」オプション
                if (nd != null)
                {
                    var type = Type.GetType(nd.typeName);
                    if (type != null && typeof(SubTreeNodeData).IsAssignableFrom(type))
                    {
                        menu.AddItem(new GUIContent("サブツリーを開く"), false, () =>
                            TryOpenSubTree(hitGuid));
                    }
                }
            }
            else if (hitConn.HasValue)
            {
                // 接続線上での右クリック
                var conn = hitConn.Value;
                menu.AddItem(new GUIContent("接続を解除"), false, () =>
                {
                    PushUndo();
                    var parentNd = _treeData?.nodes.Find(n => n.guid == conn.parentGuid);
                    if (parentNd != null)
                    {
                        if (conn.isDecorator)
                            parentNd.childGuid = null;
                        else
                            parentNd.childrenGuids.Remove(conn.childGuid);
                    }
                    MarkDirty();
                    Repaint();
                });
            }
            else
            {
                if (_treeData == null)
                {
                    menu.AddItem(new GUIContent("新規ツリーを作成"), false, () => CreateNewTree());
                }
                else
                {
                    // ノードなし → ノード追加
                    bool hasRoot = !string.IsNullOrEmpty(_treeData.rootGuid);

                    // ルートがない場合はComposite系のみ追加可能
                    foreach (var entry in hasRoot
                        ? NodeTypeRegistry.GetEntries()
                        : NodeTypeRegistry.GetRootCandidates())
                    {
                        var capturedEntry = entry;
                        var label = $"{entry.Category}/{entry.DisplayName}";
                        menu.AddItem(new GUIContent(label), false, () =>
                            AddNode(capturedEntry.Type, graphPos));
                    }
                }
            }

            menu.ShowAsContext();
        }

        // ─── ノード操作 ──────────────────────────────────────

        private void AddNode(Type type, Vector2 graphPos)
        {
            if (_treeData == null) return;
            PushUndo();

            var nd = new NodeData
            {
                guid      = Guid.NewGuid().ToString(),
                typeName  = type.AssemblyQualifiedName,
                positionX = graphPos.x,
                positionY = graphPos.y
            };

            _treeData.nodes.Add(nd);
            _nodeRects[nd.guid] = new Rect(nd.positionX, nd.positionY, NodeWidth, NodeHeight);

            // ルートがなければ自動設定（Composite系のみ）
            if (string.IsNullOrEmpty(_treeData.rootGuid) &&
                typeof(CompositeNodeData).IsAssignableFrom(type))
                _treeData.rootGuid = nd.guid;

            MarkDirty();
            Repaint();
        }

        private void ConnectNodes(string fromGuid, string toGuid, bool fromBottom)
        {
            if (_treeData == null) return;
            PushUndo();

            var fromNd = _treeData.nodes.Find(n => n.guid == fromGuid);
            var toNd   = _treeData.nodes.Find(n => n.guid == toGuid);
            if (fromNd == null || toNd == null) return;

            var fromType = Type.GetType(fromNd.typeName);
            var toType   = Type.GetType(toNd.typeName);

            if (fromBottom)
            {
                // fromの下端 → toの上端
                if (fromType != null && typeof(CompositeNodeData).IsAssignableFrom(fromType))
                {
                    if (!fromNd.childrenGuids.Contains(toGuid))
                        fromNd.childrenGuids.Add(toGuid);
                }
                else if (fromType != null && typeof(DecoratorNodeData).IsAssignableFrom(fromType))
                {
                    fromNd.childGuid = toGuid;
                }
            }
            else
            {
                // fromの上端 → toの下端（親→子 逆向き接続）
                if (toType != null && typeof(CompositeNodeData).IsAssignableFrom(toType))
                {
                    if (!toNd.childrenGuids.Contains(fromGuid))
                        toNd.childrenGuids.Add(fromGuid);
                }
                else if (toType != null && typeof(DecoratorNodeData).IsAssignableFrom(toType))
                {
                    toNd.childGuid = fromGuid;
                }
            }

            SortChildrenByPosition();
            MarkDirty();
            Repaint();
        }

        private void DeleteSelected()
        {
            if (_treeData == null || _selected.Count == 0) return;
            PushUndo();

            foreach (var guid in _selected)
            {
                _treeData.nodes.RemoveAll(n => n.guid == guid);
                _nodeRects.Remove(guid);

                // 接続からも除去
                foreach (var nd in _treeData.nodes)
                {
                    nd.childrenGuids.Remove(guid);
                    if (nd.childGuid == guid) nd.childGuid = null;
                }

                if (_treeData.rootGuid == guid) _treeData.rootGuid = null;
            }

            _selected.Clear();
            MarkDirty();
            Repaint();
        }

        private void TryOpenSubTree(string guid)
        {
            var nd = _treeData?.nodes.Find(n => n.guid == guid);
            if (nd == null) return;

            var type = Type.GetType(nd.typeName);
            if (type == null || !typeof(SubTreeNodeData).IsAssignableFrom(type)) return;

            // parametersJsonからsubTreeJsonPathを取得
            var tmp = Activator.CreateInstance(type) as BehaviourNodeData;
            if (!string.IsNullOrEmpty(nd.parametersJson))
                MiniJson.PopulateFields(nd.parametersJson, tmp);

            var subNode = tmp as SubTreeNodeData;
            if (subNode == null || string.IsNullOrEmpty(subNode.subTreeJsonPath))
            {
                Debug.LogWarning("[ModuTree] サブツリーのJSONパスが設定されていません。インスペクタパネルで subTreeJsonPath を設定してください。");
                return;
            }

            // 相対パスを絶対パスに解決（savePath基準）
            var resolvedPath = subNode.subTreeJsonPath;
            if (!Path.IsPathRooted(resolvedPath) && !string.IsNullOrEmpty(_savePath))
            {
                var baseDir = Path.GetDirectoryName(_savePath) ?? "";
                resolvedPath = Path.GetFullPath(Path.Combine(baseDir, resolvedPath));
            }

            if (!File.Exists(resolvedPath))
            {
                Debug.LogWarning($"[ModuTree] サブツリーJSONが見つかりません: {resolvedPath}  (元パス: {subNode.subTreeJsonPath})");
                return;
            }

            // 現在のツリーをスタックに積む
            _subTreeStack.Push((_treeData, _savePath));

            var subJson = File.ReadAllText(resolvedPath);
            LoadTree(BehaviourTreeSerializer.FromJson(subJson), resolvedPath);
            Repaint();
        }

        private void PopSubTree()
        {
            if (_subTreeStack.Count == 0) return;
            var (data, path) = _subTreeStack.Pop();
            LoadTree(data, path);
            Repaint();
        }

        /// <summary>Rootノードを画面中央に表示し、ズームを1.0にリセットする</summary>
        private void FocusRootNode()
        {
            if (_treeData == null) return;

            // 対象ノード: Rootがあればそれ、なければ全ノードの重心
            Vector2 targetGraphPos;
            if (!string.IsNullOrEmpty(_treeData.rootGuid) &&
                _nodeRects.TryGetValue(_treeData.rootGuid, out var rootRect))
            {
                targetGraphPos = rootRect.center;
            }
            else if (_nodeRects.Count > 0)
            {
                targetGraphPos = _nodeRects.Values
                    .Aggregate(Vector2.zero, (sum, r) => sum + r.center)
                    / _nodeRects.Count;
            }
            else
            {
                _zoom      = 1f;
                _panOffset = Vector2.zero;
                Repaint();
                return;
            }

            _zoom = 1f;
            float graphWidth  = position.width - InspectorWidth;
            float graphHeight = position.height - HeaderHeight;
            // グラフ座標をスクリーン中央に合わせるパンオフセットを計算
            _panOffset = new Vector2(graphWidth / 2f, graphHeight / 2f)
                         - targetGraphPos * _zoom;
            Repaint();
        }

        private void CreateNewTree()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "新規BehaviourTree", "NewBehaviourTree", "json",
                "保存先を選択してください");
            if (string.IsNullOrEmpty(path)) return;

            var data = new BehaviourTreeData();
            LoadTree(data, path);
            _isDirty = true;
            Repaint();
        }

        // ─── コピー・ペースト・Undo ──────────────────────────

        private void CopySelected()
        {
            if (_treeData == null) return;
            _clipboard = _treeData.nodes
                .Where(n => _selected.Contains(n.guid))
                .Select(n => CloneNodeData(n))
                .ToList();
        }

        private void CutSelected()
        {
            CopySelected();
            DeleteSelected();
        }

        private void PasteNodes()
        {
            if (_treeData == null || _clipboard.Count == 0) return;
            PushUndo();

            var guidMap = new Dictionary<string, string>();
            foreach (var src in _clipboard)
            {
                var newGuid        = Guid.NewGuid().ToString();
                guidMap[src.guid]  = newGuid;
            }

            _selected.Clear();

            foreach (var src in _clipboard)
            {
                var nd = CloneNodeData(src);
                nd.guid       = guidMap[src.guid];
                nd.positionX += 40f;
                nd.positionY += 40f;
                // 子接続もGUID変換
                nd.childrenGuids = nd.childrenGuids
                    .Select(g => guidMap.TryGetValue(g, out var ng) ? ng : g)
                    .ToList();
                if (nd.childGuid != null && guidMap.TryGetValue(nd.childGuid, out var nc))
                    nd.childGuid = nc;

                _treeData.nodes.Add(nd);
                _nodeRects[nd.guid] = new Rect(nd.positionX, nd.positionY, NodeWidth, NodeHeight);
                _selected.Add(nd.guid);
            }

            MarkDirty();
            Repaint();
        }

        private void PushUndo()
        {
            var snapshot = BehaviourTreeSerializer.ToJson(_treeData);
            _undoStack.Push(snapshot);
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            var snapshot = _undoStack.Pop();
            _treeData    = BehaviourTreeSerializer.FromJson(snapshot);
            RebuildNodeRects();
            MarkDirty();
            Repaint();
        }

        // ─── 保存 ────────────────────────────────────────────

        private void Save()
        {
            if (_treeData == null) return;

            if (string.IsNullOrEmpty(_savePath))
            {
                SaveAs();
                return;
            }

            WriteJson(_savePath);
        }

        private void SaveAs()
        {
            if (_treeData == null) return;

            string defaultDir  = _savePath != null ? Path.GetDirectoryName(_savePath) : "Assets";
            string defaultName = _savePath != null ? Path.GetFileNameWithoutExtension(_savePath) : "NewBehaviourTree";
            string path = EditorUtility.SaveFilePanelInProject(
                "BehaviourTreeを別名保存", defaultName, "json", "保存先を選択", defaultDir);
            if (string.IsNullOrEmpty(path)) return;

            _savePath = path;
            WriteJson(path);
        }

        private void WriteJson(string path)
        {
            try
            {
                var json = BehaviourTreeSerializer.ToJson(_treeData);
                File.WriteAllText(path, json);
                _isDirty = false;
                AssetDatabase.Refresh();
                Debug.Log($"[ModuTree] 保存しました: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModuTree] 保存に失敗しました: {ex.Message}");
            }
        }

        // ─── ツリー読み込み ───────────────────────────────────

        private void LoadTree(BehaviourTreeData data, string path)
        {
            _treeData   = data;
            _savePath   = path;
            _isDirty    = false;
            _selected.Clear();
            _undoStack.Clear();
            RebuildNodeRects();
            SortChildrenByPosition();
        }

        private void RebuildNodeRects()
        {
            _nodeRects.Clear();
            if (_treeData == null) return;
            foreach (var nd in _treeData.nodes)
                _nodeRects[nd.guid] = new Rect(nd.positionX, nd.positionY, NodeWidth, NodeHeight);
        }

        // ─── ユーティリティ ──────────────────────────────────

        private string HitTestNode(Vector2 screenPos)
        {
            if (_treeData == null) return null;
            // 後ろから（上に描画されるノードが優先）
            for (int i = _treeData.nodes.Count - 1; i >= 0; i--)
            {
                var nd = _treeData.nodes[i];
                if (!_nodeRects.TryGetValue(nd.guid, out var r)) continue;
                if (GraphToScreen(r).Contains(screenPos)) return nd.guid;
            }
            return null;
        }

        /// <summary>スクリーン座標が接続線の近くにあるか判定し、接続情報を返す</summary>
        private (string parentGuid, string childGuid, bool isDecorator)? HitTestConnection(Vector2 screenPos)
        {
            if (_treeData == null) return null;
            const float threshold = 8f;
            const int   samples   = 20;

            foreach (var nd in _treeData.nodes)
            {
                if (!_nodeRects.TryGetValue(nd.guid, out var parentRect)) continue;
                var parentScreen = GraphToScreen(parentRect);

                for (int i = 0; i < nd.childrenGuids.Count; i++)
                {
                    var childGuid = nd.childrenGuids[i];
                    if (!_nodeRects.TryGetValue(childGuid, out var childRect)) continue;
                    var childScreen = GraphToScreen(childRect);

                    var from = new Vector2(parentScreen.center.x, parentScreen.yMax);
                    var to   = new Vector2(childScreen.center.x,  childScreen.y);
                    if (IsNearBezier(screenPos, from, to, threshold, samples))
                        return (nd.guid, childGuid, false);
                }

                if (!string.IsNullOrEmpty(nd.childGuid) &&
                    _nodeRects.TryGetValue(nd.childGuid, out var decChildRect))
                {
                    var decChildScreen = GraphToScreen(decChildRect);
                    var from = new Vector2(parentScreen.center.x, parentScreen.yMax);
                    var to   = new Vector2(decChildScreen.center.x, decChildScreen.y);
                    if (IsNearBezier(screenPos, from, to, threshold, samples))
                        return (nd.guid, nd.childGuid, true);
                }
            }
            return null;
        }

        private static bool IsNearBezier(Vector2 point, Vector2 from, Vector2 to, float threshold, int samples)
        {
            float dist = Mathf.Abs(to.y - from.y) * 0.5f;
            var p1 = from + Vector2.up   * dist;
            var p2 = to   + Vector2.down * dist;

            for (int i = 0; i <= samples; i++)
            {
                float t  = i / (float)samples;
                float u  = 1f - t;
                var   bp = u*u*u * from + 3f*u*u*t * p1 + 3f*u*t*t * p2 + t*t*t * to;
                if (Vector2.Distance(point, bp) <= threshold)
                    return true;
            }
            return false;
        }

        private bool IsNearConnector(Vector2 screenPos, string guid, bool bottom)
        {
            if (!_nodeRects.TryGetValue(guid, out var r)) return false;
            var screenRect = GraphToScreen(r);
            var cy = bottom ? screenRect.yMax : screenRect.y;
            var cx = screenRect.center.x;
            return Vector2.Distance(screenPos, new Vector2(cx, cy)) < 12f * _zoom;
        }

        private Vector2 ScreenToGraph(Vector2 screenPos)
            => (screenPos - _panOffset) / _zoom;

        private Rect GraphToScreen(Rect graphRect)
            => new Rect(
                graphRect.x * _zoom + _panOffset.x,
                graphRect.y * _zoom + _panOffset.y,
                graphRect.width  * _zoom,
                graphRect.height * _zoom);

        private static Rect NormalizeRect(Rect r)
        {
            if (r.width  < 0) { r.x += r.width;  r.width  = -r.width; }
            if (r.height < 0) { r.y += r.height; r.height = -r.height; }
            return r;
        }

        private static void DrawBorder(Rect r, Color color, float thickness = 1f)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        private static GUIStyle GetCenteredStyle(int fontSize)
            => new GUIStyle(GUI.skin.label)
            {
                fontSize  = fontSize,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

        private BehaviourNodeMetaAttribute GetMeta(NodeData nd)
        {
            var type = Type.GetType(nd.typeName);
            if (type == null) return null;
            return type.GetCustomAttributes(typeof(BehaviourNodeMetaAttribute), false)
                       .FirstOrDefault() as BehaviourNodeMetaAttribute;
        }

        private static string GetShortTypeName(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName)) return "Unknown";
            var parts = assemblyQualifiedName.Split(',');
            var fullName = parts[0].Trim();
            var dotIdx = fullName.LastIndexOf('.');
            return dotIdx >= 0 ? fullName.Substring(dotIdx + 1) : fullName;
        }

        private static NodeData CloneNodeData(NodeData src)
            => new NodeData
            {
                guid          = src.guid,
                typeName      = src.typeName,
                positionX     = src.positionX,
                positionY     = src.positionY,
                childrenGuids = new List<string>(src.childrenGuids),
                childGuid     = src.childGuid,
                parametersJson = src.parametersJson
            };

        private void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>全CompositeノードのchildrenGuidsをX座標昇順（左優先）に並べ替える</summary>
        private void SortChildrenByPosition()
        {
            if (_treeData == null) return;
            foreach (var nd in _treeData.nodes)
            {
                if (nd.childrenGuids.Count <= 1) continue;
                nd.childrenGuids.Sort((a, b) =>
                {
                    float ax = _nodeRects.TryGetValue(a, out var ra) ? ra.x : 0f;
                    float bx = _nodeRects.TryGetValue(b, out var rb) ? rb.x : 0f;
                    return ax.CompareTo(bx);
                });
            }
        }

        private void NotifyInspector()
        {
            Repaint();
        }
    }
}
