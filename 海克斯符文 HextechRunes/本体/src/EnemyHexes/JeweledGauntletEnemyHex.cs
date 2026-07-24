namespace HextechRunes;

// 敌方「珠光护手」需要在怪物行动被锁定后做确定性判定，同时改写头顶意图预览并包装
// MonsterModel.PerformMove 的异步结果；标准 effect hook 无法表达，因此真实实现位于
// HextechCombatHooks.JeweledGauntlet。本类仅用于注册与图鉴/描述登记。
internal sealed class JeweledGauntletEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.JeweledGauntlet;
}
