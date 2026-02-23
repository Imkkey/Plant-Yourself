using System.Runtime.CompilerServices;
using UnityEngine;

namespace Projectiles
{
	public static class MathUtility
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Map(float inMin, float inMax, float outMin, float outMax, float value)
		{
			if (value <= inMin)
				return outMin;

			if (value >= inMax)
				return outMax;

			return (outMax - outMin) * ((value - inMin) / (inMax - inMin)) + outMin;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Map(Vector2 inRange, Vector2 outRange, float value)
		{
			return Map(inRange.x, inRange.y, outRange.x, outRange.y, value);
		}
	}
}
