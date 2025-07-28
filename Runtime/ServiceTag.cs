using System;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Represents a service tag with optional styling
	/// </summary>
	[Serializable]
	public struct ServiceTag
	{
		public string name;
		public Color? backgroundColor;
		public Color? textColor;

		public ServiceTag(string name)
		{
			this.name = name;
			this.backgroundColor = null;
			this.textColor = null;
		}

		public ServiceTag(string name, Color backgroundColor)
		{
			this.name = name;
			this.backgroundColor = backgroundColor;
			this.textColor = null; // Will auto-calculate based on brightness
		}

		public ServiceTag(string name, Color backgroundColor, Color textColor)
		{
			this.name = name;
			this.backgroundColor = backgroundColor;
			this.textColor = textColor;
		}

		/// <summary>
		/// Create a tag with automatic text color based on background brightness
		/// </summary>
		public static ServiceTag WithAutoTextColor(string name, Color backgroundColor)
		{
			// Calculate brightness using standard luminance formula
			float brightness = backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f;
			Color textColor = brightness > 0.5f ? Color.black : Color.white;
			
			return new ServiceTag(name, backgroundColor, textColor);
		}

		/// <summary>
		/// Create a tag with a semi-transparent background
		/// </summary>
		public static ServiceTag WithAlpha(string name, Color baseColor, float backgroundAlpha = 0.3f)
		{
			var backgroundColor = new Color(baseColor.r, baseColor.g, baseColor.b, backgroundAlpha);
			var textColor = new Color(
				Mathf.Min(baseColor.r * 1.5f, 1f), 
				Mathf.Min(baseColor.g * 1.5f, 1f), 
				Mathf.Min(baseColor.b * 1.5f, 1f), 
				1f);
			
			return new ServiceTag(name, backgroundColor, textColor);
		}

		/// <summary>
		/// Implicit conversion from string to ServiceTag (no styling)
		/// </summary>
		public static implicit operator ServiceTag(string tagName)
		{
			return new ServiceTag(tagName);
		}

		/// <summary>
		/// Check if this tag has custom styling
		/// </summary>
		public bool HasCustomStyle => backgroundColor.HasValue;

		public override string ToString()
		{
			return name;
		}
	}
}