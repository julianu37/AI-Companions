using UnityEngine;

namespace Companions;

// 1.5.72: every way combat moves a companion checks the ground first (user: "run a check on all the movement in the mod to
// see if there are any other sections that do not have the safety check for things like a drop"). The audit found the
// combat fallbacks still used the game's own BaseAI moves - MoveTo when the mover had given up (the mover gives up right
// after "stopping at the edge", so this walked the drop it had just refused), Flee (a random way out when its tries fail)
// and RandomMovement / RandomMovementArroundPoint (no checks at all). Companions have the game's m_avoidWater / m_avoidFire
// off, so none of those looked at water or fire either. These replacements only take a step that MHR_Mover.SafeStep
// passes: no wall, no drop or climb, no deep water, no fire, no spike wall.
public partial class CompanionCombatAI
{
	private Vector3 _wanderPoint;

	private float _wanderUntil;

	private float _nextUnsafeLog;

	// the game's MoveTo, only while the next couple of meters are safe; otherwise stand
	private bool SafeGameMove(float dt, Vector3 point, float dist, bool run)
	{
		Vector3 me = base.transform.position;
		Vector3 way = point - me;
		way.y = 0f;
		float len = way.magnitude;
		if (len <= Mathf.Max(dist, 0.5f))
		{
			_ai.StopMoving();
			return true;
		}
		if (!MHR_Mover.SafeStep(me, way / len, Mathf.Min(2f, len)))
		{
			_ai.StopMoving();
			if (CompanionsPlugin.LogDetailed) UnsafeLog("not walking on towards " + (_targetCreature != null ? _targetCreature.m_name : "it") + " - a drop, water, fire or spikes in the way");
			return false;
		}
		return _ai.GameMoveTo(dt, point, dist, run);
	}

	// away from a point: the safest direction that still leads away (tried in 8 directions), else stand
	private bool SafeFlee(float dt, Vector3 from)
	{
		Vector3 me = base.transform.position;
		Vector3 away = me - from;
		away.y = 0f;
		if (away.sqrMagnitude < 0.01f)
		{
			away = -base.transform.forward;
		}
		away.Normalize();
		for (int k = 0; k < 8; k++)
		{
			float ang = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 30f;
			if (Mathf.Abs(ang) > 100f)
			{
				break;
			}
			Vector3 d = Quaternion.Euler(0f, ang, 0f) * away;
			if (MHR_Mover.SafeStep(me, d, 3f))
			{
				_character.SetMoveDir(d);
				_character.SetRun(run: true);
				return true;
			}
		}
		_ai.StopMoving();
		if (CompanionsPlugin.LogDetailed) UnsafeLog("no safe way to back off (a drop, water, fire or spikes) - standing its ground");
		return false;
	}

	// moving about a point (waiting for a chance, looking for a lost target): a safe spot within the radius, reached through
	// MHR_Mover (its own drop, water and fire checks), a new one every few seconds
	private void SafeWander(float dt, Vector3 center, float radius, bool run)
	{
		Vector3 me = base.transform.position;
		if (Time.time >= _wanderUntil || Vector3.Distance(me, _wanderPoint) < 1f || Vector3.Distance(center, _wanderPoint) > radius + 2f)
		{
			_wanderUntil = Time.time + Random.Range(2f, 4f);
			_wanderPoint = me;
			for (int i = 0; i < 8; i++)
			{
				Vector2 r = Random.insideUnitCircle * radius;
				Vector3 p = center + new Vector3(r.x, 0f, r.y);
				Vector3 way = p - me;
				way.y = 0f;
				float len = way.magnitude;
				if (len > 1f && MHR_Mover.SafeStep(me, way / len, Mathf.Min(len, 4f)))
				{
					_wanderPoint = p;
					break;
				}
			}
		}
		if (Vector3.Distance(me, _wanderPoint) < 1f)
		{
			_ai.StopMoving();
			return;
		}
		MHR_Mover.Go(_ai, dt, _wanderPoint, 0.8f, run, MHR_Mover.CombatTag, run ? MHR_Mover.SpeedRun : MHR_Mover.SpeedJog);
	}

	private void UnsafeLog(string msg)
	{
		if (Time.time >= _nextUnsafeLog)
		{
			_nextUnsafeLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Combat] " + msg);
		}
	}
}
