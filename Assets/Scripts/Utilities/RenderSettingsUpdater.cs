using UnityEngine;
using UnityEngine.Rendering;

namespace Projectiles
{
	// This utility helps set correct scene lighting when multi-peer mode is used
	public sealed class RenderSettingsUpdater : MonoBehaviour
	{
		public Material    Skybox;
		public Light       Sun;

		public bool        Fog;
		public FogMode     FogMode;
		public Color       FogColor;
		public float       FogDensity;
		public float       FogStartDistance;
		public float       FogEndDistance;

		public AmbientMode AmbientMode;
		[ColorUsage(true, true)]
		public Color       AmbientLight;
		public float       AmbientIntensity;
		[ColorUsage(true, true)]
		public Color       AmbientEquatorColor;
		[ColorUsage(true, true)]
		public Color       AmbientGroundColor;
		[ColorUsage(true, true)]
		public Color       AmbientSkyColor;
		public SphericalHarmonicsL2 AmbientProbe;

		public Color       SubtractiveShadowColor;

		public float       ReflectionIntensity;
		public int         ReflectionBounces;
		public DefaultReflectionMode DefaultReflectionMode;
		public int         DefaultReflectionResolution;

		public float       HaloStrength;
		public float       FlareStrength;
		public float       FlareFadeSpeed;

		public void ApplySettings()
		{
			RenderSettings.skybox                 = Skybox;
			RenderSettings.sun                    = Sun;

			RenderSettings.fog                    = Fog;
			RenderSettings.fogMode                = FogMode;
			RenderSettings.fogColor               = FogColor;
			RenderSettings.fogDensity             = FogDensity;
			RenderSettings.fogStartDistance       = FogStartDistance;
			RenderSettings.fogEndDistance         = FogEndDistance;

			RenderSettings.ambientMode            = AmbientMode;
			RenderSettings.ambientLight           = AmbientLight;
			RenderSettings.ambientIntensity       = AmbientIntensity;
			RenderSettings.ambientEquatorColor    = AmbientEquatorColor;
			RenderSettings.ambientGroundColor     = AmbientGroundColor;
			RenderSettings.ambientSkyColor        = AmbientSkyColor;
			RenderSettings.ambientProbe           = AmbientProbe;

			RenderSettings.subtractiveShadowColor = SubtractiveShadowColor;

			RenderSettings.reflectionIntensity    = ReflectionIntensity;
			RenderSettings.reflectionBounces      = ReflectionBounces;
			RenderSettings.defaultReflectionMode  = DefaultReflectionMode;
			RenderSettings.defaultReflectionResolution = DefaultReflectionResolution;

			RenderSettings.haloStrength           = HaloStrength;
			RenderSettings.flareStrength          = FlareStrength;
			RenderSettings.flareFadeSpeed         = FlareFadeSpeed;

		}

		[ContextMenu("Load Settings")]
		public void LoadSettings()
		{
			Skybox                 = RenderSettings.skybox;
			Sun                    = RenderSettings.sun;

			Fog                    = RenderSettings.fog;
			FogMode                = RenderSettings.fogMode;
			FogColor               = RenderSettings.fogColor;
			FogDensity             = RenderSettings.fogDensity;
			FogStartDistance       = RenderSettings.fogStartDistance;
			FogEndDistance         = RenderSettings.fogEndDistance;

			AmbientMode            = RenderSettings.ambientMode;
			AmbientLight           = RenderSettings.ambientLight;
			AmbientIntensity       = RenderSettings.ambientIntensity;
			AmbientEquatorColor    = RenderSettings.ambientEquatorColor;
			AmbientGroundColor     = RenderSettings.ambientGroundColor;
			AmbientSkyColor        = RenderSettings.ambientSkyColor;
			AmbientProbe           = RenderSettings.ambientProbe;

			SubtractiveShadowColor = RenderSettings.subtractiveShadowColor;

			ReflectionIntensity    = RenderSettings.reflectionIntensity;
			ReflectionBounces      = RenderSettings.reflectionBounces;
			DefaultReflectionMode  = RenderSettings.defaultReflectionMode;
			DefaultReflectionResolution = RenderSettings.defaultReflectionResolution;

			HaloStrength           = RenderSettings.haloStrength;
			FlareStrength          = RenderSettings.flareStrength;
			FlareFadeSpeed         = RenderSettings.flareFadeSpeed;
		}
	}
}
