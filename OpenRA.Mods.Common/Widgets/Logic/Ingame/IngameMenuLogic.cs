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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Scripting;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class IngameMenuLogic : ChromeLogic
	{
		
		const string Leave = "Game-IngameMenuLogic-Leave";

		
		const string AbortMission = "Game-IngameMenuLogic-Abort";

		
		const string LeaveMissionTitle = "Game-IngameMenuLogic-LeaveDialog-Title";

		
		const string LeaveMissionPrompt = "Game-IngameMenuLogic-LeaveDialog-Text";

		
		const string LeaveMissionAccept = "Game-IngameMenuLogic-LeaveDialog-OkButton";

		
		const string LeaveMissionCancel = "Game-IngameMenuLogic-LeaveDialog-CancelButton";

		
		const string RestartButton = "Game-IngameMenuLogic-Restart";

		
		const string RestartMissionTitle = "Game-IngameMenuLogic-RestartDialog-Title";

		
		const string RestartMissionPrompt = "Game-IngameMenuLogic-RestartDialog-Text";

		
		const string RestartMissionAccept = "Game-IngameMenuLogic-RestartDialog-OkButton";

		
		const string RestartMissionCancel = "Game-IngameMenuLogic-RestartDialog-CancelButton";

		
		const string SurrenderButton = "Game-IngameMenuLogic-Surrender";

		
		const string SurrenderTitle = "Game-IngameMenuLogic-SurrenderDialog-Title";

		
		const string SurrenderPrompt = "Game-IngameMenuLogic-SurrenderDialog-Text";

		
		const string SurrenderAccept = "Game-IngameMenuLogic-SurrenderDialog-OkButton";

		
		const string SurrenderCancel = "Game-IngameMenuLogic-SurrenderDialog-CancelButton";

		
		const string LoadGameButton = "Game-IngameMenuLogic-LoadGame";

		
		const string SaveGameButton = "Game-IngameMenuLogic-SaveGame";

		
		const string MusicButton = "Game-IngameMenuLogic-Music";

		
		const string SettingsButton = "Game-IngameMenuLogic-Settings";

		
		const string ReturnToMap = "Game-IngameMenuLogic-ReturnMap";

		
		const string Resume = "Game-IngameMenuLogic-Resume";

		
		const string SaveMapButton = "Game-IngameMenuLogic-SaveMap";

		
		const string ErrorMaxPlayerTitle = "Game-IngameMenuLogic-ErrorMaxPlayer-Title";

		const string ErrorMaxPlayerPrompt = "Game-IngameMenuLogic-ErrorMaxPlayer-Text";

		
		const string ErrorMaxPlayerAccept = "Game-IngameMenuLogic-ErrorMaxPlayer-OkButton";

		
		const string ExitMapButton = "Game-IngameMenuLogic-ExitEditor";

		
		const string ExitMapEditorTitle = "Game-IngameMenuLogic-ExitEditorDialog-Title";

		
		const string ExitMapEditorPromptUnsaved = "Game-IngameMenuLogic-ExitEditorDialog-Text";

		
		const string ExitMapEditorPromptDeleted = "Game-IngameMenuLogic-ExitEditorDialog-TextDeleted";

		
		const string ExitMapEditorAnywayConfirm = "Game-IngameMenuLogic-ExitEditorDialog-ConfirmAnyway";

		
		const string ExitMapEditorConfirm = "Game-IngameMenuLogic-ExitEditorDialog-OkButton";

		
		const string PlayMapWarningTitle = "Game-IngameMenuLogic-PlayMapWarning-Title";

		
		const string PlayMapWarningPrompt = "Game-IngameMenuLogic-PlayMapWarning-Text";

		
		const string PlayMapWarningCancel = "Game-IngameMenuLogic-PlayMapWarning-CancelButton";

		
		const string ExitToMapEditorTitle = "Game-IngameMenuLogic-ExitToEditorDialog-Title";

		
		const string ExitToMapEditorPrompt = "Game-IngameMenuLogic-ExitToEditorDialog-Text";

		
		const string ExitToMapEditorConfirm = "Game-IngameMenuLogic-ExitToEditorDialog-ConfirmButton";

		
		const string ExitToMapEditorCancel = "Game-IngameMenuLogic-ExitToEditorDialog-CancelButton";

		
		const string PlayMapButton = "Game-IngameMenuLogic-PlayMap";

		
		const string BackToEditorButton = "Game-IngameMenuLogic-BackToEditor";

		readonly Widget menu;
		readonly Widget buttonContainer;
		readonly ButtonWidget buttonTemplate;
		readonly int2 buttonStride;
		readonly List<ButtonWidget> buttons = new();

		readonly ModData modData;
		readonly Action onExit;
		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly MenuPostProcessEffect mpe;
		readonly bool isSinglePlayer;
		readonly bool hasError;
		bool leaving;
		bool hideMenu;

		static bool lastGameEditor = false;

		[ObjectCreator.UseCtor]
		public IngameMenuLogic(Widget widget, ModData modData, World world, Action onExit, WorldRenderer worldRenderer,
			IngameInfoPanel initialPanel, Dictionary<string, MiniYaml> logicArgs)
		{
			this.modData = modData;
			this.world = world;
			this.worldRenderer = worldRenderer;
			this.onExit = onExit;

			var buttonHandlers = new Dictionary<string, Action>
			{
				{ "ABORT_MISSION", CreateAbortMissionButton },
				{ "BACK_TO_EDITOR", CreateBackToEditorButton },
				{ "RESTART", CreateRestartButton },
				{ "SURRENDER", CreateSurrenderButton },
				{ "LOAD_GAME", CreateLoadGameButton },
				{ "SAVE_GAME", CreateSaveGameButton },
				{ "MUSIC", CreateMusicButton },
				{ "SETTINGS", CreateSettingsButton },
				{ "RESUME", CreateResumeButton },
				{ "SAVE_MAP", CreateSaveMapButton },
				{ "PLAY_MAP", CreatePlayMapButton },
				{ "EXIT_EDITOR", CreateExitEditorButton }
			};

			isSinglePlayer = !world.LobbyInfo.GlobalSettings.Dedicated && world.LobbyInfo.NonBotClients.Count() == 1;

			menu = widget.Get("INGAME_MENU");
			mpe = world.WorldActor.TraitOrDefault<MenuPostProcessEffect>();
			mpe?.Fade(mpe.Info.MenuEffect);

			buttonContainer = menu.Get("MENU_BUTTONS");
			buttonTemplate = buttonContainer.Get<ButtonWidget>("BUTTON_TEMPLATE");
			buttonContainer.RemoveChild(buttonTemplate);
			buttonContainer.IsVisible = () => !hideMenu;

			if (logicArgs.TryGetValue("ButtonStride", out var buttonStrideNode))
				buttonStride = FieldLoader.GetValue<int2>("ButtonStride", buttonStrideNode.Value);

			var scriptContext = world.WorldActor.TraitOrDefault<LuaScript>();
			hasError = scriptContext != null && scriptContext.FatalErrorOccurred;

			if (logicArgs.TryGetValue("Buttons", out var buttonsNode))
			{
				var buttonIds = FieldLoader.GetValue<string[]>("Buttons", buttonsNode.Value);
				foreach (var button in buttonIds)
					if (buttonHandlers.TryGetValue(button, out var createHandler))
						createHandler();
			}

			// Recenter the button container
			if (buttons.Count > 0)
			{
				var expand = (buttons.Count - 1) * buttonStride;
				buttonContainer.Bounds.X -= expand.X / 2;
				buttonContainer.Bounds.Y -= expand.Y / 2;
				buttonContainer.Bounds.Width += expand.X;
				buttonContainer.Bounds.Height += expand.Y;
			}

			var panelRoot = widget.GetOrNull("PANEL_ROOT");
			if (panelRoot != null && world.Type != WorldType.Editor)
			{
				Action<bool> requestHideMenu = h => hideMenu = h;
				var gameInfoPanel = Game.LoadWidget(world, "GAME_INFO_PANEL", panelRoot, new WidgetArgs()
				{
					{ "initialPanel", initialPanel },
					{ "hideMenu", requestHideMenu },
					{ "closeMenu", CloseMenu },
				});

				gameInfoPanel.IsVisible = () => !hideMenu;
			}
		}

		public static void OnQuit(World world)
		{
			// TODO: Create a mechanism to do things like this cleaner. Also needed for scripted missions
			if (world.Type == WorldType.Regular)
			{
				var moi = world.Map.Rules.Actors[SystemActors.Player].TraitInfoOrDefault<MissionObjectivesInfo>();
				if (moi != null)
				{
					var faction = world.LocalPlayer?.Faction.InternalName;
					Game.Sound.PlayNotification(world.Map.Rules, null, "Speech", moi.LeaveNotification, faction);
					TextNotificationsManager.AddTransientLine(null, moi.LeaveTextNotification);
				}
			}

			var iop = world.WorldActor.TraitsImplementing<IObjectivesPanel>().FirstOrDefault();
			var exitDelay = iop?.ExitDelay ?? 0;
			var mpe = world.WorldActor.TraitOrDefault<MenuPostProcessEffect>();

			// HACK: Opening up skirmish menu can mess up the OrderManager.
			if (!Game.IsCurrentWorld(world))
			{
				Game.Disconnect();
				Ui.ResetAll();
				Game.LoadShellMap();
				return;
			}

			if (mpe != null)
			{
				Game.RunAfterDelay(exitDelay, () =>
				{
					if (Game.IsCurrentWorld(world))
						mpe.Fade(MenuPostProcessEffect.EffectType.Black);
				});
				exitDelay += 40 * mpe.Info.FadeLength;
			}

			lastGameEditor = false;
			Game.RunAfterDelay(exitDelay, () =>
			{
				if (!Game.IsCurrentWorld(world))
					return;

				Game.Disconnect();
				Ui.ResetAll();
				Game.LoadShellMap();
			});
		}

		void ShowMenu()
		{
			hideMenu = false;
		}

		void CloseMenu()
		{
			Ui.CloseWindow();
			mpe?.Fade(MenuPostProcessEffect.EffectType.None);
			onExit();
			Ui.ResetTooltips();
		}

		ButtonWidget AddButton(string id, string label)
		{
			var button = buttonTemplate.Clone() as ButtonWidget;
			var lastButton = buttons.LastOrDefault();
			if (lastButton != null)
			{
				button.Bounds.X = lastButton.Bounds.X + buttonStride.X;
				button.Bounds.Y = lastButton.Bounds.Y + buttonStride.Y;
			}

			button.Id = id;
			button.IsDisabled = () => leaving;
			var text = Game.Translate(label);
			button.GetText = () => text;
			buttonContainer.AddChild(button);
			buttons.Add(button);

			return button;
		}

		void CreateAbortMissionButton()
		{
			if (world.Type != WorldType.Regular)
				return;

			var button = AddButton("ABORT_MISSION", world.IsGameOver
				? Game.Translate(Leave)
				: Game.Translate(AbortMission));

			button.OnClick = () =>
			{
				hideMenu = true;

				ConfirmationDialogs.ButtonPrompt(modData,
					title: LeaveMissionTitle,
					text: LeaveMissionPrompt,
					onConfirm: () => { OnQuit(world); leaving = true; },
					confirmText: LeaveMissionAccept,
					onCancel: ShowMenu,
					cancelText: LeaveMissionCancel);
			};
		}

		void CreateRestartButton()
		{
			if (world.Type != WorldType.Regular || !isSinglePlayer)
				return;

			var iop = world.WorldActor.TraitsImplementing<IObjectivesPanel>().FirstOrDefault();
			var exitDelay = iop?.ExitDelay ?? 0;

			void OnRestart()
			{
				Ui.CloseWindow();
				if (mpe != null)
				{
					if (Game.IsCurrentWorld(world))
						mpe.Fade(MenuPostProcessEffect.EffectType.Black);
					exitDelay += 40 * mpe.Info.FadeLength;
				}

				Game.RunAfterDelay(exitDelay, Game.RestartGame);
			}

			var button = AddButton("RESTART", RestartButton);
			button.IsDisabled = () => leaving;
			button.OnClick = () =>
			{
				hideMenu = true;
				ConfirmationDialogs.ButtonPrompt(modData,
					title: RestartMissionTitle,
					text: RestartMissionPrompt,
					onConfirm: OnRestart,
					confirmText: RestartMissionAccept,
					onCancel: ShowMenu,
					cancelText: RestartMissionCancel);
			};
		}

		void CreateSurrenderButton()
		{
			if (world.Type != WorldType.Regular || isSinglePlayer || world.LocalPlayer == null)
				return;

			void OnSurrender()
			{
				world.IssueOrder(new Order("Surrender", world.LocalPlayer.PlayerActor, false));
				CloseMenu();
			}

			var button = AddButton("SURRENDER", SurrenderButton);
			button.IsDisabled = () => world.LocalPlayer.WinState != WinState.Undefined || hasError || leaving;
			button.OnClick = () =>
			{
				hideMenu = true;
				ConfirmationDialogs.ButtonPrompt(modData,
					title: SurrenderTitle,
					text: SurrenderPrompt,
					onConfirm: OnSurrender,
					confirmText: SurrenderAccept,
					onCancel: ShowMenu,
					cancelText: SurrenderCancel);
			};
		}

		void CreateLoadGameButton()
		{
			if (world.Type != WorldType.Regular || !world.LobbyInfo.GlobalSettings.GameSavesEnabled || world.IsReplay)
				return;

			var button = AddButton("LOAD_GAME", LoadGameButton);
			button.IsDisabled = () => leaving || !GameSaveBrowserLogic.IsLoadPanelEnabled(modData.Manifest);
			button.OnClick = () =>
			{
				hideMenu = true;
				Ui.OpenWindow("GAMESAVE_BROWSER_PANEL", new WidgetArgs
				{
					{ "onExit", () => hideMenu = false },
					{ "onStart", CloseMenu },
					{ "isSavePanel", false },
					{ "world", null }
				});
			};
		}

		void CreateSaveGameButton()
		{
			if (world.Type != WorldType.Regular || !world.LobbyInfo.GlobalSettings.GameSavesEnabled || world.IsReplay)
				return;

			var button = AddButton("SAVE_GAME", SaveGameButton);
			button.IsDisabled = () => hasError || leaving || !world.Players.Any(p => p.Playable && p.WinState == WinState.Undefined);
			button.OnClick = () =>
			{
				hideMenu = true;
				Ui.OpenWindow("GAMESAVE_BROWSER_PANEL", new WidgetArgs
				{
					{ "onExit", () => hideMenu = false },
					{ "onStart", () => { } },
					{ "isSavePanel", true },
					{ "world", world }
				});
			};
		}

		void CreateMusicButton()
		{
			var button = AddButton("MUSIC", MusicButton);
			button.OnClick = () =>
			{
				hideMenu = true;
				Ui.OpenWindow("MUSIC_PANEL", new WidgetArgs()
				{
					{ "onExit", () => hideMenu = false },
					{ "world", world }
				});
			};
		}

		void CreateSettingsButton()
		{
			var button = AddButton("SETTINGS", SettingsButton);
			button.OnClick = () =>
			{
				hideMenu = true;
				Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs()
				{
					{ "world", world },
					{ "worldRenderer", worldRenderer },
					{ "onExit", () => hideMenu = false },
				});
			};
		}

		void CreateResumeButton()
		{
			var button = AddButton("RESUME", world.IsGameOver ? ReturnToMap : Resume);
			button.Key = modData.Hotkeys["escape"];
			button.OnClick = CloseMenu;
		}

		void CreateSaveMapButton()
		{
			if (world.Type != WorldType.Editor)
				return;

			var button = AddButton("SAVE_MAP", SaveMapButton);
			button.OnClick = () =>
			{
				hideMenu = true;
				var editorActorLayer = world.WorldActor.Trait<EditorActorLayer>();
				var actionManager = world.WorldActor.Trait<EditorActionManager>();

				var playerDefinitions = editorActorLayer.Players.ToMiniYaml();

				var playerCount = new MapPlayers(playerDefinitions).Players.Count;
				if (playerCount > MapPlayers.MaximumPlayerCount)
				{
					ConfirmationDialogs.ButtonPrompt(modData,
						title: ErrorMaxPlayerTitle,
						text: ErrorMaxPlayerPrompt,
						textArguments: new object[] { "players", playerCount, "max", MapPlayers.MaximumPlayerCount },
						onConfirm: ShowMenu,
						confirmText: ErrorMaxPlayerAccept);

					return;
				}

				Ui.OpenWindow("SAVE_MAP_PANEL", new WidgetArgs()
				{
					{ "onSave", (Action<string>)(_ => { ShowMenu(); actionManager.Modified = false; }) },
					{ "onExit", CloseMenu },
					{ "map", world.Map },
					{ "world", world },
					{ "playerDefinitions", playerDefinitions },
					{ "actorDefinitions", editorActorLayer.Save() }
				});
			};
		}

		void CreatePlayMapButton()
		{
			if (world.Type != WorldType.Editor)
				return;

			var actionManager = world.WorldActor.Trait<EditorActionManager>();
			AddButton("PLAY_MAP", PlayMapButton)
				.OnClick = () =>
				{
					hideMenu = true;
					var uid = modData.MapCache.GetUpdatedMap(world.Map.Uid);
					var map = uid == null ? null : modData.MapCache[uid];
					if (map == null || (map.Visibility != MapVisibility.Lobby && map.Visibility != MapVisibility.MissionSelector))
					{
						ConfirmationDialogs.ButtonPrompt(modData,
							title: PlayMapWarningTitle,
							text: PlayMapWarningPrompt,
							onCancel: ShowMenu,
							cancelText: PlayMapWarningCancel);

						return;
					}

					ExitEditor(actionManager, () =>
					{
						lastGameEditor = true;

						Ui.CloseWindow();
						Ui.ResetTooltips();
						void CloseMenu()
						{
							mpe?.Fade(MenuPostProcessEffect.EffectType.None);
							onExit();
						}

						if (map.Visibility == MapVisibility.Lobby)
						{
							// HACK: Server lobby should be usable without a server.
							ConnectionLogic.Connect(Game.CreateLocalServer(uid),
								"",
								() => Game.OpenWindow("SERVER_LOBBY", new WidgetArgs
								{
									{ "onExit", CloseMenu },
									{ "onStart", () => { } },
									{ "skirmishMode", true }
								}),
								() => Game.CloseServer());
						}
						else if (map.Visibility == MapVisibility.MissionSelector)
						{
							Game.OpenWindow("MISSIONBROWSER_PANEL", new WidgetArgs
							{
								{ "onExit", CloseMenu },
								{ "onStart", () => { } },
								{ "initialMap", uid }
							});
						}
					});
				};
		}

		void CreateBackToEditorButton()
		{
			if (world.Type != WorldType.Regular || !lastGameEditor)
				return;

			AddButton("BACK_TO_EDITOR", BackToEditorButton)
				.OnClick = () =>
				{
					hideMenu = true;
					void OnConfirm()
					{
						lastGameEditor = false;
						var map = modData.MapCache.GetUpdatedMap(world.Map.Uid);
						if (map == null)
							Game.LoadShellMap();
						else
						{
							DiscordService.UpdateStatus(DiscordState.InMapEditor);
							Game.LoadEditor(map);
						}
					}

					ConfirmationDialogs.ButtonPrompt(modData,
						title: ExitToMapEditorTitle,
						text: ExitToMapEditorPrompt,
						onConfirm: OnConfirm,
						confirmText: ExitToMapEditorConfirm,
						onCancel: ShowMenu,
						cancelText: ExitToMapEditorCancel);
				};
		}

		void CreateExitEditorButton()
		{
			if (world.Type != WorldType.Editor)
				return;

			var actionManager = world.WorldActor.Trait<EditorActionManager>();
			AddButton("EXIT_EDITOR", ExitMapButton)
				.OnClick = () => ExitEditor(actionManager, () => OnQuit(world));
		}

		void ExitEditor(EditorActionManager actionManager, Action onSuccess)
		{
			var map = modData.MapCache.GetUpdatedMap(world.Map.Uid);
			var deletedOrUnavailable = map == null || modData.MapCache[map].Status != MapStatus.Available;
			if (actionManager.HasUnsavedItems() || deletedOrUnavailable)
			{
				hideMenu = true;
				ConfirmationDialogs.ButtonPrompt(modData,
					title: ExitMapEditorTitle,
					text: deletedOrUnavailable ? ExitMapEditorPromptDeleted : ExitMapEditorPromptUnsaved,
					onConfirm: () => { onSuccess(); leaving = true; },
					confirmText: deletedOrUnavailable ? ExitMapEditorAnywayConfirm : ExitMapEditorConfirm,
					onCancel: ShowMenu);
			}
			else
			{
				onSuccess();
				leaving = true;
			}
		}
	}
}
