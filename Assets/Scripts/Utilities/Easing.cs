// Basic Easing library for simple easing functionality in Unity
// Unlicense license [whole license stated at the bottom of the file]

// Based on Robert Penner's easing functions (http://www.robertpenner.com/easing/) and AHEasing (https://github.com/warrenm/AHEasing)
// Modified for Unity by Jiri Stary (https://github.com/jiristary)

using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityEasing
{
	public enum Ease
	{
		Linear,

		// Quadratic x^2
		InQuad,
		OutQuad,
		InOutQuad,

		// Cubic x^3
		InCubic,
		OutCubic,
		InOutCubic,

		// Quartic x^4
		InQuart,
		OutQuart,
		InOutQuart,

		// Quintic x^5
		InQuint,
		OutQuint,
		InOutQuint,

		// Sine
		InSine,
		OutSine,
		InOutSine,

		// Circular
		InCirc,
		OutCirc,
		InOutCirc,

		// Exponential
		InExpo,
		OutExpo,
		InOutExpo,

		// Elastic
		InElastic,
		OutElastic,
		InOutElastic,

		// Back
		InBack,
		OutBack,
		InOutBack,

		// Bounce
		InBounce,
		OutBounce,
		InOutBounce,
	}

	public static class Easing
	{
		private const float PI = Mathf.PI;
		private const float HALF_PI = Mathf.PI * 0.5f;

		//==================================================================================================

		/// <summary>
		/// Get eased value between 0 and 1
		/// </summary>
		public static float Get(this Ease easeType, float value)
		{
			if (value <= 0f)
				return 0f;

			if (value >= 1f)
				return 1f;

			return GetUnclamped(value, easeType);
		}

		/// <summary>
		/// Get unclamped eased value
		/// </summary>
		public static float GetUnclamped(this Ease easeType, float value)
		{
			return GetUnclamped(value, easeType);
		}

		/// <summary>
		/// Get eased value between 0 and 1
		/// </summary>
		public static float Get(float value, Ease easeType)
		{
			if (value <= 0f)
				return 0f;

			if (value >= 1f)
				return 1f;

			return GetUnclamped(value, easeType);
		}

		/// <summary>
		/// Get unclamped eased value
		/// </summary>
		public static float GetUnclamped(float value, Ease easeType)
		{
			switch (easeType)
			{
				default:
				case Ease.Linear:       return Linear(value);
				case Ease.OutQuad:      return QuadraticEaseOut(value);
				case Ease.InQuad:       return QuadraticEaseIn(value);
				case Ease.InOutQuad:    return QuadraticEaseInOut(value);
				case Ease.InCubic:      return CubicEaseIn(value);
				case Ease.OutCubic:     return CubicEaseOut(value);
				case Ease.InOutCubic:   return CubicEaseInOut(value);
				case Ease.InQuart:      return QuarticEaseIn(value);
				case Ease.OutQuart:     return QuarticEaseOut(value);
				case Ease.InOutQuart:   return QuarticEaseInOut(value);
				case Ease.InQuint:      return QuinticEaseIn(value);
				case Ease.OutQuint:     return QuinticEaseOut(value);
				case Ease.InOutQuint:   return QuinticEaseInOut(value);
				case Ease.InSine:       return SineEaseIn(value);
				case Ease.OutSine:      return SineEaseOut(value);
				case Ease.InOutSine:    return SineEaseInOut(value);
				case Ease.InCirc:       return CircularEaseIn(value);
				case Ease.OutCirc:      return CircularEaseOut(value);
				case Ease.InOutCirc:    return CircularEaseInOut(value);
				case Ease.InExpo:       return ExponentialEaseIn(value);
				case Ease.OutExpo:      return ExponentialEaseOut(value);
				case Ease.InOutExpo:    return ExponentialEaseInOut(value);
				case Ease.InElastic:    return ElasticEaseIn(value);
				case Ease.OutElastic:   return ElasticEaseOut(value);
				case Ease.InOutElastic: return ElasticEaseInOut(value);
				case Ease.InBack:       return BackEaseIn(value);
				case Ease.OutBack:      return BackEaseOut(value);
				case Ease.InOutBack:    return BackEaseInOut(value);
				case Ease.InBounce:     return BounceEaseIn(value);
				case Ease.OutBounce:    return BounceEaseOut(value);
				case Ease.InOutBounce:  return BounceEaseInOut(value);
			}
		}

		//==================================================================================================

		/// <summary>
		/// Modeled after the line y = x
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Linear(float p)
		{
			return p;
		}

		/// <summary>
		/// Modeled after the parabola y = x^2
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuadraticEaseIn(float p)
		{
			return p * p;
		}

		/// <summary>
		/// Modeled after the parabola y = -x^2 + 2x
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuadraticEaseOut(float p)
		{
			return -(p * (p - 2f));
		}

		/// <summary>
		/// Modeled after the piecewise quadratic
		/// y = (1/2)((2x)^2)             ; [0, 0.5)
		/// y = -(1/2)((2x-1)*(2x-3) - 1) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuadraticEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 2f * p * p;
			}
			else
			{
				return (-2f * p * p) + (4f * p) - 1f;
			}
		}

		/// <summary>
		/// Modeled after the cubic y = x^3
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CubicEaseIn(float p)
		{
			return p * p * p;
		}

		/// <summary>
		/// Modeled after the cubic y = (x - 1)^3 + 1
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CubicEaseOut(float p)
		{
			float f = p - 1f;
			return f * f * f + 1f;
		}

		/// <summary>
		/// Modeled after the piecewise cubic
		/// y = (1/2)((2x)^3)       ; [0, 0.5)
		/// y = (1/2)((2x-2)^3 + 2) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CubicEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 4f * p * p * p;
			}
			else
			{
				float f = ((2f * p) - 2f);
				return 0.5f * f * f * f + 1f;
			}
		}

		/// <summary>
		/// Modeled after the quartic x^4
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuarticEaseIn(float p)
		{
			return p * p * p * p;
		}

		/// <summary>
		/// Modeled after the quartic y = 1 - (x - 1)^4
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuarticEaseOut(float p)
		{
			float f = (p - 1f);
			return f * f * f * (1f - p) + 1f;
		}

		/// <summary>
		// Modeled after the piecewise quartic
		// y = (1/2)((2x)^4)        ; [0, 0.5)
		// y = -(1/2)((2x-2)^4 - 2) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuarticEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 8f * p * p * p * p;
			}
			else
			{
				float f = (p - 1f);
				return -8f * f * f * f * f + 1f;
			}
		}

		/// <summary>
		/// Modeled after the quintic y = x^5
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuinticEaseIn(float p)
		{
			return p * p * p * p * p;
		}

		/// <summary>
		/// Modeled after the quintic y = (x - 1)^5 + 1
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuinticEaseOut(float p)
		{
			float f = (p - 1f);
			return f * f * f * f * f + 1f;
		}

		/// <summary>
		/// Modeled after the piecewise quintic
		/// y = (1/2)((2x)^5)       ; [0, 0.5)
		/// y = (1/2)((2x-2)^5 + 2) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float QuinticEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 16f * p * p * p * p * p;
			}
			else
			{
				float f = ((2f * p) - 2f);
				return 0.5f * f * f * f * f * f + 1f;
			}
		}

		/// <summary>
		/// Modeled after quarter-cycle of sine wave
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float SineEaseIn(float p)
		{
			return Mathf.Sin((p - 1f) * HALF_PI) + 1;
		}

		/// <summary>
		/// Modeled after quarter-cycle of sine wave (different phase)
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float SineEaseOut(float p)
		{
			return Mathf.Sin(p * HALF_PI);
		}

		/// <summary>
		/// Modeled after half sine wave
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float SineEaseInOut(float p)
		{
			return 0.5f * (1f - Mathf.Cos(p * PI));
		}

		/// <summary>
		/// Modeled after shifted quadrant IV of unit circle
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CircularEaseIn(float p)
		{
			return 1f - Mathf.Sqrt(1f - (p * p));
		}

		/// <summary>
		/// Modeled after shifted quadrant II of unit circle
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CircularEaseOut(float p)
		{
			return Mathf.Sqrt((2f - p) * p);
		}

		/// <summary>
		/// Modeled after the piecewise circular function
		/// y = (1/2)(1 - Math.Sqrt(1 - 4x^2))           ; [0, 0.5)
		/// y = (1/2)(Math.Sqrt(-(2x - 3)*(2x - 1)) + 1) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float CircularEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 0.5f * (1f - Mathf.Sqrt(1f - 4f * (p * p)));
			}
			else
			{
				return 0.5f * (Mathf.Sqrt(-((2f * p) - 3f) * ((2f * p) - 1f)) + 1f);
			}
		}

		/// <summary>
		/// Modeled after the exponential function y = 2^(10(x - 1))
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ExponentialEaseIn(float p)
		{
			return p == 0f ? p : Mathf.Pow(2f, 10f * (p - 1f));
		}

		/// <summary>
		/// Modeled after the exponential function y = -2^(-10x) + 1
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ExponentialEaseOut(float p)
		{
			return p == 1f ? p : 1f - Mathf.Pow(2f, -10f * p);
		}

		/// <summary>
		/// Modeled after the piecewise exponential
		/// y = (1/2)2^(10(2x - 1))         ; [0,0.5)
		/// y = -(1/2)*2^(-10(2x - 1))) + 1 ; [0.5,1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ExponentialEaseInOut(float p)
		{
			if (p == 0f || p == 1f)
				return p;

			if (p < 0.5f)
			{
				return 0.5f * Mathf.Pow(2, (20f * p) - 10f);
			}
			else
			{
				return -0.5f * Mathf.Pow(2, (-20f * p) + 10f) + 1f;
			}
		}

		/// <summary>
		/// Modeled after the damped sine wave y = sin(13pi/2*x)*Math.Pow(2, 10 * (x - 1))
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ElasticEaseIn(float p)
		{
			return Mathf.Sin(13f * HALF_PI * p) * Mathf.Pow(2f, 10f * (p - 1f));
		}

		/// <summary>
		/// Modeled after the damped sine wave y = sin(-13pi/2*(x + 1))*Math.Pow(2, -10x) + 1
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ElasticEaseOut(float p)
		{
			return Mathf.Sin(-13f * HALF_PI * (p + 1f)) * Mathf.Pow(2f, -10f * p) + 1f;
		}

		/// <summary>
		/// Modeled after the piecewise exponentially-damped sine wave:
		/// y = (1/2)*sin(13pi/2*(2*x))*Math.Pow(2, 10 * ((2*x) - 1))      ; [0,0.5)
		/// y = (1/2)*(sin(-13pi/2*((2x-1)+1))*Math.Pow(2,-10(2*x-1)) + 2) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ElasticEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 0.5f * Mathf.Sin(13 * HALF_PI * (2 * p)) * Mathf.Pow(2f, 10f * ((2f * p) - 1));
			}
			else
			{
				return 0.5f * (Mathf.Sin(-13f * HALF_PI * ((2f * p - 1f) + 1f)) * Mathf.Pow(2f, -10f * (2f * p - 1f)) + 2f);
			}
		}

		/// <summary>
		/// Modeled after the overshooting cubic y = x^3-x*sin(x*pi)
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BackEaseIn(float p)
		{
			return p * p * p - p * Mathf.Sin(p * PI);
		}

		/// <summary>
		/// Modeled after overshooting cubic y = 1-((1-x)^3-(1-x)*sin((1-x)*pi))
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BackEaseOut(float p)
		{
			float f = 1f - p;
			return 1f - (f * f * f - f * Mathf.Sin(f * PI));
		}

		/// <summary>
		/// Modeled after the piecewise overshooting cubic function:
		/// y = (1/2)*((2x)^3-(2x)*sin(2*x*pi))           ; [0, 0.5)
		/// y = (1/2)*(1-((1-x)^3-(1-x)*sin((1-x)*pi))+1) ; [0.5, 1]
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BackEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				float f = 2f * p;
				return 0.5f * (f * f * f - f * Mathf.Sin(f * PI));
			}
			else
			{
				float f = (1f - (2f * p - 1f));
				return 0.5f * (1f - (f * f * f - f * Mathf.Sin(f * PI))) + 0.5f;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BounceEaseIn(float p)
		{
			return 1f - BounceEaseOut(1f - p);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BounceEaseOut(float p)
		{
			if (p < 4f / 11f)
			{
				return (121f * p * p) / 16f;
			}
			else if (p < 8f / 11f)
			{
				return (363f / 40f * p * p) - (99f / 10f * p) + 17f / 5f;
			}
			else if (p < 9f / 10f)
			{
				return (4356f / 361f * p * p) - (35442f / 1805f * p) + 16061f / 1805f;
			}
			else
			{
				return (54f / 5f * p * p) - (513f / 25f * p) + 268f / 25f;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BounceEaseInOut(float p)
		{
			if (p < 0.5f)
			{
				return 0.5f * BounceEaseIn(p * 2f);
			}
			else
			{
				return 0.5f * BounceEaseOut(p * 2f - 1f) + 0.5f;
			}
		}
	}
}

// This is free and unencumbered software released into the public domain.
//
// Anyone is free to copy, modify, publish, use, compile, sell, or
// distribute this software, either in source code form or as a compiled
// binary, for any purpose, commercial or non-commercial, and by any
// means.
//
// In jurisdictions that recognize copyright laws, the author or authors
// of this software dedicate any and all copyright interest in the
// software to the public domain. We make this dedication for the benefit
// of the public at large and to the detriment of our heirs and
// successors. We intend this dedication to be an overt act of
// relinquishment in perpetuity of all present and future rights to this
// software under copyright law.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// For more information, please refer to <http://unlicense.org/>
