using ModuTree.Runtime.Core;
using ModuTree.Runtime.Engine;
using ModuTree.UnityIntegration;
using UnityEngine;
using UnityEngine.UI;

namespace ModuTree.JankenSample
{
    /// <summary>
    /// ジャンケンサンプルのエントリポイント。
    /// BehaviourTreeRunner を継承しているため、Play中に Hierarchy で選択すると
    /// ModuTree Editor ウィンドウでノードの実行状態を確認できる。
    ///
    /// Update() の自動実行は無効化し、ボタン押下時に1回だけ Engine を動かす。
    /// </summary>
    public class JankenRunner : BehaviourTreeRunner
    {
        [Header("UI")]
        [SerializeField] private Button rockButton;      // グー
        [SerializeField] private Button scissorsButton;  // チョキ
        [SerializeField] private Button paperButton;     // パー

        [SerializeField] private Text resultText;        // 結果表示テキスト
        [SerializeField] private Text thinkingText;      // 思考中テキスト

        protected override void Awake()
        {
            base.Awake();

            // ボタンにコールバックを登録
            rockButton.onClick.AddListener(()     => OnPlayerChose(JankenHand.Rock));
            scissorsButton.onClick.AddListener(() => OnPlayerChose(JankenHand.Scissors));
            paperButton.onClick.AddListener(()    => OnPlayerChose(JankenHand.Paper));

            resultText.text   = "グー・チョキ・パーのどれかを選んでください";
            thinkingText.text = "";
        }

        /// <summary>毎フレームの自動実行は行わない（ワンショット専用）</summary>
        protected override void Update() { }

        /// <summary>Blackboard の初期値を設定する</summary>
        protected override void SetupBlackboard(Blackboard blackboard)
        {
            blackboard.Set(JankenBBKeys.PlayerHand, JankenHand.Rock);
            blackboard.Set(JankenBBKeys.AiHand, JankenHand.Rock);
        }

        /// <summary>プレイヤーが手を選んだときに呼ばれる</summary>
        private async void OnPlayerChose(JankenHand playerHand)
        {
            // ボタン操作を無効化（AI思考中の連打を防ぐ）
            SetButtonsInteractable(false);

            Engine.Blackboard.Set(JankenBBKeys.PlayerHand, playerHand);

            resultText.text   = $"あなた: {HandToJapanese(playerHand)}";
            thinkingText.text = "AI が考えています...";

            // BehaviourTree をリセットし、Success/Failure になるまで回し続ける
            // （SequenceNodeData は子1つ進むごとに Running を返すため複数ステップ必要）
            Engine.Reset();
            NodeState state;
            do
            {
                state = await Engine.UpdateAsync(_cts.Token);
            }
            while (state == NodeState.Running && !_cts.Token.IsCancellationRequested);

            var aiHand = Engine.Blackboard.Get(JankenBBKeys.AiHand);
            thinkingText.text = "AI の手は " + HandToJapanese(aiHand) + " です！";
            resultText.text   =
                $"あなた: {HandToJapanese(playerHand)}  vs  AI: {HandToJapanese(aiHand)}\n" +
                "AI の勝ち！";

            SetButtonsInteractable(true);
        }

        private void SetButtonsInteractable(bool value)
        {
            rockButton.interactable     = value;
            scissorsButton.interactable = value;
            paperButton.interactable    = value;
        }

        private static string HandToJapanese(JankenHand hand) => hand switch
        {
            JankenHand.Rock     => "グー",
            JankenHand.Scissors => "チョキ",
            JankenHand.Paper    => "パー",
            _                   => "?"
        };
    }
}