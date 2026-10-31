using System;
using UnityEngine;

namespace Companions;

internal struct FarmZone
{
	public Vector3 Center;

	public float HalfSize;

	public string CropSeed;

	public float Rotation;

	// 1.5.42: not a zone the player set - a patch of ground they tilled, used as an "Any" zone ([Farming] PlantTilledGround)
	public bool Tilled;

	public bool IsValid => HalfSize > 0f;

	public bool Contains(Vector3 pos)
	{
		WorldToLocal(pos, out var lx, out var lz);
		if (lx >= 0f - HalfSize && lx <= HalfSize && lz >= 0f - HalfSize)
		{
			return lz <= HalfSize;
		}
		return false;
	}

	public Vector3 LocalToWorld(float lx, float lz)
	{
		float f = Rotation * (Mathf.PI / 180f);
		float num = Mathf.Cos(f);
		float num2 = Mathf.Sin(f);
		return new Vector3(Center.x + lx * num - lz * num2, Center.y, Center.z + lx * num2 + lz * num);
	}

	public void WorldToLocal(Vector3 pos, out float lx, out float lz)
	{
		float num = pos.x - Center.x;
		float num2 = pos.z - Center.z;
		float f = (0f - Rotation) * (Mathf.PI / 180f);
		float num3 = Mathf.Cos(f);
		float num4 = Mathf.Sin(f);
		lx = num * num3 - num2 * num4;
		lz = num * num4 + num2 * num3;
	}

	public void GetCorners(out Vector3 c0, out Vector3 c1, out Vector3 c2, out Vector3 c3)
	{
		float halfSize = HalfSize;
		c0 = LocalToWorld(0f - halfSize, 0f - halfSize);
		c1 = LocalToWorld(halfSize, 0f - halfSize);
		c2 = LocalToWorld(halfSize, halfSize);
		c3 = LocalToWorld(0f - halfSize, halfSize);
	}
}
