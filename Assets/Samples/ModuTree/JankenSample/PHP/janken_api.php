<?php
declare(strict_types=1);

/**
 * ジャンケンAI APIエンドポイントのサンプル。
 *
 * リクエスト (POST, JSON):
 *   { "hand": 0 }  // 0=グー, 1=チョキ, 2=パー
 *
 * レスポンス (JSON):
 *   {
 *     "playerHand": 0,
 *     "playerHandLabel": "グー",
 *     "aiHand": 2,
 *     "aiHandLabel": "パー",
 *     "result": "lose"   // "win" | "lose" | "draw"
 *   }
 *
 * 使い方（PHP CLI でのテスト）:
 *   php -r "
 *     \$_SERVER['REQUEST_METHOD'] = 'POST';
 *     \$_POST['hand'] = 0;
 *     include 'janken_api.php';
 *   "
 */

// ─── ランタイムのロード ──────────────────────────────
// このファイルから Runtime/PHP/ の場所を解決する
// 実際のプロジェクトではサーバのディレクトリ構造に合わせてパスを調整すること
$runtimePath = __DIR__ . '/../../../../ModuTree/Runtime/PHP~/autoload.php';
require_once $runtimePath;

// ─── サンプルノードのロード ──────────────────────────
require_once __DIR__ . '/Keys/JankenBBKeys.php';
require_once __DIR__ . '/Nodes/IsPlayerChoseCondition.php';
require_once __DIR__ . '/Nodes/SelectHandAction.php';

// ─── リクエストの解析 ────────────────────────────────
header('Content-Type: application/json; charset=utf-8');

$requestBody = file_get_contents('php://input');
$requestData = !empty($requestBody) ? json_decode($requestBody, true) : [];

// GETパラメータもフォールバックとして受け付ける（テスト用）
$handInt = (int)($requestData['hand'] ?? $_GET['hand'] ?? 0);

try {
    $playerHand = JankenHand::from($handInt);
} catch (\ValueError) {
    http_response_code(400);
    echo json_encode(['error' => "無効な手の値です: {$handInt}。0=グー, 1=チョキ, 2=パー"]);
    exit;
}

// ─── BehaviourTreeEngine の実行 ──────────────────────
$engine = new BehaviourTreeEngine();

// プレイヤーの手をBlackboardにセット
$engine->blackboard->set(JankenBBKeys::$playerHand, $playerHand);

// AIデータJSONの読み込みと初期化
// このサンプルではUnityで作ったJSONをそのまま使用する
$jsonPath = __DIR__ . '/../JankenBehaviourTree.json';
if (!file_exists($jsonPath)) {
    http_response_code(500);
    echo json_encode(['error' => "AIデータが見つかりません: {$jsonPath}"]);
    exit;
}

$engine->initialize(
    file_get_contents($jsonPath),
    dirname($jsonPath)
);

// ツリーをSuccessまたはFailureになるまで実行（ワンショット実行）
$state = $engine->runToCompletion();

// ─── 結果の取得 ──────────────────────────────────────
/** @var JankenHand|null $aiHand */
$aiHand = $engine->blackboard->get(JankenBBKeys::$aiHand);

if ($state !== NodeState::Success || $aiHand === null) {
    http_response_code(500);
    echo json_encode(['error' => 'AIの思考に失敗しました']);
    exit;
}

// 勝敗判定
$result = determineResult($playerHand, $aiHand);

echo json_encode([
    'playerHand'      => $playerHand->value,
    'playerHandLabel' => $playerHand->label(),
    'aiHand'          => $aiHand->value,
    'aiHandLabel'     => $aiHand->label(),
    'result'          => $result,
]);

// ─── 勝敗判定ロジック ────────────────────────────────

/**
 * プレイヤー視点の勝敗を判定する。
 *
 * @return string "win" | "lose" | "draw"
 */
function determineResult(JankenHand $player, JankenHand $ai): string
{
    if ($player === $ai) return 'draw';

    $playerWins = match ($player) {
        JankenHand::Rock     => $ai === JankenHand::Scissors,
        JankenHand::Scissors => $ai === JankenHand::Paper,
        JankenHand::Paper    => $ai === JankenHand::Rock,
    };

    return $playerWins ? 'win' : 'lose';
}