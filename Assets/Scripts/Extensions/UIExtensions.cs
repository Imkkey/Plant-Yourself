using UnityEngine;

namespace Projectiles
{
	public static class UIExtensions
	{
		public static void SetVisibility(this CanvasGroup @this, bool value)
		{
			if (@this == null)
				return;

			@this.alpha = value == true ? 1f : 0f;
			@this.interactable = value;
			@this.blocksRaycasts = value;
		}

		public static void SetTextSafe(this TMPro.TextMeshProUGUI @this, string text)
		{
			if (@this == null)
				return;

			@this.text = text;
		}

		public static string GetTextSafe(this TMPro.TextMeshProUGUI @this)
		{
			if (@this == null)
				return null;

			return @this.text;
		}
	}
}
