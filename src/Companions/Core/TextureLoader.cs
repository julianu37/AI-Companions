using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Companions;

public static class TextureLoader
{
	private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

	private static readonly Assembly ModAssembly = Assembly.GetExecutingAssembly();

	private static readonly MethodInfo LoadImageMethod = ResolveLoadImage();

	private static MethodInfo ResolveLoadImage()
	{
		return Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("LoadImage", new Type[2]
		{
			typeof(Texture2D),
			typeof(byte[])
		});
	}

	public static Texture2D LoadUITexture(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		string key = "UI_" + name;
		if (Cache.TryGetValue(key, out var value))
		{
			return value;
		}
		if (LoadImageMethod == null)
		{
			CompanionsPlugin.Log.LogWarning("TextureLoader: ImageConversion.LoadImage not found via reflection.");
			return null;
		}
		string text = "Companions.Resources.Textures.UI." + name + ".png";
		using Stream stream = ModAssembly.GetManifestResourceStream(text);
		if (stream == null)
		{
			CompanionsPlugin.Log.LogWarning("TextureLoader: UI resource '" + text + "' not found.");
			return null;
		}
		byte[] array = new byte[stream.Length];
		stream.Read(array, 0, array.Length);
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
		if ((bool)LoadImageMethod.Invoke(null, new object[2] { texture2D, array }))
		{
			texture2D.name = name;
			Cache[key] = texture2D;
			return texture2D;
		}
		UnityEngine.Object.Destroy(texture2D);
		return null;
	}

	public static Texture2D LoadRadialTexture(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		string key = "Radial_" + name;
		if (Cache.TryGetValue(key, out var value))
		{
			return value;
		}
		if (LoadImageMethod == null)
		{
			CompanionsPlugin.Log.LogWarning("TextureLoader: ImageConversion.LoadImage not found via reflection.");
			return null;
		}
		string text = "Companions.Resources.Textures.UI.RadialMenu." + name + ".png";
		using Stream stream = ModAssembly.GetManifestResourceStream(text);
		if (stream == null)
		{
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("TextureLoader: Radial resource '" + text + "' not found.");
			return null;
		}
		byte[] array = new byte[stream.Length];
		stream.Read(array, 0, array.Length);
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
		if ((bool)LoadImageMethod.Invoke(null, new object[2] { texture2D, array }))
		{
			texture2D.name = name;
			Cache[key] = texture2D;
			return texture2D;
		}
		UnityEngine.Object.Destroy(texture2D);
		return null;
	}
}
