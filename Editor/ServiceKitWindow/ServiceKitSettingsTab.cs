using UnityEngine.UIElements;

namespace Nonatomic.ServiceKit.Editor.ServiceKitWindow
{
	/// <summary>
	///     Represents the Settings tab in the ServiceKit Window.
	/// </summary>
	public class ServiceKitSettingsTab : VisualElement
	{
		public ServiceKitSettingsTab()
		{
			// Create the root container
			AddToClassList("settings-tab");

			// Title bar
			var titleBar = new VisualElement();
			titleBar.AddToClassList("services-title-bar");
			Add(titleBar);

			// Add header
			var headerLabel = new Label("Settings");
			headerLabel.AddToClassList("settings-header");
			titleBar.Add(headerLabel);

			// Add buttons container
			var buttonsContainer = new VisualElement();
			buttonsContainer.AddToClassList("settings-titlebar-buttons-container");
			titleBar.Add(buttonsContainer);
		}
	}
}