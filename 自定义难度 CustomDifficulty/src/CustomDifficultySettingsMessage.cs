using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CustomDifficulty;

public sealed class CustomDifficultySettingsMessage : INetMessage, IPacketSerializable
{
	// 每房间增量取值 -20..20，网络编码统一加 100 存为 byte（80..120）。
	private const int DeltaEncodeOffset = 100;

	public byte HpTicks { get; set; }

	public byte AttackTicks { get; set; }

	public byte DifficultyMode { get; set; }

	public byte EncodedHpDeltaPercentPerRoom { get; set; } = DeltaEncodeOffset;

	public byte EncodedAttackDeltaPercentPerRoom { get; set; } = DeltaEncodeOffset;

	public int HpDeltaPercentPerRoom
	{
		get => EncodedHpDeltaPercentPerRoom - DeltaEncodeOffset;
		set => EncodedHpDeltaPercentPerRoom = (byte)(CustomDifficultySettings.ClampDeltaPercent(value) + DeltaEncodeOffset);
	}

	public int AttackDeltaPercentPerRoom
	{
		get => EncodedAttackDeltaPercentPerRoom - DeltaEncodeOffset;
		set => EncodedAttackDeltaPercentPerRoom = (byte)(CustomDifficultySettings.ClampDeltaPercent(value) + DeltaEncodeOffset);
	}

	public bool ShouldBroadcast => false;

	public bool ShouldBuffer => true;

	public NetTransferMode Mode => NetTransferMode.Reliable;

	public LogLevel LogLevel => LogLevel.Debug;

	public void Serialize(PacketWriter writer)
	{
		writer.WriteByte(HpTicks);
		writer.WriteByte(AttackTicks);
		writer.WriteByte(DifficultyMode);
		writer.WriteByte(EncodedHpDeltaPercentPerRoom);
		writer.WriteByte(EncodedAttackDeltaPercentPerRoom);
	}

	public void Deserialize(PacketReader reader)
	{
		HpTicks = reader.ReadByte();
		AttackTicks = reader.ReadByte();
		DifficultyMode = reader.ReadByte();
		EncodedHpDeltaPercentPerRoom = reader.ReadByte();
		EncodedAttackDeltaPercentPerRoom = reader.ReadByte();
	}
}
