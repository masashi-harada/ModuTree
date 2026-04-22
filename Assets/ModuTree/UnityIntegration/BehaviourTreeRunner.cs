using System.IO;
using System.Threading;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Engine;
using UnityEngine;

namespace ModuTree.UnityIntegration
{
    /// <summary>
    /// BehaviourTreeEngineをUnityで動かすためのMonoBehaviour。
    /// このクラスを継承して SetupBlackboard() をオーバーライドして使う。
    ///
    /// ノード内で Transform.Translate などのUnity APIを直接呼ばず、
    /// Blackboardに指示を書き込み、このRunner側で実行すること。
    /// </summary>
    public abstract class BehaviourTreeRunner : MonoBehaviour
    {
        [Header("BehaviourTree JSON")]
        public TextAsset behaviourTreeJson;

        // JSONファイルのベースディレクトリ（相対パス解決用）
        // OnValidate で自動更新されるため手動編集不要
        [SerializeField, HideInInspector] private string _baseDirectory = "";

        /// <summary>エンジンへのアクセス</summary>
        public BehaviourTreeEngine Engine { get; private set; }

        protected CancellationTokenSource _cts;

        protected virtual void Awake()
        {
            if (behaviourTreeJson == null)
            {
                Debug.LogError("[ModuTree] BehaviourTree JSONが未設定です", this);
                enabled = false;
                return;
            }

            _cts   = new CancellationTokenSource();
            Engine = new BehaviourTreeEngine();
            SetupBlackboard(Engine.Blackboard);
            Engine.Initialize(behaviourTreeJson.text, _baseDirectory);
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // behaviourTreeJsonのアセットパスからベースディレクトリを更新する
            if (behaviourTreeJson != null)
            {
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(behaviourTreeJson);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var absPath = Path.GetFullPath(
                        Path.Combine(Application.dataPath, "..", assetPath));
                    _baseDirectory = Path.GetDirectoryName(absPath) ?? "";
                }
            }
            else
            {
                _baseDirectory = "";
            }
        }
#endif

        protected virtual async void Update()
        {
            if (Engine == null) return;
            // 毎フレーム非同期で1ステップ実行
            await Engine.UpdateAsync(_cts.Token);
        }

        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            Engine?.Dispose();
        }

        /// <summary>
        /// Blackboardの初期設定をここで行う。
        /// 例: blackboard.Set(MyKeys.Agent, transform);
        /// </summary>
        protected virtual void SetupBlackboard(Blackboard blackboard) { }

        /// <summary>
        /// 実行中に別のJSONに切り替える（ホットリロード用）。
        /// 全ノードをリセットして最初から再実行する。
        /// </summary>
        public void ReloadJson(string json)
        {
            Engine?.Initialize(json, _baseDirectory);
        }
    }
}