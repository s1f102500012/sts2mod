namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void AssetResolverPrefersRawTextureBeforePackedResource()
	{
		Dictionary<string, FakeTexture> cache = new(StringComparer.Ordinal);
		int packedLoads = 0;

		FakeTexture? resolved = AssetResourceResolver.Resolve(
			"res://HextechRunes/images/relics/test.png",
			cache,
			static texture => texture.IsValid,
			static _ => new FakeTexture("raw", true),
			_ =>
			{
				packedLoads++;
				return new FakeTexture("packed", true);
			});

		Equal("raw", resolved?.Name, "raw image texture");
		Equal(0, packedLoads, "packed resource loader calls");
		Equal("raw", cache["res://HextechRunes/images/relics/test.png"].Name, "cached raw image texture");
	}

	private static void AssetResolverRecognizesRawImagePaths()
	{
		Expect(AssetResourceResolver.IsRawImagePath("res://HextechRunes/images/relics/test.png"), "PNG should use raw decoding");
		Expect(AssetResourceResolver.IsRawImagePath("res://HextechRunes/images/relics/test.JPG"), "JPG should use raw decoding");
		Expect(AssetResourceResolver.IsRawImagePath("res://HextechRunes/images/relics/test.jpeg"), "JPEG should use raw decoding");
		Expect(!AssetResourceResolver.IsRawImagePath("res://HextechRunes/images/relics/test.ctex"), "CTEX must not be treated as a raw image");
	}

	private static void AssetResolverRejectsInvalidTextureObjects()
	{
		const string path = "res://HextechRunes/images/relics/test.png";
		Dictionary<string, FakeTexture> cache = new(StringComparer.Ordinal)
		{
			[path] = new FakeTexture("stale", false)
		};

		FakeTexture? resolved = AssetResourceResolver.Resolve(
			path,
			cache,
			static texture => texture.IsValid,
			static _ => null,
			static _ => new FakeTexture("disposed-packed-wrapper", false));

		Equal<FakeTexture?>(null, resolved, "invalid texture resolution");
		Expect(!cache.ContainsKey(path), "invalid cached and packed textures must not remain cached");
	}

	private static void AssetResolverPropagatesLoaderExceptionsWithoutCachingDirtyEntries()
	{
		const string path = "res://HextechRunes/images/relics/test.png";
		Dictionary<string, FakeTexture> cache = new(StringComparer.Ordinal)
		{
			[path] = new FakeTexture("stale", false)
		};

		ExpectThrows<InvalidOperationException>(
			() => AssetResourceResolver.Resolve(
				path,
				cache,
				static texture => texture.IsValid,
				static _ => throw new InvalidOperationException("primary loader failure"),
				static _ => new FakeTexture("secondary", true)),
			"loader exceptions must propagate to the caller-side try/catch");
		Expect(!cache.ContainsKey(path), "stale entry must be evicted and no dirty entry cached after a loader throw");
	}

	private static void AssetResolverRawOnlyMissReturnsNullWithoutCaching()
	{
		// 生产形态:raw 图片路径的 secondary 恒返回 null(HextechTextures.LoadPortableTexture)。
		const string path = "res://HextechRunes/images/relics/test.png";
		Dictionary<string, FakeTexture> cache = new(StringComparer.Ordinal);

		FakeTexture? resolved = AssetResourceResolver.Resolve(
			path,
			cache,
			static texture => texture.IsValid,
			static _ => null,
			static _ => null);

		Equal<FakeTexture?>(null, resolved, "raw-only miss resolution");
		Expect(!cache.ContainsKey(path), "a raw-only miss must not cache anything");
	}

	private sealed record FakeTexture(string Name, bool IsValid);
}
