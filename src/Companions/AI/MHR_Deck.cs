using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.85: a small route planner for ship decks (user: "do we need a route planner on the boat. They tend to be small but
// the NPC might need to know where it is and where to go if it is going to use ranged weapons on the boat ... A small
// route planning should only run while on a boat" / "a grid for each ship type"). The game's route map never covers
// ships (they move), so on a deck the companion could only steer straight - into the mast, or over the side.
//
// One grid per ship type (prefab), in the ship's own frame so it holds while the ship moves and rolls: half-metre cells,
// walkable where the deck is under it (a ray along the ship's down hits that ship's flat deck), there's head room, and the
// deck carries on half a metre each way (off the rail). Built the first time a companion is aboard that type, kept after.
// Paths are A* over the cells (8 directions, no corner cutting), followed in the ship's frame every frame.
internal static class MHR_Deck
{
	internal class Grid
	{
		public string Type;
		public Vector3 Min;
		public int W;
		public int H;
		public bool[] Walk;
		public float[] Y;
		public int Count;
	}

	private class Walker
	{
		public Ship Ship;
		public readonly List<Vector3> Path = new List<Vector3>();
		public int Idx;
		public Vector3 GoalLocal;
		public float NextPlan;
		public float ProgressAt;
		public float BestLeft = float.MaxValue;
		public bool Walking;
		public bool StuckLogged;
	}

	private const float Cell = 0.5f;

	private static readonly Dictionary<string, Grid> s_grids = new Dictionary<string, Grid>();

	private static readonly Dictionary<int, Walker> s_walkers = new Dictionary<int, Walker>();

	private static int s_vehicle = -1;

	private static int s_block = -1;

	private static int Vehicle()
	{
		if (s_vehicle == -1)
		{
			s_vehicle = LayerMask.GetMask("vehicle");
		}
		return s_vehicle;
	}

	private static int Block()
	{
		if (s_block == -1)
		{
			s_block = LayerMask.GetMask("vehicle", "Default", "static_solid", "piece");
		}
		return s_block;
	}

	private static string TypeOf(Ship ship)
	{
		return Utils.GetPrefabName(ship.gameObject);
	}

	private static Ship ShipOf(Collider col)
	{
		if (col == null)
		{
			return null;
		}
		return col.attachedRigidbody != null ? col.attachedRigidbody.GetComponent<Ship>() : col.GetComponentInParent<Ship>();
	}

	// the deck grid for this ship's type (built on first use)
	internal static Grid For(Ship ship)
	{
		if (ship == null)
		{
			return null;
		}
		string type = TypeOf(ship);
		if (s_grids.TryGetValue(type, out Grid g))
		{
			return g;
		}
		g = Build(ship, type);
		s_grids[type] = g;
		return g;
	}

	private static Grid Build(Ship ship, string type)
	{
		Transform t = ship.transform;
		Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		foreach (Collider col in ship.GetComponentsInChildren<Collider>())
		{
			if (col == null || col.isTrigger || ((1 << col.gameObject.layer) & Vehicle()) == 0)
			{
				continue;
			}
			Bounds b = col.bounds;
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
				Vector3 l = t.InverseTransformPoint(corner);
				min = Vector3.Min(min, l);
				max = Vector3.Max(max, l);
			}
		}
		Grid g = new Grid
		{
			Type = type
		};
		if (min.x > max.x)
		{
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Deck] no deck colliders on '" + type + "' - no grid");
			return g;
		}
		g.Min = new Vector3(min.x, 0f, min.z);
		g.W = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / Cell));
		g.H = Mathf.Max(1, Mathf.CeilToInt((max.z - min.z) / Cell));
		g.Walk = new bool[g.W * g.H];
		g.Y = new float[g.W * g.H];
		bool[] deck = new bool[g.W * g.H];
		float top = max.y + 2f;
		float depth = max.y - min.y + 4f;
		for (int x = 0; x < g.W; x++)
		{
			for (int z = 0; z < g.H; z++)
			{
				Vector3 local = new Vector3(g.Min.x + (x + 0.5f) * Cell, top, g.Min.z + (z + 0.5f) * Cell);
				Vector3 from = t.TransformPoint(local);
				if (!Physics.Raycast(from, -t.up, out RaycastHit hit, depth, Vehicle()) || ShipOf(hit.collider) != ship || Vector3.Dot(hit.normal, t.up) < 0.7f)
				{
					continue;
				}
				int i = x * g.H + z;
				deck[i] = true;
				g.Y[i] = t.InverseTransformPoint(hit.point).y;
				// head room: nothing solid from knee to head height
				Vector3 a = hit.point + t.up * 0.6f;
				Vector3 b = hit.point + t.up * 1.6f;
				if (Physics.CheckCapsule(a, b, 0.25f, Block(), QueryTriggerInteraction.Ignore))
				{
					deck[i] = false;
				}
			}
		}
		// off the rail: the deck carries on a cell each way
		for (int x = 0; x < g.W; x++)
		{
			for (int z = 0; z < g.H; z++)
			{
				int i = x * g.H + z;
				if (!deck[i])
				{
					continue;
				}
				bool inner = x > 0 && x < g.W - 1 && z > 0 && z < g.H - 1 && deck[(x - 1) * g.H + z] && deck[(x + 1) * g.H + z] && deck[x * g.H + z - 1] && deck[x * g.H + z + 1];
				g.Walk[i] = inner;
				if (inner)
				{
					g.Count++;
				}
			}
		}
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo($"[Deck] grid for '{type}': {g.W} x {g.H} cells of {Cell} m, {g.Count} to walk on");
		return g;
	}

	private static bool Cell2(Grid g, Vector3 local, out int x, out int z)
	{
		x = Mathf.FloorToInt((local.x - g.Min.x) / Cell);
		z = Mathf.FloorToInt((local.z - g.Min.z) / Cell);
		return x >= 0 && z >= 0 && x < g.W && z < g.H;
	}

	private static Vector3 Center(Grid g, int x, int z)
	{
		return new Vector3(g.Min.x + (x + 0.5f) * Cell, g.Y[x * g.H + z], g.Min.z + (z + 0.5f) * Cell);
	}

	// the walkable cell nearest a point in the ship's frame
	internal static bool Nearest(Grid g, Vector3 local, out int bx, out int bz)
	{
		bx = bz = -1;
		if (g == null || g.Walk == null || g.Count == 0)
		{
			return false;
		}
		float bd = float.MaxValue;
		for (int x = 0; x < g.W; x++)
		{
			for (int z = 0; z < g.H; z++)
			{
				if (!g.Walk[x * g.H + z])
				{
					continue;
				}
				Vector3 c = Center(g, x, z);
				float d = (c.x - local.x) * (c.x - local.x) + (c.z - local.z) * (c.z - local.z);
				if (d < bd)
				{
					bd = d;
					bx = x;
					bz = z;
				}
			}
		}
		return bx >= 0;
	}

	// the deck spot (ship's frame) nearest a world point - a seat's stand spot, the side nearest you
	internal static bool SpotNear(Ship ship, Vector3 world, out Vector3 local)
	{
		local = Vector3.zero;
		Grid g = For(ship);
		if (g == null || !Nearest(g, ship.transform.InverseTransformPoint(world), out int x, out int z))
		{
			return false;
		}
		local = Center(g, x, z);
		return true;
	}

	// 1.5.89: the nearest spot along the side of the ship that faces `world` (you, ashore): an edge cell (deck missing next to
	// it) whose outward side points your way - from a seat at the bow that's a step sideways, not the length of the deck
	// (log 81234e48: ~10 s walking to the deck spot nearest you before jumping off)
	internal static bool SideSpot(Ship ship, Vector3 meWorld, Vector3 world, out Vector3 local)
	{
		local = Vector3.zero;
		Grid g = For(ship);
		if (g == null || g.Walk == null || g.Count == 0)
		{
			return false;
		}
		Transform t = ship.transform;
		Vector3 me = t.InverseTransformPoint(meWorld);
		Vector3 you = t.InverseTransformPoint(world);
		float bd = float.MaxValue;
		for (int x = 0; x < g.W; x++)
		{
			for (int z = 0; z < g.H; z++)
			{
				if (!g.Walk[x * g.H + z])
				{
					continue;
				}
				Vector3 c = Center(g, x, z);
				Vector2 toYou = new Vector2(you.x - c.x, you.z - c.z);
				if (toYou.sqrMagnitude < 0.01f)
				{
					continue;
				}
				toYou.Normalize();
				bool edge = false;
				for (int k = 0; k < 4 && !edge; k++)
				{
					int dx = k == 0 ? 1 : (k == 1 ? -1 : 0);
					int dz = k == 2 ? 1 : (k == 3 ? -1 : 0);
					int nx = x + dx;
					int nz = z + dz;
					bool open = nx < 0 || nz < 0 || nx >= g.W || nz >= g.H || !g.Walk[nx * g.H + nz];
					edge = open && Vector2.Dot(new Vector2(dx, dz), toYou) > 0.5f;
				}
				if (!edge)
				{
					continue;
				}
				float d = (c.x - me.x) * (c.x - me.x) + (c.z - me.z) * (c.z - me.z);
				if (d < bd)
				{
					bd = d;
					local = c;
				}
			}
		}
		return bd < float.MaxValue;
	}

	// A* over the cells, 8 directions, no cutting corners; the path in the ship's frame
	private static bool Plan(Grid g, Vector3 fromLocal, Vector3 toLocal, List<Vector3> path)
	{
		path.Clear();
		if (!Nearest(g, fromLocal, out int sx, out int sz) || !Nearest(g, toLocal, out int gx, out int gz))
		{
			return false;
		}
		int n = g.W * g.H;
		float[] cost = new float[n];
		int[] came = new int[n];
		bool[] closed = new bool[n];
		for (int i = 0; i < n; i++)
		{
			cost[i] = float.MaxValue;
			came[i] = -1;
		}
		int start = sx * g.H + sz;
		int goal = gx * g.H + gz;
		cost[start] = 0f;
		List<int> open = new List<int> { start };
		while (open.Count > 0)
		{
			int bi = 0;
			float bf = float.MaxValue;
			for (int k = 0; k < open.Count; k++)
			{
				int c = open[k];
				float h = Mathf.Sqrt((c / g.H - gx) * (c / g.H - gx) + (c % g.H - gz) * (c % g.H - gz));
				if (cost[c] + h < bf)
				{
					bf = cost[c] + h;
					bi = k;
				}
			}
			int cur = open[bi];
			open.RemoveAt(bi);
			if (cur == goal)
			{
				break;
			}
			closed[cur] = true;
			int cx = cur / g.H;
			int cz = cur % g.H;
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					if (dx == 0 && dz == 0)
					{
						continue;
					}
					int nx = cx + dx;
					int nz = cz + dz;
					if (nx < 0 || nz < 0 || nx >= g.W || nz >= g.H)
					{
						continue;
					}
					int ni = nx * g.H + nz;
					if (!g.Walk[ni] || closed[ni])
					{
						continue;
					}
					if (dx != 0 && dz != 0 && (!g.Walk[cx * g.H + nz] || !g.Walk[nx * g.H + cz]))
					{
						continue;
					}
					float nc = cost[cur] + ((dx != 0 && dz != 0) ? 1.414f : 1f);
					if (nc < cost[ni])
					{
						cost[ni] = nc;
						came[ni] = cur;
						if (!open.Contains(ni))
						{
							open.Add(ni);
						}
					}
				}
			}
		}
		if (goal != start && came[goal] < 0)
		{
			return false;
		}
		for (int c = goal; c >= 0; c = (c == start) ? -1 : came[c])
		{
			path.Insert(0, Center(g, c / g.H, c % g.H));
		}
		return path.Count > 0;
	}

	private static Walker GetWalker(CompanionAI ai)
	{
		int id = ai.GetInstanceID();
		if (!s_walkers.TryGetValue(id, out Walker w))
		{
			w = new Walker();
			s_walkers[id] = w;
		}
		return w;
	}

	internal static void Release(CompanionAI ai)
	{
		if (ai != null && s_walkers.TryGetValue(ai.GetInstanceID(), out Walker w))
		{
			if (w.Walking)
			{
				ai.GetComponent<Character>()?.SetWalk(false);
			}
			s_walkers.Remove(ai.GetInstanceID());
		}
	}

	// walks on the deck to a spot in the ship's frame; true once within `dist` of it. Only for a companion on that deck.
	// 1.5.86: jogs; walks only for the last `walkLast` metres (a seat) - was the last 3 m (log 79fb1bde: "moving very slow")
	// 1.5.89: stuck after `stuckAfter` s without getting 0.2 m closer; the start and a stuck walk are logged
	internal static bool Walk(CompanionAI ai, Ship ship, Vector3 goalLocal, float dist, out bool stuck, float walkLast = 1f, float stuckAfter = 5f)
	{
		stuck = false;
		Grid g = For(ship);
		Transform t = ship.transform;
		Character c = ai.GetComponent<Character>();
		Vector3 me = t.InverseTransformPoint(ai.transform.position);
		float left = new Vector2(me.x - goalLocal.x, me.z - goalLocal.z).magnitude;
		if (left <= dist)
		{
			ai.StopMoving();
			Release(ai);
			return true;
		}
		Walker w = GetWalker(ai);
		float now = Time.time;
		if (w.Ship != ship || (w.GoalLocal - goalLocal).sqrMagnitude > 0.25f || now >= w.NextPlan)
		{
			bool fresh = w.Ship != ship || (w.GoalLocal - goalLocal).sqrMagnitude > 0.25f;
			w.Ship = ship;
			w.GoalLocal = goalLocal;
			w.NextPlan = now + 2f;
			if (fresh)
			{
				w.ProgressAt = now;
				w.BestLeft = float.MaxValue;
				w.StuckLogged = false;
			}
			bool planned = g != null && Plan(g, me, goalLocal, w.Path);
			if (!planned)
			{
				w.Path.Clear();
				w.Path.Add(goalLocal);
			}
			w.Idx = 0;
			if (fresh)
			{
				float len = 0f;
				Vector3 prev = me;
				foreach (Vector3 p in w.Path)
				{
					len += new Vector2(p.x - prev.x, p.z - prev.z).magnitude;
					prev = p;
				}
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogDebug($"[Deck] {c?.m_name} walking {len:F1} m over {w.Path.Count} cells from {me:F1} to {goalLocal:F1} on the ship{(planned ? "" : " (no route on the grid - straight)")}");
			}
		}
		if (left < w.BestLeft - 0.2f)
		{
			w.BestLeft = left;
			w.ProgressAt = now;
		}
		if (now - w.ProgressAt > stuckAfter)
		{
			stuck = true;
			if (!w.StuckLogged)
			{
				w.StuckLogged = true;
				if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo($"[Deck] {c?.m_name} stuck at {me:F1} on the ship, {left:F1} m from {goalLocal:F1}");
			}
		}
		while (w.Idx < w.Path.Count - 1 && new Vector2(me.x - w.Path[w.Idx].x, me.z - w.Path[w.Idx].z).magnitude < 0.35f)
		{
			w.Idx++;
		}
		Vector3 next = t.TransformPoint(w.Path[Mathf.Min(w.Idx, w.Path.Count - 1)]);
		Vector3 dir = next - ai.transform.position;
		dir -= t.up * Vector3.Dot(dir, t.up);
		if (dir.sqrMagnitude < 0.0001f)
		{
			return false;
		}
		dir.Normalize();
		bool walk = left < walkLast;
		if (c != null && walk != w.Walking)
		{
			c.SetWalk(walk);
			w.Walking = walk;
		}
		ai.MoveTowards(dir, run: false);
		return false;
	}
}
