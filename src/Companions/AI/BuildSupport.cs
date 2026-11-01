using System;
using System.Reflection;

namespace Companions;

/// <summary>
/// Fase E6: <b>is the piece actually held up?</b> - the acceptance "sem peça solta no ar", answered by the game
/// itself instead of by a rule of ours.
///
/// <para>The vanilla <c>WearNTear</c> computes a piece's support in <c>UpdateSupport</c> (private): it takes the
/// piece's own collider boxes, grows them by 0,15 m, looks for the ground (any solid that is not a piece) or a
/// touching piece that supports, and subtracts the material's loss over the distance between the centres of mass.
/// Under the material's minimum the piece <b>breaks</b> - that is the game's own "floating" rule. Reading it back
/// with <c>HaveSupport</c> is the most honest check there is: it is the same number the game uses to collapse a
/// structure (the BuildOrders does the same, with a full simulation of it - see <c>docs/references.md</c>).</para>
///
/// <para>Everything here is <b>fail-open</b>: if the reflection does not resolve (a future game version renamed the
/// members), <see cref="Supported"/> answers <c>null</c> and the caller says nothing - the build order never fails
/// because a diagnostic could not be read.</para>
/// </summary>
internal static class BuildSupport
{
	private static bool _looked;
	private static MethodInfo _update;
	private static MethodInfo _have;
	private static MethodInfo _min;
	private static FieldInfo _value;

	private static void Look()
	{
		if (_looked)
		{
			return;
		}
		_looked = true;
		try
		{
			Type type = typeof(WearNTear);
			_update = type.GetMethod("UpdateSupport", BindingFlags.Instance | BindingFlags.NonPublic);
			_have = type.GetMethod("HaveSupport", BindingFlags.Instance | BindingFlags.NonPublic);
			_min = type.GetMethod("GetMinSupport", BindingFlags.Instance | BindingFlags.NonPublic);
			_value = type.GetField("m_support", BindingFlags.Instance | BindingFlags.NonPublic);
		}
		catch (Exception)
		{
			_update = null;
			_have = null;
			_min = null;
			_value = null;
		}
	}

	/// <summary>
	/// Recomputes the piece's support the way the game does and answers whether it stands. <c>null</c> when the game's
	/// members could not be read (never a refusal).
	/// </summary>
	internal static bool? Supported(WearNTear wear)
	{
		if (wear == null)
		{
			return null;
		}
		Look();
		try
		{
			if (_update != null)
			{
				_update.Invoke(wear, null);
			}
			if (_have != null)
			{
				return (bool)_have.Invoke(wear, null);
			}
		}
		catch (Exception)
		{
			// a piece mid-placement (no ZDO yet) can throw inside the game's own update: not a verdict
		}
		return null;
	}

	/// <summary>The support value the game stored (<c>m_support</c>), or <c>null</c>.</summary>
	internal static float? Value(WearNTear wear)
	{
		if (wear == null)
		{
			return null;
		}
		Look();
		try
		{
			return _value != null ? (float?)(float)_value.GetValue(wear) : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>The minimum support of the piece's material (<c>GetMinSupport</c>), or <c>null</c>.</summary>
	internal static float? Min(WearNTear wear)
	{
		if (wear == null)
		{
			return null;
		}
		Look();
		try
		{
			return _min != null ? (float?)(float)_min.Invoke(wear, null) : null;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
