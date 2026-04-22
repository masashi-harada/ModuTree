using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModuTree.Runtime.Core;
using ModuTree.Runtime.Engine;

namespace ModuTree.Runtime.Nodes
{
    /// <summary>
    /// 外部JSONファイルを参照するサブツリーノード。
    /// 親ツリーのBlackboardを共有する。
    /// ツリーの複雑さを分割管理するために使用する。
    /// </summary>
    [BehaviourNodeMeta(
        displayName: "Sub Tree",
        category:    "SubTree",
        description: "外部JSONファイルを参照するサブツリー。\n親ツリーのBlackboardを共有する。\nパスは親JSONファイルからの相対パスで指定する。",
        color:       "#4A3A7A")]
    public class SubTreeNodeData : BehaviourNodeData
    {
        [NodeField("AIデータパス", "親JSONファイルからの相対パス（例: SubTree.json / ../Other/SubTree.json）")]
        public string subTreeJsonPath = "";

        private BehaviourTreeEngine _subEngine;

        /// <summary>
        /// subTreeJsonPath を BaseDirectory 基準で解決した絶対パスを返す。
        /// 既に絶対パスの場合はそのまま返す（後方互換）。
        /// </summary>
        private string ResolvedPath
        {
            get
            {
                if (string.IsNullOrEmpty(subTreeJsonPath)) return "";
                if (Path.IsPathRooted(subTreeJsonPath))   return subTreeJsonPath;
                if (string.IsNullOrEmpty(BaseDirectory))  return subTreeJsonPath;
                return Path.GetFullPath(Path.Combine(BaseDirectory, subTreeJsonPath));
            }
        }

        protected override Task OnStartAsync(CancellationToken ct)
        {
            var resolved = ResolvedPath;
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved))
                throw new FileNotFoundException(
                    $"サブツリーJSONが見つかりません: {resolved}  (元パス: {subTreeJsonPath})");

            if (_subEngine == null)
            {
                // 親のBlackboardを共有してサブエンジンを生成
                // サブエンジンにもベースディレクトリを渡す（ネスト対応）
                var subBaseDir = Path.GetDirectoryName(resolved) ?? "";
                _subEngine = new BehaviourTreeEngine(Blackboard);
                var json = File.ReadAllText(resolved);
                _subEngine.Initialize(json, subBaseDir);
            }
            else
            {
                // 2回目以降は状態のみリセット
                _subEngine.Reset();
            }

            return Task.CompletedTask;
        }

        protected override async Task<NodeState> OnUpdateAsync(CancellationToken ct)
        {
            if (_subEngine == null) return NodeState.Failure;
            return await _subEngine.UpdateAsync(ct);
        }

        protected override async Task OnAbortAsync(CancellationToken ct)
        {
            if (_subEngine != null)
                await _subEngine.AbortAsync(ct);
        }

        /// <summary>サブツリーのエンジンインスタンス（エディタのモニタリング用）</summary>
        public BehaviourTreeEngine SubEngine => _subEngine;
    }
}