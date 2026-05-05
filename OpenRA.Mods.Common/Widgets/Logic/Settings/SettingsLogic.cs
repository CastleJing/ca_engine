#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA;
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SettingsLogic : ChromeLogic
	{
		const string SettingsSaveTitle = "Chrome-Dialog-SettingsSave-Title";
		const string SettingsSavePrompt = "Chrome-Dialog-SettingsSave-Prompt";
		const string SettingsSaveCancel = "Chrome-Dialog-SettingsSave-Cancel";
		const string RestartTitle = "Chrome-Dialog-SettingsRestart-Title";
		const string RestartPrompt = "Chrome-Dialog-SettingsRestart-Prompt";
		const string RestartAccept = "Chrome-Dialog-SettingsRestart-Confirm";
		const string RestartCancel = "Chrome-Dialog-SettingsRestart-Cancel";
		const string ResetTitle = "Chrome-Dialog-SettingsReset-Title";
		const string ResetPrompt = "Chrome-Dialog-SettingsReset-Prompt";
		const string ResetAccept = "Chrome-Dialog-SettingsReset-Confirm";
		const string ResetCancel = "Chrome-Dialog-SettingsReset-Cancel";

		readonly Dictionary<string, Func<bool>> leavePanelActions = new();
		readonly Dictionary<string, Action> resetPanelActions = new();

		readonly Widget panelContainer, tabContainer;
		readonly ButtonWidget tabTemplate;
		readonly int2 buttonStride;
		readonly List<ButtonWidget> buttons = new();
		readonly Dictionary<string, string> panels = new();
		string activePanel;

		bool needsRestart = false;

		static SettingsLogic() { }

		[ObjectCreator.UseCtor]
		public SettingsLogic(Widget widget, Action onExit, WorldRenderer worldRenderer, Dictionary<string, MiniYaml> logicArgs, ModData modData)
		{
			panelContainer = widget.Get("PANEL_CONTAINER");
			var panelTemplate = panelContainer.Get<ContainerWidget>("PANEL_TEMPLATE");
			panelContainer.RemoveChild(panelTemplate);

			tabContainer = widget.Get("SETTINGS_TAB_CONTAINER");
			tabTemplate = tabContainer.Get<ButtonWidget>("BUTTON_TEMPLATE");
			tabContainer.RemoveChild(tabTemplate);

			if (logicArgs.TryGetValue("ButtonStride", out var buttonStrideNode))
				buttonStride = FieldLoader.GetValue<int2>("ButtonStride", buttonStrideNode.Value);

			if (logicArgs.TryGetValue("Panels", out var settingsPanels))
			{
				panels = settingsPanels.ToDictionary(kv => kv.Value);

				foreach (var panel in panels)
				{
					var container = panelTemplate.Clone() as ContainerWidget;
					container.Id = panel.Key;
					panelContainer.AddChild(container);

					Game.LoadWidget(worldRenderer.World, panel.Key, container, new WidgetArgs()
					{
						{ "registerPanel", (Action<string, string, Func<Widget, Func<bool>>, Func<Widget, Action>>)RegisterSettingsPanel },
						{ "panelID", panel.Key },
						{ "label", panel.Value }
					});
				}
			}

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				needsRestart |= leavePanelActions[activePanel]();
				var current = Game.Settings;
				current.Save();

				void CloseAndExit() { Ui.CloseWindow(); onExit(); }
				if (needsRestart)
				{
					void NoRestart() => ConfirmationDialogs.ButtonPrompt(modData,
						title: SettingsSaveTitle,
						text: SettingsSavePrompt,
						onCancel: CloseAndExit,
						cancelText: SettingsSaveCancel);

					if (!Game.ExternalMods.TryGetValue(ExternalMod.MakeKey(Game.ModData.Manifest), out var external))
					{
						NoRestart();
						return;
					}

					ConfirmationDialogs.ButtonPrompt(modData,
						title: RestartTitle,
						text: RestartPrompt,
						onConfirm: () => Game.SwitchToExternalMod(external, null, NoRestart),
						confirmText: RestartAccept,
						onCancel: CloseAndExit,
						cancelText: RestartCancel);
				}
				else
					CloseAndExit();
			};

			widget.Get<ButtonWidget>("RESET_BUTTON").OnClick = () =>
			{
				void Reset()
				{
					resetPanelActions[activePanel]();
					Game.Settings.Save();
				}

				ConfirmationDialogs.ButtonPrompt(modData,
					title: ResetTitle,
					text: ResetPrompt,
					titleArguments: new object[] { "panel", Game.Translate(panels[activePanel]) },
					onConfirm: Reset,
					confirmText: ResetAccept,
					onCancel: () => { },
					cancelText: ResetCancel);
			};
		}

		public void RegisterSettingsPanel(string panelID, string label, Func<Widget, Func<bool>> init, Func<Widget, Action> reset)
		{
			var panel = panelContainer.Get(panelID);

			activePanel ??= panelID;

			panel.IsVisible = () => activePanel == panelID;

			leavePanelActions.Add(panelID, init(panel));
			resetPanelActions.Add(panelID, reset(panel));

			AddSettingsTab(panelID, label);
		}

		ButtonWidget AddSettingsTab(string id, string label)
		{
			var tab = tabTemplate.Clone() as ButtonWidget;
			var lastButton = buttons.LastOrDefault();
			if (lastButton != null)
			{
				tab.Bounds.X = lastButton.Bounds.X + buttonStride.X;
				tab.Bounds.Y = lastButton.Bounds.Y + buttonStride.Y;
			}

			tab.Id = id;
			tab.GetText = () => Game.Translate(label);
			tab.IsHighlighted = () => activePanel == id;
			tab.OnClick = () =>
			{
				needsRestart |= leavePanelActions[activePanel]();
				Game.Settings.Save();
				activePanel = id;
			};

			tabContainer.AddChild(tab);
			buttons.Add(tab);

			return tab;
		}
	}
}
