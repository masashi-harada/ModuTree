<?php
declare(strict_types=1);

/**
 * AIが出す手を Blackboard に書き込むアクション。
 * hand フィールドで指定した手を aiHand キーにセットして Success を返す。
 *
 * JSONパラメータ例:
 *   {"hand": 2}  // JankenHand::Paper（パー）でグーに勝つ
 *   {"hand": 0}  // JankenHand::Rock（グー）でチョキに勝つ
 *   {"hand": 1}  // JankenHand::Scissors（チョキ）でパーに勝つ
 */
class SelectHandAction extends ActionNodeData
{
    /** AIが出す手。JSONから自動復元される（int→JankenHand変換）。 */
    public JankenHand $hand = JankenHand::Rock;

    protected function onUpdate(): NodeState
    {
        $this->blackboard->set(JankenBBKeys::$aiHand, $this->hand);
        return NodeState::Success;
    }
}