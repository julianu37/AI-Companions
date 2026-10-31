using System;
using TMPro;
using UnityEngine;

namespace Companions;

internal static class FontFix
{
	private static bool _warned;

	internal static void Ensure(string source)
	{
		try
		{
			if (!Broken(TMP_Settings.defaultFontAsset))
			{
				return;
			}
			TMP_FontAsset tMP_FontAsset = Find();
			if (tMP_FontAsset == null)
			{
				if (!_warned)
				{
					_warned = true;
					if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Fonts] No replacement TMP font found yet (" + source + ") - will retry on next scene load.");
				}
			}
			else
			{
				TMP_Settings.defaultFontAsset = tMP_FontAsset;
				if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Fonts] TMP default font set to '" + tMP_FontAsset.name + "' (" + source + ").");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Fonts] default font repair skipped: " + ex.Message);
		}
	}

	private static bool Broken(TMP_FontAsset f)
	{
		if (f == null)
		{
			return true;
		}
		return f.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static TMP_FontAsset Find()
	{
		UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
		TMP_FontAsset tMP_FontAsset = null;
		for (int i = 0; i < array.Length; i++)
		{
			TMP_FontAsset tMP_FontAsset2 = array[i] as TMP_FontAsset;
			if (!(tMP_FontAsset2 == null) && !Broken(tMP_FontAsset2))
			{
				string text = tMP_FontAsset2.name.ToLowerInvariant();
				if (text.Contains("averia") || text.Contains("norse") || text.Contains("valheim"))
				{
					return tMP_FontAsset2;
				}
				if (tMP_FontAsset == null)
				{
					tMP_FontAsset = tMP_FontAsset2;
				}
			}
		}
		return tMP_FontAsset;
	}
}
