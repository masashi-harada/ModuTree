<?php
declare(strict_types=1);

/**
 * プレイヤーが指定の手を選んでいるかチェックするコンディション。
 * targetHand と Blackboard の playerHand が一致すれば Success を返す。
 *
 * JSONパラメータ例:
 *   {"targetHand": 0}  // JankenHand::Rock
 *   {"targetHand": 1}  // JankenHand::Scissors
 *   {"targetHand": 2}  // JankenHand::Paper
 */
class IsPlayerChoseCondition extends ConditionNodeData
{
    /** 比較対象の手。JSONから自動復元される（int→JankenHand変換）。 */
    public JankenHand $targetHand = JankenHand::Rock;

    protected function onUpdate(): NodeState
    {
        /** @var JankenHand|null $playerHand */
        $playerHand = $this->blackboard->get(JankenBBKeys::$playerHand);
        return $playerHand === $this->targetHand
            ? NodeState::Success
            : NodeState::Failure;
    }
}