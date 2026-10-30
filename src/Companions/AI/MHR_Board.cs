using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.82: getting on a ship like a player (user, log e26ca237: "The NPC does not seem able to use the ladder to get onto the
// ship or jump to it" / "The NPC should be able to get on a ship if I can get on a ship. It might be worth having the follow
// in my footsteps fall back as well"). The log: it picked the ship's ladder but never got within its 2 m use range (it hangs
// on the hull over the water), the ship isn't on the route map, jumps never landed on a deck, and the board order just
// walked at the ship's middle with the game's MoveTo and put it in a seat after 30 s anyway.
//
// Boarding - on the board order, or on its own while following you when you're aboard a ship close by - is, every frame:
// aboard (standing on the ship's deck) -> a free seat, else stand on deck; next to one of the ship's ladders -> use it
// (Ladder.Interact puts it at the top, as it does you); on the dock or shore edge with the deck in jump reach -> jump aboard;
// otherwise walk (or swim, stamina allowing) to the nearest ladder through MHR_Mover. Stuck for a while -> your own steps
// onto the ship (where you stood before you got on, then what you did: a jump, or the ladder). Still nothing -> it waits on
// the shore ("can't get aboard"), no teleporting.
internal static class MHR_Board
{
	private class S
	{
		public Ship Ship;
		public Chair Chair;
		public bool Auto;
		public float Since;
		public float ProgressAt;
		public float Best = float.MaxValue;
		public float NextJump;
		public float NextLog;
		public bool Footsteps;
		public bool FootstepsTried;
		public Vector3 StepFrom;
		public Vector3 StepTo;
		public float StepBest = float.MaxValue;
		public bool HasShore;
		public Vector3 Shore;
		public float TreadSince = -1f;
		public float WadeBest = float.MaxValue;
		public bool TakeoffTried;
		public bool HasTakeoff;
		public Vector3 Takeoff;
		public int TakeoffJumps;
		public bool JumpLogged;
		public float WadeAt = -1f;
		public bool WadeGaveUp;
		public float HoldUntil;
		public Ship HoldShip;
		public Ship SatOn;
		public float OwnerOffSince = -1f;
		public float SeatSince = -1f;
		public bool LaunchTried;
		public bool HasLaunch;
		public Vector3 Launch;
		public Ship LeaveShip;
		public float LeaveSince;
		public bool HasLeaveSpot;
		public Vector3 LeaveSpot;
		public float LeapAt = -10f;
		public Vector3 LeapDir;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static int s_vehicle = -1;

	private static int s_solid = -1;

	private const float GiveUpAfter = 30f;

	private const float StuckFor = 8f;

	private const float HoldFor = 20f;

	// 1.5.88: the ladder is used from 2 m (Ladder.m_useDistance) and treading water is free - the swim ends this far short of it
	private const float LadderReach = 1.7f;

	private static int Vehicle()
	{
		if (s_vehicle == -1)
		{
			s_vehicle = LayerMask.GetMask("vehicle");
		}
		return s_vehicle;
	}

	private static int Solid()
	{
		if (s_solid == -1)
		{
			s_solid = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
		}
		return s_solid;
	}

	private static S Get(CompanionAI ai)
	{
		int id = ai.GetInstanceID();
		if (!s_s.TryGetValue(id, out S s))
		{
			s = new S();
			s_s[id] = s;
		}
		return s;
	}

	private static S Peek(CompanionAI ai)
	{
		return ai != null && s_s.TryGetValue(ai.GetInstanceID(), out S s) ? s : null;
	}

	private static float Flat(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	private static void Log(string msg)
	{
		CompanionsPlugin.Log?.LogInfo("[Board] " + msg);
	}

	internal static bool Active(CompanionAI ai)
	{
		S s = Peek(ai);
		return s != null && s.Ship != null;
	}

	// 1.5.84: the boarding companion's nearest ship ladder and its flat distance (WaterSafety asks, so it doesn't turn back
	// for the shore with the ladder right there)
	internal static bool KeepSwimming(Character c)
	{
		CompanionAI ai = c != null ? c.GetComponent<CompanionAI>() : null;
		S s = Peek(ai);
		if (s == null || s.Ship == null)
		{
			return false;
		}
		Ladder l = NearestLadder(s.Ship, c.transform.position, out float flat);
		if (l == null || flat > 8f)
		{
			return false;
		}
		CompanionStamina stam = c.GetComponent<CompanionStamina>();
		return flat < 3.5f || stam == null || stam.Stamina >= MHR_Water.SwimStamina(c, Mathf.Max(0f, flat - LadderReach)) * 1.1f;
	}

	private static Ladder NearestLadder(Ship ship, Vector3 me, out float flat)
	{
		flat = float.MaxValue;
		Ladder best = null;
		foreach (Ladder l in ship.GetComponentsInChildren<Ladder>())
		{
			if (l == null || l.m_targetPos == null)
			{
				continue;
			}
			float d = Flat(me, l.transform.position);
			if (d < flat)
			{
				flat = d;
				best = l;
			}
		}
		return best;
	}

	internal static void Start(CompanionAI ai, Ship ship, Chair chair, bool auto)
	{
		if (ai == null || ship == null)
		{
			return;
		}
		S s = Get(ai);
		s.Ship = ship;
		s.Chair = chair;
		s.Auto = auto;
		s.Since = Time.time;
		s.ProgressAt = Time.time;
		s.Best = float.MaxValue;
		s.Footsteps = false;
		s.FootstepsTried = false;
		s.HoldUntil = 0f;
		s.TreadSince = -1f;
		s.SeatSince = -1f;
		s.LaunchTried = false;
		s.HasLaunch = false;
		s.WadeBest = float.MaxValue;
		s.WadeAt = -1f;
		s.WadeGaveUp = false;
		s.TakeoffTried = false;
		s.HasTakeoff = false;
		s.TakeoffJumps = 0;
		s.JumpLogged = false;
		MHR_Mover.Release(ai);
		MHR_Deck.Release(ai);
		Log(ai.GetComponent<Character>()?.m_name + (auto ? " is following you aboard" : " is boarding the ship"));
	}

	internal static void Cancel(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && s.Ship != null)
		{
			s.Ship = null;
			s.Chair = null;
			s.Footsteps = false;
			s.SeatSince = -1f;
			MHR_Mover.Release(ai);
			MHR_Deck.Release(ai);
		}
	}

	// the ship under a character standing on its deck (a short ray down onto the ship's own colliders)
	// 1.5.86: you count as aboard when the ship has you on its list of players aboard (log 79fb1bde: you'd loaded in aboard,
	// the ray missed you, and it set off getting off the ship)
	private static bool Aboard(Player p, Ship ship)
	{
		return p != null && ship != null && (ShipUnder(p) == ship || ship.IsPlayerInBoat(p));
	}

	internal static Ship ShipUnder(Character c)
	{
		if (c == null)
		{
			return null;
		}
		Ship s = c.GetStandingOnShip();
		if (s != null)
		{
			return s;
		}
		if (Physics.Raycast(c.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.2f, Vehicle()))
		{
			return hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody.GetComponent<Ship>() : hit.collider.GetComponentInParent<Ship>();
		}
		return null;
	}

	// 1.5.83: which seat it took, for the log (user: "The NPC is also sitting on a stool that does not look to be there")
	private static string SeatPath(Transform t, Transform root)
	{
		string path = t.name;
		while (t.parent != null && t.parent != root)
		{
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

	// a free seat on the ship: none within 0.5 m of a character
	private static Chair FreeChair(Ship ship, Chair prefer, Vector3 near, Character self)
	{
		if (Free(prefer, self))
		{
			return prefer;
		}
		Chair best = null;
		float bd = float.MaxValue;
		foreach (Chair ch in ship.GetComponentsInChildren<Chair>())
		{
			if (!Free(ch, self))
			{
				continue;
			}
			float d = Vector3.Distance(near, ch.m_attachPoint.position);
			if (d < bd)
			{
				bd = d;
				best = ch;
			}
		}
		return best;
	}

	// 1.5.84: the mast has a seat point but no seat (log ca66e4e5: "taking a seat: 'interactive/mast'" - user: "sitting in
	// the middle of the boat rather than one of the seats ... There are 2 seats near the front and 2 near the rear")
	internal static bool IsMast(Chair ch)
	{
		return ch != null && ch.gameObject.name.IndexOf("mast", System.StringComparison.OrdinalIgnoreCase) >= 0;
	}

	// 1.5.85: `self` doesn't count (it walks up to its own seat)
	internal static bool Free(Chair ch, Character self = null)
	{
		if (ch == null || !ch.m_inShip || ch.m_attachPoint == null || IsMast(ch))
		{
			return false;
		}
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c != null && c != self && Vector3.Distance(c.transform.position, ch.m_attachPoint.position) < 0.5f)
			{
				return false;
			}
		}
		return true;
	}

	// a spot on the deck on our side of the ship: the ship's closest point to us, a metre in, straight down onto it
	private static bool DeckSpot(Ship ship, Vector3 me, out Vector3 spot, Collider[] cols = null)
	{
		spot = Vector3.zero;
		Vector3 center = ship.transform.position;
		Collider best = null;
		float bd = float.MaxValue;
		Vector3 bp = center;
		foreach (Collider col in cols ?? ship.GetComponentsInChildren<Collider>())
		{
			if (col == null || col.isTrigger || ((1 << col.gameObject.layer) & Vehicle()) == 0)
			{
				continue;
			}
			Vector3 p;
			try
			{
				p = col.ClosestPoint(me);
			}
			catch
			{
				continue;
			}
			float d = Flat(p, me);
			if (d < bd)
			{
				bd = d;
				best = col;
				bp = p;
			}
		}
		if (best == null)
		{
			return false;
		}
		Vector3 inward = center - bp;
		inward.y = 0f;
		Vector3 probe = bp + (inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.zero) * 1f;
		if (!Physics.Raycast(new Vector3(probe.x, Mathf.Max(me.y, center.y) + 4f, probe.z), Vector3.down, out RaycastHit hit, 10f, Vehicle()))
		{
			return false;
		}
		Ship s = hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody.GetComponent<Ship>() : hit.collider.GetComponentInParent<Ship>();
		if (s != ship)
		{
			return false;
		}
		spot = hit.point;
		return true;
	}

	// While following: you're aboard a ship close by -> board it on its own. Returns true when it handled this frame
	// (boarding, or waiting on the shore after it couldn't get aboard).
	internal static bool FollowHook(CompanionAI ai, GameObject target, float dt)
	{
		if (ai == null || target == null || ai.IsOnShip || !(ModConfig.FollowAutoBoard?.Value ?? true))
		{
			return false;
		}
		Player p = target.GetComponent<Player>();
		Character c = ai.GetComponent<Character>();
		if (p == null || c == null)
		{
			return false;
		}
		Ship ship = ShipUnder(p);
		Ship mine = ShipUnder(c);
		S s = Get(ai);
		if (mine != null && ship != mine && Aboard(p, mine))
		{
			ship = mine;
		}
		// 1.5.85: on a ship you're not on - walk to its side nearest you and get off (no teleport)
		if (mine != null && mine != ship)
		{
			return Leave(ai, s, c, mine, p, dt);
		}
		// 1.5.89: in the air after jumping off - keep heading your way until it lands (log 81234e48: the follow route took over
		// mid-jump, "taking the 30m way round", and it came down on the deck again)
		if (mine == null && s.LeaveShip != null && !c.IsOnGround() && !c.IsSwimming() && Time.time - s.LeapAt < 3f)
		{
			ai.MoveTowards(s.LeapDir, run: true);
			return true;
		}
		s.LeaveShip = null;
		if (ship == null)
		{
			s.HoldUntil = 0f;
			return false;
		}
		// 1.5.85: standing on the same deck as you (no free seat): keep near you across the deck
		if (mine == ship)
		{
			if (Flat(ai.transform.position, p.transform.position) > 2.5f && MHR_Deck.SpotNear(ship, p.transform.position, out Vector3 near))
			{
				MHR_Deck.Walk(ai, ship, near, 1.5f, out _, walkLast: 0f);
			}
			else
			{
				MHR_Deck.Release(ai);
				ai.StopMoving();
				ai.LookAtPoint(p.transform.position);
			}
			return true;
		}
		// couldn't get aboard: wait on the shore while you're on that ship (the far-away catch-up still applies)
		if (Time.time < s.HoldUntil && s.HoldShip == ship)
		{
			if (s.HasShore && Flat(ai.transform.position, s.Shore) > 1f)
			{
				MHR_Mover.Go(ai, dt, s.Shore, 0.8f, false, "Board", MHR_Mover.SpeedJog);
				if (MHR_Mover.GaveUp(ai))
				{
					s.HasShore = false;
				}
				return true;
			}
			s.HasShore = false;
			ai.StopMoving();
			ai.LookAtPoint(ship.transform.position);
			return true;
		}
		float range = ModConfig.FollowBoardRange?.Value ?? 20f;
		Rigidbody rb = ship.GetComponent<Rigidbody>();
		if (Flat(ai.transform.position, ship.transform.position) > range || (rb != null && rb.linearVelocity.magnitude > 3f) || ai.IsInCombat)
		{
			return false;
		}
		Start(ai, ship, null, auto: true);
		return Update(ai, dt);
	}

	// 1.5.85: getting off a ship like you do (user, log ce2af5bd: "They also seem to be teleporting a bit"): across the deck to
	// the side nearest you, then over the side - a jump, a drop, or into the water and swim. 15 s and the normal follow (and
	// its far-away catch-up) takes over.
	private static bool Leave(CompanionAI ai, S s, Character c, Ship ship, Player p, float dt)
	{
		float now = Time.time;
		if (s.LeaveShip != ship)
		{
			// you're in the water or mid-jump: stay aboard until you're on your feet somewhere
			if (p.IsSwimming() || !p.IsOnGround())
			{
				MHR_Deck.Release(ai);
				ai.StopMoving();
				return true;
			}
			s.LeaveShip = ship;
			s.LeaveSince = now;
			s.NextJump = 0f;
			s.LeapAt = -10f;
			s.HasLeaveSpot = MHR_Deck.SideSpot(ship, ai.transform.position, p.transform.position, out s.LeaveSpot) || MHR_Deck.SpotNear(ship, p.transform.position, out s.LeaveSpot);
			Log(c.m_name + " getting off the ship");
		}
		if (now - s.LeaveSince > 15f)
		{
			MHR_Deck.Release(ai);
			return false;
		}
		Vector3 me = ai.transform.position;
		Vector3 you = p.transform.position;
		// not straight after a jump - it's at the side already (the walk would pull it back from the rail)
		if (s.HasLeaveSpot && now - s.LeapAt > 1.5f)
		{
			bool there = MHR_Deck.Walk(ai, ship, s.LeaveSpot, 0.5f, out bool stuck, walkLast: 0f, stuckAfter: 3f);
			if (!there && !stuck)
			{
				return true;
			}
		}
		MHR_Deck.Release(ai);
		Vector3 dir = you - me;
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.01f)
		{
			return false;
		}
		dir.Normalize();
		ai.transform.rotation = Quaternion.LookRotation(dir);
		// over the side like you: run at it and jump, again as soon as it's back on its feet on the deck
		ai.MoveTowards(dir, run: true);
		if (c.IsOnGround() && now >= s.NextJump)
		{
			s.NextJump = now + 0.5f;
			s.LeapAt = now;
			s.LeapDir = dir;
			MHR_Jump.Leap(ai, c, dir);
			Log(c.m_name + " jumping off the ship");
		}
		return true;
	}

	// 1.5.85: the dry spot nearest the ladder (a dock end, the bank) within 15 m of it - walk there, then dive in and swim the
	// short way (user, log ce2af5bd: "It looked like they still took the long way around one of the times" - the ladder was
	// 9.7 m off, past the 8 m dive range, and the route went 26 m round)
	private static bool LaunchSpot(Ladder ladder, Vector3 me, out Vector3 spot)
	{
		spot = me;
		Vector3 lp = ladder.transform.position;
		int mask = Solid() | Vehicle();
		for (int r = 1; r <= 15; r++)
		{
			float best = float.MaxValue;
			bool found = false;
			for (int k = 0; k < 24; k++)
			{
				Vector3 d = Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward;
				Vector3 q = lp + d * r;
				if (!Physics.Raycast(new Vector3(q.x, lp.y + 8f, q.z), Vector3.down, out RaycastHit hit, 30f, mask))
				{
					continue;
				}
				if (((1 << hit.collider.gameObject.layer) & Vehicle()) != 0 || hit.point.y > lp.y + 4f)
				{
					continue;
				}
				q = hit.point;
				if (MHR_Water.DepthAt(q) >= 0.1f || Flat(q, me) > 40f)
				{
					continue;
				}
				float score = Flat(q, me);
				if (score < best)
				{
					best = score;
					spot = q;
					found = true;
				}
			}
			if (found)
			{
				return true;
			}
		}
		return false;
	}

	// a seated companion gets up when you've left the ship (2 s on land, within 40 m) - only while it's following you
	internal static bool ShouldLeaveSeat(CompanionAI ai, Ship ship)
	{
		S s = Peek(ai);
		if (s == null || s.SatOn == null || s.SatOn != ship)
		{
			return false;
		}
		CompanionSetup setup = ai.GetComponent<CompanionSetup>();
		if (setup == null || !setup.GetFollow())
		{
			s.OwnerOffSince = -1f;
			return false;
		}
		Player owner = MHR_Owner.Of(ai);
		if (owner == null)
		{
			return false;
		}
		if (Aboard(owner, ship) || !owner.IsOnGround() || Vector3.Distance(owner.transform.position, ai.transform.position) > 40f)
		{
			s.OwnerOffSince = -1f;
			return false;
		}
		if (s.OwnerOffSince < 0f)
		{
			s.OwnerOffSince = Time.time;
		}
		if (Time.time - s.OwnerOffSince < 2f)
		{
			return false;
		}
		s.SatOn = null;
		s.OwnerOffSince = -1f;
		Log(ai.GetComponent<Character>()?.m_name + " - you've gone ashore, getting off the ship");
		return true;
	}

	// boarding, every frame; true while it's still at it
	internal static bool Update(CompanionAI ai, float dt)
	{
		S s = Peek(ai);
		Character c = ai.GetComponent<Character>();
		Humanoid h = c as Humanoid;
		if (s == null || s.Ship == null || c == null || h == null)
		{
			return false;
		}
		Ship ship = s.Ship;
		float now = Time.time;
		Vector3 me = ai.transform.position;
		string name = c.m_name;
		// aboard: a seat, or stand on the deck
		if (ShipUnder(c) == ship)
		{
			Chair seat = FreeChair(ship, s.Chair, me, c);
			bool auto = s.Auto;
			if (seat != null)
			{
				// 1.5.85: walks across the deck to the seat, then sits (user, log ce2af5bd: "Rather than getting up and walking to
				// the seat they just apear there"); can't get closer for 5 s, or 20 s in all -> sits anyway
				s.Chair = seat;
				Vector3 local = ship.transform.InverseTransformPoint(seat.m_attachPoint.position);
				if (s.SeatSince < 0f)
				{
					s.SeatSince = now;
					s.ProgressAt = now;
					Log($"{name} is aboard - walking to a seat: '{SeatPath(seat.transform, ship.transform)}' at {local:F2} on the ship");
				}
				Vector3 spot = MHR_Deck.SpotNear(ship, seat.m_attachPoint.position, out Vector3 l2) ? l2 : local;
				bool there = MHR_Deck.Walk(ai, ship, spot, 0.6f, out bool stuck, walkLast: 1f);
				if (!there && !stuck && now - s.SeatSince < 20f)
				{
					return true;
				}
				if (!there)
				{
					Log(name + " can't get any closer to the seat - sitting down anyway");
				}
				Cancel(ai);
				Log($"{name} sits down: '{SeatPath(seat.transform, ship.transform)}' (world {seat.m_attachPoint.position:F1})");
				ai.AttachToShip(ship, seat);
				// 1.5.88: ordered aboard too - it gets up when you go ashore if it's following you (log 5930316c: "did not leave
				// when I did")
				Get(ai).SatOn = ship;
			}
			else
			{
				Cancel(ai);
				Log(name + " is aboard - no free seat, standing on deck");
				if (!auto)
				{
					ai.BoardingEnded();
				}
			}
			return false;
		}
		// next to one of its ladders: use it
		Ladder nearest = null;
		float ld = float.MaxValue;
		foreach (Ladder l in ship.GetComponentsInChildren<Ladder>())
		{
			if (l == null || l.m_targetPos == null)
			{
				continue;
			}
			float d = Vector3.Distance(me, l.transform.position);
			// 1.5.86: swimming or standing, like you - not mid-dive (log 79fb1bde: it used the ladder in the air from 4.9 m)
			if (d < l.m_useDistance - 0.1f && (c.IsSwimming() || c.IsOnGround()))
			{
				l.Interact(h, hold: false, alt: false);
				Log(name + " used the ship's ladder");
				s.ProgressAt = now;
				return true;
			}
			if (d < ld)
			{
				ld = d;
				nearest = l;
			}
		}
		// 1.5.83: resting on the bank for its breath before a swim isn't being stuck (log 6b069f3f: it gave up while resting,
		// then stood in the shallows)
		CompanionStamina stam = ai.GetComponent<CompanionStamina>();
		if (MHR_Mover.RestingForSwim(ai))
		{
			s.ProgressAt = now;
			if (now >= s.NextLog)
			{
				s.NextLog = now + 5f;
				Log($"{name} resting before the swim ({(stam != null ? stam.Stamina : 0f):F0}/{(stam != null ? stam.MaxStamina : 0f):F0} stamina)");
			}
		}
		// progress = getting closer to the ship
		float dist = Flat(me, ship.transform.position);
		if (dist < s.Best - 0.5f)
		{
			s.Best = dist;
			s.ProgressAt = now;
		}
		// 1.5.93: jump straight aboard when it can - checked before diving in (user, log c22fc35d: "kept jumping into the water
		// to get to the ladder when they could jump directly onto the boat deck" - the dive/walk branch always took the frame
		// first and the jump check was never reached)
		if (TryJumpAboard(ai, s, c, me, ship, now, name))
		{
			return true;
		}
		// 1.5.93: somewhere to jump aboard from, before any swim: where you jumped aboard yourself (your steps), else dry ground
		// within 15 m with the deck in jump reach. Walk there and jump; three jumps that don't land aboard -> the ladder.
		if (!s.TakeoffTried && !c.IsSwimming() && MHR_Water.DepthAt(me) < 0.3f)
		{
			s.TakeoffTried = true;
			Player owner = MHR_Owner.Of(ai);
			if (owner != null && MHR_Follow.StepsOnto(owner.gameObject, ship, out Vector3 from, out _) && MHR_Water.DepthAt(from) < 0.1f && Flat(from, me) < 30f && JumpCheck(c, ship, from, null, out _, out _, out _, out _, out _))
			{
				s.HasTakeoff = true;
				s.Takeoff = from;
				Log($"{name} going to jump aboard where you did, from {from:F1}");
			}
			else if (JumpSpot(c, ship, me, out Vector3 spot))
			{
				s.HasTakeoff = true;
				s.Takeoff = spot;
				Log($"{name} going to jump aboard from {spot:F1} ({Flat(spot, me):F1} m away)");
			}
		}
		if (s.HasTakeoff && !c.IsSwimming())
		{
			if (Flat(me, s.Takeoff) > 0.6f)
			{
				MHR_Mover.Go(ai, dt, s.Takeoff, 0.4f, dist > 8f, "Board", dist > 8f ? MHR_Mover.SpeedRun : MHR_Mover.SpeedJog);
				if (MHR_Mover.Progressing(ai))
				{
					s.ProgressAt = now;
				}
				if (MHR_Mover.GaveUp(ai))
				{
					s.HasTakeoff = false;
					MHR_Mover.Release(ai);
					Log(name + " can't get to the spot to jump aboard from - the ladder instead");
				}
				return true;
			}
			// there: face the deck and wait for the jump check (above) - a few tries, then the ladder
			if (s.TakeoffJumps >= 3 || now - s.ProgressAt > 6f)
			{
				s.HasTakeoff = false;
				Log(name + " couldn't jump aboard from there - the ladder instead");
			}
			else
			{
				ai.StopMoving();
				ai.LookAtPoint(ship.transform.position);
				return true;
			}
		}
		// 1.5.84: like a player (user: "Its faster to jump off the dock to the boat and wait till you pop up from the water then
		// use the ladder ... rather than just treading water for a moment to wait for the ladder to be in range")
		Ladder near = NearestLadder(ship, me, out float flatL);
		CompanionStamina st2 = ai.GetComponent<CompanionStamina>();
		if (near != null && !s.LaunchTried && !c.IsSwimming() && flatL > 2f && flatL <= 15f)
		{
			s.LaunchTried = true;
			s.HasLaunch = LaunchSpot(near, me, out s.Launch);
			if (s.HasLaunch)
			{
				Log($"{name} heading for the water's edge nearest the ladder ({Flat(s.Launch, near.transform.position):F1} m from it)");
			}
		}
		// a swim longer than it has stamina for at all: say so and wait on the shore
		// 1.5.88: from the launch spot when it has one (log 5930316c: gave up on the walking route's 9 m swim, then dived in from
		// 4.4 m and made it when you ordered it), and only to within reach of the ladder
		// 1.5.92: already in the water (wading) - straight at the ladder, not the walking route round the dock
		float swimM = s.HasLaunch && near != null ? Mathf.Max(0f, Flat(s.Launch, near.transform.position) - LadderReach) : (near != null && MHR_Water.DepthAt(me) > 0.3f ? Mathf.Max(0f, flatL - LadderReach) : MHR_Mover.RouteSwim(ai));
		if (swimM > 0.5f && stam != null && stam.MaxStamina > 0f && !c.IsSwimming())
		{
			float needSwim = MHR_Water.SwimStamina(c, swimM) * 1.25f + 5f;
			if (needSwim > stam.MaxStamina)
			{
				return GiveUp(ai, s, c, $"the swim to the ladder ({swimM:F0} m) needs about {needSwim:F0} stamina, it has {stam.MaxStamina:F0} at most");
			}
		}
		bool atLaunch = s.HasLaunch && Flat(me, s.Launch) < 1.2f;
		if (near != null && (flatL <= 8f || atLaunch))
		{
			Vector3 toL = near.transform.position - me;
			toL.y = 0f;
			Vector3 dirL = toL.sqrMagnitude > 0.01f ? toL.normalized : ai.transform.forward;
			if (c.IsSwimming() && flatL < 3.5f)
			{
				// in the water by the ladder: tread water (no stamina) until it's in reach
				if (s.TreadSince < 0f)
				{
					s.TreadSince = now;
					Log(name + " treading water by the ladder until it's in reach");
				}
				s.ProgressAt = now;
				if (flatL > 0.8f)
				{
					ai.transform.rotation = Quaternion.LookRotation(dirL);
					ai.MoveTowards(dirL, run: false);
				}
				else
				{
					ai.StopMoving();
				}
				if (now - s.TreadSince > 20f)
				{
					return GiveUp(ai, s, c, "the ladder never came within reach");
				}
				return true;
			}
			s.TreadSince = -1f;
			if (!c.IsSwimming() && c.IsOnGround() && !Physics.Raycast(me + Vector3.up * 0.5f, dirL, 1.5f, Solid()))
			{
				Vector3 ahead = me + dirL * 1.2f;
				ahead.y = MHR_Water.GroundAt(ahead);
				if (MHR_Water.DepthAt(ahead) > 0.8f)
				{
					// at the water's edge facing the ladder: dive in, if it has the stamina for the swim
					float need = MHR_Water.SwimStamina(c, Mathf.Max(0f, flatL - LadderReach)) * 1.25f + 5f;
					if (st2 != null && st2.Stamina < need)
					{
						ai.StopMoving();
						s.ProgressAt = now;
						if (now >= s.NextLog)
						{
							s.NextLog = now + 5f;
							Log($"{name} resting before the swim to the ladder ({st2.Stamina:F0}/{need:F0} stamina)");
						}
						return true;
					}
					if (now >= s.NextJump)
					{
						s.NextJump = now + 2f;
						s.ProgressAt = now;
						MHR_Jump.Leap(ai, c, dirL);
						// 1.5.92: the launch spot is used once (log 571a5159: it landed in the shallows, walked the long way back
						// round the dock to the launch spot and dived again - three times)
						s.HasLaunch = false;
						Log($"{name} diving in towards the ladder ({flatL:F1} m)");
					}
					return true;
				}
				if ((MHR_Water.DepthAt(ahead) < 0.4f || atLaunch) && ahead.y > me.y - 1.5f)
				{
					// dry ground on the way to the edge: walk straight at the ladder
					ai.transform.rotation = Quaternion.LookRotation(dirL);
					ai.MoveTowards(dirL, run: false);
					s.ProgressAt = now;
					return true;
				}
			}
		}
		// 1.5.92: standing in the water short of swimming depth with the ladder close: wade straight at it like you - the
		// ladder is used from the shallows once it's in reach (above), or it swims when it gets deep. No headway for 4 s ->
		// the normal route to the ladder.
		if (near != null && !s.WadeGaveUp && !c.IsSwimming() && flatL <= 15f && MHR_Water.DepthAt(me) > 0.3f)
		{
			if (s.WadeAt < 0f)
			{
				s.WadeAt = now;
				s.WadeBest = flatL;
				Log($"{name} wading to the ladder ({flatL:F1} m)");
			}
			if (flatL < s.WadeBest - 0.3f)
			{
				s.WadeBest = flatL;
				s.WadeAt = now;
				s.ProgressAt = now;
			}
			if (now - s.WadeAt > 4f)
			{
				s.WadeGaveUp = true;
				Log(name + " can't wade any closer to the ladder - going round");
			}
			else
			{
				Vector3 toW = near.transform.position - me;
				toW.y = 0f;
				if (toW.sqrMagnitude > 0.01f)
				{
					toW.Normalize();
					ai.transform.rotation = Quaternion.LookRotation(toW);
					ai.MoveTowards(toW, run: false);
				}
				return true;
			}
		}
		// your own steps onto the ship, once it's been stuck for a while
		if (!s.FootstepsTried && now - s.ProgressAt > StuckFor)
		{
			s.FootstepsTried = true;
			Player owner = MHR_Owner.Of(ai);
			if (owner != null && MHR_Follow.StepsOnto(owner.gameObject, ship, out s.StepFrom, out s.StepTo))
			{
				s.Footsteps = true;
				s.StepBest = float.MaxValue;
				s.ProgressAt = now;
				Log($"{name} can't find its own way aboard - following your steps (you got on at {s.StepFrom:F1})");
			}
		}
		if (s.Footsteps)
		{
			float toStep = Flat(me, s.StepFrom);
			if (toStep < s.StepBest - 0.3f)
			{
				s.StepBest = toStep;
				s.ProgressAt = now;
			}
			if (toStep > 0.6f)
			{
				MHR_Mover.Go(ai, dt, s.StepFrom, 0.4f, true, "Board", MHR_Mover.SpeedJog);
			}
			else
			{
				Vector3 dir = s.StepTo - me;
				dir.y = 0f;
				if (dir.sqrMagnitude > 0.01f)
				{
					dir.Normalize();
					if (s.StepTo.y > me.y + 0.4f && c.IsOnGround() && now >= s.NextJump)
					{
						s.NextJump = now + 1.5f;
						MHR_Jump.Leap(ai, c, dir);
						Log(name + " jumping where you did");
					}
					else
					{
						ai.transform.rotation = Quaternion.LookRotation(dir);
						ai.MoveTowards(dir, run: false);
					}
				}
			}
		}
		else
		{
			// walk (or swim) to the nearest ladder, else to our side of the ship
			Vector3 goal = nearest != null ? nearest.transform.position : (DeckSpot(ship, me, out Vector3 d2) ? d2 : ship.transform.position);
			bool toLaunch = s.HasLaunch && !c.IsSwimming() && !atLaunch;
			if (toLaunch)
			{
				goal = s.Launch;
			}
			MHR_Mover.Go(ai, dt, goal, toLaunch ? 0.6f : 0.5f, dist > 8f, "Board", dist > 8f ? MHR_Mover.SpeedRun : MHR_Mover.SpeedJog);
			if (toLaunch && MHR_Mover.Progressing(ai))
			{
				s.ProgressAt = now;
			}
			if (toLaunch && MHR_Mover.GaveUp(ai))
			{
				s.HasLaunch = false;
				MHR_Mover.Release(ai);
			}
		}
		if (now >= s.NextLog)
		{
			s.NextLog = now + 5f;
			CompanionsPlugin.Log?.LogDebug($"[Board] {name}: {dist:F1} m from the ship, nearest ladder {(nearest != null ? ld.ToString("F1") + " m" : "none")}{(s.Footsteps ? ", on your steps" : "")}");
		}
		// stuck too long: wait on the shore
		if (now - s.ProgressAt > StuckFor * 2f || now - s.Since > GiveUpAfter * 2f || (s.Footsteps && now - s.ProgressAt > StuckFor))
		{
			return GiveUp(ai, s, c, "no way found");
		}
		return true;
	}

	// 1.5.93: can it jump aboard from `from` (feet): the deck spot on our side (DeckSpot) 0.3-4 m across, no more than 1.6 m
	// up or 3 m down, within its running jump's reach (MHR_Jump.Reach) and nothing solid in the way
	private static bool JumpCheck(Character c, Ship ship, Vector3 from, Collider[] cols, out Vector3 dir, out float gap, out float dy, out float reach, out bool clear)
	{
		dir = Vector3.zero;
		gap = dy = reach = 0f;
		clear = false;
		if (!DeckSpot(ship, from, out Vector3 deck, cols))
		{
			return false;
		}
		gap = Flat(from, deck);
		dy = deck.y - from.y;
		dir = deck - from;
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.01f)
		{
			return false;
		}
		dir.Normalize();
		reach = MHR_Jump.Reach(c, dir, dy);
		clear = !Physics.Linecast(from + Vector3.up * 1.2f, deck + Vector3.up * 0.8f, Solid());
		return gap > 0.3f && gap <= 4f && dy <= 1.6f && dy >= -3f && reach >= gap + 0.3f && clear;
	}

	// on the ground (dock, shore, shallows) with the deck in jump reach: jump aboard. A near miss is logged once per boarding.
	private static bool TryJumpAboard(CompanionAI ai, S s, Character c, Vector3 me, Ship ship, float now, string name)
	{
		if (!c.IsOnGround() || c.IsSwimming() || now < s.NextJump)
		{
			return false;
		}
		bool ok = JumpCheck(c, ship, me, null, out Vector3 dir, out float gap, out float dy, out float reach, out bool clear);
		if (!ok)
		{
			if (!s.JumpLogged && gap > 0f && gap <= 6f)
			{
				s.JumpLogged = true;
				CompanionsPlugin.Log?.LogInfo($"[Board] {name} can't jump aboard from {me:F1}: {gap:F1} m across, {dy:+0.0;-0.0} m up, its jump reaches {reach:F1} m, {(clear ? "clear" : "something in the way")}");
			}
			return false;
		}
		s.NextJump = now + 1.5f;
		s.ProgressAt = now;
		if (s.HasTakeoff)
		{
			s.TakeoffJumps++;
		}
		MHR_Jump.Leap(ai, c, dir);
		Log($"{name} jumping aboard ({gap:F1} m across, {dy:+0.0;-0.0} m up)");
		return true;
	}

	// dry ground within 15 m (1 m grid) with the deck in jump reach, nearest first
	private static bool JumpSpot(Character c, Ship ship, Vector3 me, out Vector3 spot)
	{
		spot = me;
		Collider[] all = ship.GetComponentsInChildren<Collider>();
		List<Collider> hull = new List<Collider>();
		foreach (Collider col in all)
		{
			if (col != null && !col.isTrigger && ((1 << col.gameObject.layer) & Vehicle()) != 0 && !(col is MeshCollider mc && !mc.convex))
			{
				hull.Add(col);
			}
		}
		if (hull.Count == 0)
		{
			return false;
		}
		Collider[] cols = hull.ToArray();
		int mask = Solid() | Vehicle();
		float best = float.MaxValue;
		for (int x = -15; x <= 15; x++)
		{
			for (int z = -15; z <= 15; z++)
			{
				if (x * x + z * z > 225)
				{
					continue;
				}
				Vector3 q = me + new Vector3(x, 0f, z);
				float d = Flat(q, me);
				if (d >= best)
				{
					continue;
				}
				// close enough to the hull to be worth the full check
				float near = float.MaxValue;
				foreach (Collider col in cols)
				{
					Vector3 p;
					try
					{
						p = col.ClosestPoint(q);
					}
					catch
					{
						continue;
					}
					near = Mathf.Min(near, Flat(p, q));
				}
				if (near > 3.5f)
				{
					continue;
				}
				if (!Physics.Raycast(new Vector3(q.x, me.y + 6f, q.z), Vector3.down, out RaycastHit hit, 15f, mask) || ((1 << hit.collider.gameObject.layer) & Vehicle()) != 0)
				{
					continue;
				}
				q = hit.point;
				if (MHR_Water.DepthAt(q) >= 0.1f || Mathf.Abs(q.y - me.y) > 4f)
				{
					continue;
				}
				if (JumpCheck(c, ship, q, cols, out _, out _, out _, out _, out _))
				{
					best = d;
					spot = q;
				}
			}
		}
		return best < float.MaxValue;
	}

	// 1.5.83: gives up for now and waits on dry land facing the ship - not where it stopped (it stood in the water)
	private static bool GiveUp(CompanionAI ai, S s, Character c, string why)
	{
		Ship ship = s.Ship;
		bool auto = s.Auto;
		Log(c.m_name + " can't get aboard (" + why + ") - waiting on the shore");
		Cancel(ai);
		s.HoldShip = ship;
		s.HoldUntil = Time.time + HoldFor;
		s.HasShore = DryShore(ai.transform.position, ship != null ? ship.transform.position : ai.transform.position, out s.Shore);
		if (!s.HasShore)
		{
			ai.StopMoving();
		}
		if (!auto)
		{
			ai.BoardingEnded();
		}
		return false;
	}

	// the nearest dry ground within 15 m (rings of 12 directions), a little towards the ship when it's a tie
	private static bool DryShore(Vector3 me, Vector3 ship, out Vector3 spot)
	{
		spot = me;
		if (MHR_Water.DepthAt(me) < 0.1f)
		{
			return false;
		}
		float best = float.MaxValue;
		bool found = false;
		Vector3 toShip = ship - me;
		toShip.y = 0f;
		foreach (float r in new float[6] { 2f, 4f, 6f, 9f, 12f, 15f })
		{
			for (int k = 0; k < 12; k++)
			{
				Vector3 d = Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward;
				Vector3 p = me + d * r;
				p.y = MHR_Water.GroundAt(p);
				if (MHR_Water.DepthAt(p) >= 0.1f || p.y > me.y + 3f)
				{
					continue;
				}
				float score = r - (toShip.sqrMagnitude > 0.01f ? Vector3.Dot(d, toShip.normalized) : 0f);
				if (score < best)
				{
					best = score;
					spot = p;
					found = true;
				}
			}
			if (found)
			{
				break;
			}
		}
		return found;
	}
}
