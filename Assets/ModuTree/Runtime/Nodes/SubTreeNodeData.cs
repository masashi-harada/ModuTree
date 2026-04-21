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
        description: "外部JSONファイルを参照するサブツリー。\n親ツリーのBlackboardを共有する。",
        color:       "#4A3A7A")]
    public class SubTreeNodeData : BehaviourNodeData
    {
        [NodeField("AIデータパス", "参照するBehaviourTree JSONの絶対パス")]
        public string subTreeJsonPath = "";

        private BehaviourTreeEngine _subEngine;

        protected override Task OnStartAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(subTreeJsonPath) || !File.Exists(subTreeJsonPath))
                throw new FileNotFoundException(
                    $"サブツリーJSONが見つかりません: {subTreeJsonPath}");

            if (_subEngine == null)
            {
                // 親のBlackboardを共有してサブエンジンを生成
                _subEngine = new BehaviourTreeEngine(Blackboard);
                var json = File.ReadAllText(subTreeJsonPath);
                _subEngine.Initialize(json);
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