namespace HextechRunes;

// 棱彩蛋 —— 你宝箱房内的遗物奖励会被替换为随机的海克斯符文。
// 效果实现在 HextechTreasureRuneHooks:开箱走 TreasureRoomRelicSynchronizer 的
// 「roll→投票选牌位→发放」小游戏,不经过 TryModifyRewards,须在发放前按选位替换。
public sealed class PrismaticEggRune : HextechRelicBase
{
}
