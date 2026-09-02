using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal sealed class HextechBurnVisual
{
	private const string NodeName = "HextechRunes_BurnFlames";
	private const float BurnAmountForFullIntensity = 12f;
	// 强度平滑:约 0.2-0.4s 从熄灭到全强(或反向);低于该值视为完全熄灭。
	private const float IntensityLerpPerSecond = 4f;
	private const float ExtinguishedEpsilon = 0.02f;

	private static readonly HashSet<ulong> ActiveCreatureNodes = [];
	private static readonly Random VisualRng = new();
	private static Texture2D? _particleTexture;

	private readonly NCreature _creature;
	private Node2D? _backRoot;
	private Node2D? _frontRoot;
	private CpuParticles2D? _flames;
	private CpuParticles2D? _flamesFront;
	private CpuParticles2D? _smoke;
	private CpuParticles2D? _sparks;
	// Spine 骨骼发射:火焰/火星发射点每帧跟随骨骼位置(火贴着身体各部位烧、随动画摆动);
	// 立绘无 Spine 或原生 API 缺失时保持静态矩形发射带。
	// 实测(headless 探针):SpineBoneData.get_name 返回空串,get_global_bone_transform 按名查找
	// 永远失败并回退成 sprite 原点(火聚成一个固定点的事故根因)——所以必须缓存骨骼对象、
	// 每帧读 get_world_x/get_world_y(骨架本地坐标,y 已是 Godot 朝向)再经 sprite.ToGlobal 转换。
	private Node2D? _spineSpriteNode;
	private ulong _spineSpriteInstanceId;
	private GodotObject[]? _emissionBones;
	private float _smoothIntensity;

	private HextechBurnVisual(NCreature creature)
	{
		_creature = creature;
	}

	internal static void TryAttach(NCreature? creature)
	{
		try
		{
			if (!GodotObject.IsInstanceValid(creature)
				|| !creature.IsNodeReady()
				|| creature.Hitbox == null
				|| creature.Entity == null)
			{
				return;
			}

			ulong creatureInstanceId = creature.GetInstanceId();
			if (!ActiveCreatureNodes.Add(creatureInstanceId))
			{
				return;
			}

			HextechBurnVisual visual = new(creature);
			if (!visual.Start())
			{
				ActiveCreatureNodes.Remove(creatureInstanceId);
				return;
			}

			TaskHelper.RunSafely(visual.RunAsync(creatureInstanceId));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Burn] Could not attach burn flames visual: {ex.Message}");
		}
	}

	private static bool TryGetIntensity(NCreature creature, out float intensity)
	{
		intensity = 0f;
		if (creature.Entity is not { IsAlive: true } entity || !entity.HasPower<HextechBurnPower>())
		{
			return false;
		}

		int amount = entity.GetPowerAmount<HextechBurnPower>();
		if (amount <= 0)
		{
			return false;
		}

		intensity = Mathf.Clamp(amount / BurnAmountForFullIntensity, 0.35f, 1f);
		return true;
	}

	private bool Start()
	{
		if (_creature.Hitbox == null)
		{
			return false;
		}

		float width = Mathf.Clamp(_creature.Hitbox.Size.X, 100f, 360f);
		float height = Mathf.Abs(_creature.GetTopOfHitbox().Y - _creature.GetBottomOfHitbox().Y);

		// 火焰主体与烟画在生物身后,火星画在身前——形成"被火包裹"的前后层次。
		// 不能用负 ZIndex 表达"身后":Godot 2D 的 z 排序是全局比较,z=-1 无论挂在哪都会沉到
		// 全局 z=0 的场景背景之下(实测两次只剩 z=+1 的火星可见)。正确做法:全部保持 z=0、
		// 挂进 NCreature 内部,用子节点树顺序控制前后(back 移到 index 0 → 先画 → 被立绘盖住;
		// front 追加在末尾 → 后画 → 盖在立绘上)。顺带天然跟随受击闪烁与死亡淡出的 modulate。
		_backRoot = new Node2D { Name = NodeName + "_Back", Visible = false };
		_frontRoot = new Node2D { Name = NodeName + "_Front", Visible = false };
		_creature.AddChildSafely(_backRoot);
		_creature.AddChildSafely(_frontRoot);
		_creature.MoveChild(_backRoot, 0);

		_flames = CreateFlames(width, height);
		// 高大立绘会把身后的火焰完全挡死(实测法师类敌人几乎看不到火);身前再叠一层
		// 缩小调淡的火焰,保证任何体型下"身上着火"都可读,又不至于糊住立绘细节。
		_flamesFront = CreateFlames(width, height, front: true);
		_smoke = CreateSmoke(width, height);
		_sparks = CreateSparks(width, height);
		_backRoot.AddChild(_flames);
		_backRoot.AddChild(_smoke);
		_frontRoot.AddChild(_flamesFront);
		_frontRoot.AddChild(_sparks);
		TryInitBoneEmission();
		return true;
	}

	private async Task RunAsync(ulong creatureInstanceId)
	{
		try
		{
			while (GodotObject.IsInstanceValid(_creature) && GodotObject.IsInstanceValid(_backRoot) && GodotObject.IsInstanceValid(_frontRoot))
			{
				float dt = Mathf.Clamp((float)_backRoot!.GetProcessDeltaTime(), 1f / 120f, 0.05f);
				float target = TryGetIntensity(_creature, out float intensity) ? intensity : 0f;
				_smoothIntensity = Mathf.MoveToward(_smoothIntensity, target, IntensityLerpPerSecond * dt);
				ApplyIntensity(target > 0f);

				SceneTree tree = _backRoot.GetTree();
				if (!GodotObject.IsInstanceValid(tree))
				{
					return;
				}

				await _backRoot.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Burn] Burn flames visual stopped after runtime error: {ex.Message}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(_backRoot))
			{
				_backRoot.QueueFree();
			}

			if (GodotObject.IsInstanceValid(_frontRoot))
			{
				_frontRoot.QueueFree();
			}

			ActiveCreatureNodes.Remove(creatureInstanceId);
		}
	}

	private void ApplyIntensity(bool burning)
	{
		if (_backRoot == null || _frontRoot == null || _flames == null || _flamesFront == null || _smoke == null || _sparks == null)
		{
			return;
		}

		float smooth = _smoothIntensity;
		if (smooth <= ExtinguishedEpsilon && !burning)
		{
			// 完全熄灭:停发射并隐藏(此时整体透明度已归零,瞬隐不可见)。
			_backRoot.Visible = false;
			_frontRoot.Visible = false;
			_flames.Emitting = false;
			_flamesFront.Emitting = false;
			_smoke.Emitting = false;
			_sparks.Emitting = false;
			return;
		}

		Vector2 bottom = _creature.GetBottomOfHitbox();
		_backRoot.GlobalPosition = bottom;
		_frontRoot.GlobalPosition = bottom;
		_backRoot.Visible = true;
		_frontRoot.Visible = true;
		UpdateBoneEmissionPoints();

		// 淡出阶段(burning=false、smooth>0)停止发射,存量粒子随整体透明度自然熄灭。
		_flames.Emitting = burning;
		_flamesFront.Emitting = burning;
		_smoke.Emitting = burning && smooth > 0.4f;
		_sparks.Emitting = burning && smooth > 0.55f;

		Color tint = Colors.White with { A = Mathf.Clamp(smooth * 1.35f, 0f, 1f) };
		_backRoot.Modulate = tint;
		_frontRoot.Modulate = tint;

		// 火势随层数:燃速与整体幅度轻微变化(发射几何在 Start 按体型定死,避免每帧重建)。
		// 基线压慢:大而慢的粒子才像"火贴着身体烧",快了会读成向上喷的气流。
		float speed = Mathf.Lerp(0.65f, 1.0f, smooth);
		_flames.SpeedScale = speed;
		_flamesFront.SpeedScale = speed;
		_sparks.SpeedScale = speed;
		_smoke.SpeedScale = Mathf.Lerp(0.7f, 0.95f, smooth);
		float scale = Mathf.Lerp(0.85f, 1.12f, smooth);
		_backRoot.Scale = Vector2.One * scale;
		_frontRoot.Scale = Vector2.One * scale;
	}

	/// <summary>
	/// 缓存 Spine 骨架的骨骼对象,供发射点每帧跟随。原生 spine-godot API 通过 BoundObject.Call
	/// 访问,任何缺失都静默回退矩形发射。
	/// </summary>
	private void TryInitBoneEmission()
	{
		try
		{
			GodotObject? sprite = _creature.Visuals?.SpineBody?.BoundObject;
			if (sprite is not Node2D spriteNode || !GodotObject.IsInstanceValid(spriteNode) || !spriteNode.HasMethod("get_skeleton"))
			{
				return;
			}

			if (spriteNode.Call("get_skeleton").AsGodotObject() is not { } skeleton || !skeleton.HasMethod("get_bones"))
			{
				return;
			}

			List<GodotObject> bones = [];
			foreach (Variant boneVariant in skeleton.Call("get_bones").AsGodotArray())
			{
				if (boneVariant.AsGodotObject() is not { } bone || !bone.HasMethod("get_world_x"))
				{
					continue;
				}

				bones.Add(bone);
				if (bones.Count >= 32)
				{
					break;
				}
			}

			// 防 use-after-free:枚举期间保持 skeleton 包装存活(MegaSpineBinding 的 remarks 所述陷阱)。
			GC.KeepAlive(skeleton);
			if (bones.Count >= 3)
			{
				_spineSpriteNode = spriteNode;
				_spineSpriteInstanceId = spriteNode.GetInstanceId();
				_emissionBones = [.. bones];
			}
		}
		catch (Exception ex)
		{
			DisableBoneEmission();
			Log.Warn($"[{ModInfo.Id}][Burn] Bone emission init failed, using rect emission: {ex.Message}");
		}
	}

	private void DisableBoneEmission()
	{
		_spineSpriteNode = null;
		_emissionBones = null;
	}

	/// <summary>每帧把火焰/火星的发射点更新为当前骨骼位置(root 局部坐标),火随动画摆动。</summary>
	private void UpdateBoneEmissionPoints()
	{
		if (_spineSpriteNode == null || _emissionBones == null
			|| _backRoot == null || _flames == null || _flamesFront == null || _sparks == null)
		{
			return;
		}

		// 立绘被释放或替换(死亡、变身):骨骼包装指向的底层数据同时失效,必须立即停用,
		// 避免悬垂指针访问;下一帧会用新立绘重新初始化。
		if (!GodotObject.IsInstanceValid(_spineSpriteNode)
			|| _creature.Visuals?.SpineBody?.BoundObject is not { } current
			|| current.GetInstanceId() != _spineSpriteInstanceId)
		{
			DisableBoneEmission();
			TryInitBoneEmission();
			return;
		}

		try
		{
			Vector2 bottom = _creature.GetBottomOfHitbox();
			Vector2 top = _creature.GetTopOfHitbox();
			float height = bottom.Y - top.Y;
			// 只取下半身的骨骼点:火从腿部/下身烧起,不糊脸。底边收到脚底略上方,
			// 剔掉贴地的 root/地面控制骨(它们固定在脚下空处,会形成一个不动的悬空火点)。
			float lowerBodyTop = top.Y + height * 0.45f;
			float floorCutoff = bottom.Y - height * 0.02f;
			// 网格去重:骨骼扎堆的关节处只留一个发射点,发射概率在空间上大致均匀。
			float cellSize = Mathf.Max(12f, Mathf.Clamp(_creature.Hitbox?.Size.X ?? 200f, 100f, 360f) * 0.09f);
			HashSet<Vector2I> cells = [];
			List<Vector2> points = new(_emissionBones.Length);
			foreach (GodotObject bone in _emissionBones)
			{
				// 骨架本地坐标(y 已是 Godot 朝向),经立绘节点变换到全局(含缩放/朝向翻转)。
				Vector2 world = new(bone.Call("get_world_x").AsSingle(), bone.Call("get_world_y").AsSingle());
				if (world.Y >= 0f)
				{
					// 骨架原点在脚底、身体骨骼一律 y<0;y>=0 的是 root/地面/阴影类控制骨,
					// 不属于身体(实测机甲骑士有一根脚下 +400 的控制骨,发射后火飘到腿间空隙)。
					continue;
				}

				Vector2 point = _spineSpriteNode.ToGlobal(world);
				if (point.Y < lowerBodyTop || point.Y > floorCutoff)
				{
					continue;
				}

				if (!cells.Add(new Vector2I((int)MathF.Floor(point.X / cellSize), (int)MathF.Floor(point.Y / cellSize))))
				{
					continue;
				}

				// 发射点每帧小幅随机游移,燃烧位置活起来(纯视觉,不涉及联机决定论)。
				point += new Vector2((VisualRng.NextSingle() - 0.5f) * cellSize, (VisualRng.NextSingle() - 0.5f) * cellSize * 0.6f);
				points.Add(_backRoot.ToLocal(point));
			}

			if (points.Count < 3)
			{
				// 极端姿态下有效点太少:沿用上一帧点集,不抖动。
				return;
			}

			Vector2[] array = [.. points];
			SetEmissionPoints(_flames, array);
			SetEmissionPoints(_flamesFront, array);
			SetEmissionPoints(_sparks, array);
		}
		catch (Exception ex)
		{
			DisableBoneEmission();
			Log.Warn($"[{ModInfo.Id}][Burn] Bone emission update failed, keeping last emission: {ex.Message}");
		}
	}

	private static void SetEmissionPoints(CpuParticles2D particles, Vector2[] points)
	{
		// 骨骼点已是精确位置(root 局部空间),清除矩形模式下的整体偏移。
		particles.Position = Vector2.Zero;
		particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Points;
		particles.EmissionPoints = points;
	}

	private CpuParticles2D CreateFlames(float width, float height, bool front = false)
	{
		// 程序化软粒子火焰(渐变圆+加法混合)。粒子取"大而慢":大粒子相互重叠成连续火体、
		// 慢速上升读作"火贴着身体烧";小而快会读成向上喷的能量气流("爆气"感)。
		// front=true 生成身前层:更窄、更小、更透,保证高大立绘挡死身后火时依然可读。
		CpuParticles2D flames = ConfigureCommon(new CpuParticles2D
		{
			Amount = front ? 16 : 40,
			Lifetime = 1.05f,
			Preprocess = 1.1f,
			Randomness = 0.5f
		}, width * (front ? 0.13f : 0.18f), height * 0.1f);
		flames.Position = new Vector2(0f, -height * 0.22f);
		flames.InitialVelocityMin = height * 0.18f;
		flames.InitialVelocityMax = height * 0.38f;
		flames.Gravity = new Vector2(0f, -height * 0.26f);
		float sizeFactor = front ? 0.72f : 1f;
		flames.ScaleAmountMin = width * 0.0024f * sizeFactor;
		flames.ScaleAmountMax = width * 0.0040f * sizeFactor;
		flames.ScaleAmountCurve = MakeCurve((0f, 0.55f), (0.25f, 1f), (1f, 0.12f));
		flames.ColorRamp = MakeGradient(
			(0f, new Color(1f, 0.92f, 0.55f, 0f)),
			(0.12f, new Color(1f, 0.85f, 0.4f, 0.95f)),
			(0.45f, new Color(1f, 0.5f, 0.14f, 0.85f)),
			(0.8f, new Color(0.9f, 0.22f, 0.08f, 0.45f)),
			(1f, new Color(0.5f, 0.1f, 0.05f, 0f)));
		if (front)
		{
			flames.Modulate = Colors.White with { A = 0.55f };
		}

		return flames;
	}

	private CpuParticles2D CreateSmoke(float width, float height)
	{
		CpuParticles2D smoke = ConfigureCommon(new CpuParticles2D
		{
			Amount = 9,
			Lifetime = 1.5f,
			Preprocess = 1.2f,
			Randomness = 0.7f
		}, width * 0.16f, height * 0.06f, additive: false);
		smoke.InitialVelocityMin = height * 0.28f;
		smoke.InitialVelocityMax = height * 0.42f;
		smoke.Gravity = new Vector2(0f, -height * 0.22f);
		smoke.Position = new Vector2(0f, -height * 0.5f);
		// 机甲骑士喷火器的碎裂烟火团贴图,比圆形渐变更有翻滚烟感。
		Texture2D? smokeTex = LoadVanillaTexture("res://images/vfx/fire/mecha_knight_fire_particle.png");
		if (smokeTex != null)
		{
			smoke.Texture = smokeTex;
			smoke.ScaleAmountMin = width * 0.0011f;
			smoke.ScaleAmountMax = width * 0.0016f;
		}
		else
		{
			smoke.ScaleAmountMin = width * 0.002f;
			smoke.ScaleAmountMax = width * 0.003f;
		}
		smoke.ScaleAmountCurve = MakeCurve((0f, 0.5f), (1f, 1.25f));
		smoke.ColorRamp = MakeGradient(
			(0f, new Color(0.25f, 0.22f, 0.2f, 0f)),
			(0.25f, new Color(0.28f, 0.25f, 0.23f, 0.2f)),
			(1f, new Color(0.32f, 0.3f, 0.28f, 0f)));
		return smoke;
	}

	private CpuParticles2D CreateSparks(float width, float height)
	{
		CpuParticles2D sparks = ConfigureCommon(new CpuParticles2D
		{
			Amount = 10,
			Lifetime = 0.55f,
			Preprocess = 0.4f,
			Randomness = 0.9f
		}, width * 0.24f, height * 0.12f);
		sparks.Position = new Vector2(0f, -height * 0.25f);
		sparks.InitialVelocityMin = height * 0.5f;
		sparks.InitialVelocityMax = height * 1.0f;
		sparks.Gravity = new Vector2(0f, -height * 0.65f);
		sparks.Spread = 22f;
		// 官方余烬点贴图(24px 实心亮点)。
		Texture2D? sparkTex = LoadVanillaTexture("res://images/vfx/fire/cinder_particle.png");
		if (sparkTex != null)
		{
			sparks.Texture = sparkTex;
			sparks.ScaleAmountMin = width * 0.0016f;
			sparks.ScaleAmountMax = width * 0.0026f;
		}
		else
		{
			sparks.ScaleAmountMin = width * 0.0005f;
			sparks.ScaleAmountMax = width * 0.0009f;
		}
		sparks.ScaleAmountCurve = MakeCurve((0f, 1f), (1f, 0.3f));
		sparks.ColorRamp = MakeGradient(
			(0f, new Color(1f, 0.95f, 0.7f, 0f)),
			(0.1f, new Color(1f, 0.9f, 0.55f, 1f)),
			(0.7f, new Color(1f, 0.55f, 0.2f, 0.8f)),
			(1f, new Color(1f, 0.35f, 0.1f, 0f)));
		return sparks;
	}

	private static CpuParticles2D ConfigureCommon(CpuParticles2D particles, float emitHalfWidth, float emitHalfHeight, bool additive = true)
	{
		particles.Emitting = false;
		particles.LocalCoords = false;
		particles.Explosiveness = 0f;
		particles.Texture = GetParticleTexture();
		particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
		particles.EmissionRectExtents = new Vector2(emitHalfWidth, emitHalfHeight);
		particles.Direction = new Vector2(0f, -1f);
		particles.Spread = 14f;
		if (additive)
		{
			particles.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		}

		return particles;
	}

	private static Curve MakeCurve(params (float X, float Y)[] points)
	{
		Curve curve = new();
		foreach ((float x, float y) in points)
		{
			curve.AddPoint(new Vector2(x, y));
		}

		return curve;
	}

	private static Gradient MakeGradient(params (float Offset, Color Color)[] stops)
	{
		return new Gradient
		{
			Offsets = stops.Select(static s => s.Offset).ToArray(),
			Colors = stops.Select(static s => s.Color).ToArray()
		};
	}

	private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> _vanillaTextureCache = [];

	/// <summary>加载原版 PCK 内贴图;失败时返回 null(调用方回退到程序化渐变圆)。</summary>
	private static Texture2D? LoadVanillaTexture(string resPath)
	{
		if (_vanillaTextureCache.TryGetValue(resPath, out Texture2D? cached))
		{
			return cached != null && GodotObject.IsInstanceValid(cached) ? cached : null;
		}

		Texture2D? texture = ResourceLoader.Load(resPath) as Texture2D;
		_vanillaTextureCache[resPath] = texture;
		return texture;
	}

	private static Texture2D GetParticleTexture()
	{
		if (_particleTexture != null && GodotObject.IsInstanceValid(_particleTexture))
		{
			return _particleTexture;
		}

		Gradient gradient = new()
		{
			Offsets = [0f, 0.35f, 1f],
			Colors = [new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0f)]
		};
		_particleTexture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 64,
			Height = 64,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
		return _particleTexture;
	}
}
