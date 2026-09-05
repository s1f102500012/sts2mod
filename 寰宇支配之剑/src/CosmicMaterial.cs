using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace UniversalDominionSword;

/// <summary>
/// 遗物图标的实时星空材质:剑身两层、遮罩与十帧星场全部从 PCK 经 <see cref="ResourceLoader"/> 读取,
/// 不手工创建资源,也不设 ResourcePath(手工资源会被原版 AssetCache 捡走并 Dispose)。
/// </summary>
internal static class CosmicMaterial
{
	private static Shader? _shader;

	private static bool _loggedFailure;

	/// <summary>把材质套到一个 TextureRect 上;已套过则只刷新过滤模式。失败只记一次日志,表现层不抛。</summary>
	internal static bool TryApply(TextureRect rect)
	{
		try
		{
			rect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			if (IsApplied(rect))
			{
				return true;
			}

			rect.Material = Create();
			return true;
		}
		catch (Exception exception)
		{
			if (!_loggedFailure)
			{
				_loggedFailure = true;
				Log.Warn($"[{ModInfo.Id}] Cosmic relic material unavailable; falling back to the static icon: {exception.GetType().Name}: {exception.Message}");
			}

			return false;
		}
	}

	internal static bool IsApplied(TextureRect rect)
	{
		return _shader != null
			&& rect.Material is ShaderMaterial material
			&& material.Shader == _shader;
	}

	private static ShaderMaterial Create()
	{
		_shader ??= new Shader { Code = AvaritiaCosmicShader.Code };

		ShaderMaterial material = new() { Shader = _shader };
		material.SetShaderParameter("layer_0", Load(ModInfo.Layer0Path));
		material.SetShaderParameter("layer_1", Load(ModInfo.Layer1Path));
		material.SetShaderParameter("blade_mask", Load(ModInfo.MaskPath));
		for (int index = 0; index < ModInfo.CosmicFrameCount; index++)
		{
			material.SetShaderParameter($"cosmic_{index}", Load(ModInfo.CosmicPath(index)));
		}

		return material;
	}

	private static Texture2D Load(string path)
	{
		return ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse)
			?? throw new FileNotFoundException("Mod texture is missing from the PCK.", path);
	}
}
