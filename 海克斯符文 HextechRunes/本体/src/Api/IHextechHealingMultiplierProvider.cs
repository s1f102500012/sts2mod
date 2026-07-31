namespace HextechRunes;

/// <summary>
/// 为玩家治疗量提供乘区系数。
/// </summary>
/// <remarks>
/// 实现必须是只依赖同步模型状态的纯函数,不得读取本地 UI、Godot 节点或其他仅单端可见的状态。
/// </remarks>
public interface IHextechHealingMultiplierProvider
{
	decimal ModifyHealingMultiplicative(Player player, Creature creature, decimal amount);
}
