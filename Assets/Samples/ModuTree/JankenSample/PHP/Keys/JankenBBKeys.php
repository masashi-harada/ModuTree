<?php
declare(strict_types=1);

/**
 * ジャンケンの手を表す列挙型。
 * C#版と同じ整数値を持つ int-backed enum を使用する。
 * JSONには整数値として保存される（0=Rock, 1=Scissors, 2=Paper）。
 */
enum JankenHand: int
{
    case Rock     = 0;  // グー
    case Scissors = 1;  // チョキ
    case Paper    = 2;  // パー

    /** 日本語名を返す */
    public function label(): string
    {
        return match ($this) {
            self::Rock     => 'グー',
            self::Scissors => 'チョキ',
            self::Paper    => 'パー',
        };
    }
}

/**
 * ジャンケンサンプル用 Blackboard キー定義。
 * C#版の BlackboardKey<T> に相当する。
 * PHPにはジェネリクスがないため型はPHPDoc で補完する。
 */
class JankenBBKeys
{
    /** @var BlackboardKey プレイヤーが選んだ手（JankenHand） */
    public static BlackboardKey $playerHand;

    /** @var BlackboardKey AIが選んだ手（JankenHand）— ノードが書き込む */
    public static BlackboardKey $aiHand;

    /** @var BlackboardKey AI思考中フラグ（bool） */
    public static BlackboardKey $isThinking;
}

// ファイル読み込み時に静的プロパティを初期化する
JankenBBKeys::$playerHand  = new BlackboardKey('playerHand');
JankenBBKeys::$aiHand      = new BlackboardKey('aiHand');
JankenBBKeys::$isThinking  = new BlackboardKey('isThinking');