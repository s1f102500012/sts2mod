using Godot;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen
{
	private const string LocTable = "relic_collection";
	private const string RerollButtonTexturePath = "res://HextechRunes/images/ui/hextechRerollButton.png";
	private const string RerollButtonHoverTexturePath = "res://HextechRunes/images/ui/hextechRerollButtonHover.png";
	private const string RerollButtonUsedTexturePath = "res://HextechRunes/images/ui/hextechRerollButtonUsed.png";
	private const string GoldenRerollOuterMaskPath = "res://HextechRunes/images/ui/reroll_button_gold_1.png";
	private const string GoldenRerollFillMaskPath = "res://HextechRunes/images/ui/reroll_button_gold_2.png";
	private const string RerollButtonSfxPath = "res://HextechRunes/audio/hextechReroll.wav";
	private const string SelectSilverSfxPath = "res://HextechRunes/audio/hextechSelectSilver.wav";
	private const string SelectGoldSfxPath = "res://HextechRunes/audio/hextechSelectGold.wav";
	private const string SelectPrismaticSfxPath = "res://HextechRunes/audio/hextechSelectPrismatic.wav";
	private const string SilverCardFramePath = "res://HextechRunes/images/ui/augmentcard_frame_silver.png";
	private const string GoldCardFramePath = "res://HextechRunes/images/ui/augmentcard_frame_gold.png";
	private const string PrismaticCardFramePath = "res://HextechRunes/images/ui/augmentcard_frame_prismatic.png";

	private static readonly Vector2 PlayerRuneCardSize = new(344f, 592f);
	private const int PlayerRuneCardBottomMargin = 112;
	private const float PlayerRerollButtonTextureWidth = 76f;
	private const float PlayerRerollButtonTextureHeight = 46f;
	private const float PlayerRerollButtonHeight = 76f;
	private static readonly Vector2 PlayerRerollButtonSize = new(PlayerRerollButtonHeight * PlayerRerollButtonTextureWidth / PlayerRerollButtonTextureHeight, PlayerRerollButtonHeight);
	private const float GoldenRerollSourceScale = PlayerRerollButtonHeight / PlayerRerollButtonTextureHeight;
	private const float PlayerRerollButtonBottomInset = 38f;
	private const float RerollButtonSfxVolumeScale = 0.42f;
	private const float SelectSfxVolumeScale = 0.40f;
	private const ulong SelectionConfirmGuardDurationMsec = 1000;
}
