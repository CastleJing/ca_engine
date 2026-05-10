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
using System.Threading.Tasks;
using BeaconLib;
using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Server;
using OpenRA.Support;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class ServerListLogic : ChromeLogic
	{
		
		const string SearchStatusFailed = "Game-ServerListLogic-RrogressLabel-Failed";

		
		const string SearchStatusNoGames = "Game-ServerListLogic-RrogressLabel-NoGames";

		const string PlayersOnline = "Game-ServerListLogic-PlayerCountLabel2";

		
		const string NoServerSelected = "Game-ServerListLogic-NoServerSelected";

		
		const string MapStatusSearching = "Game-ServerListLogic-Searching";

		
		const string MapClassificationUnknown = "Game-ServerListLogic-UnknownMap";

		const string PlayersLabel = "Game-ServerListLogic-PlayerLabel-Player2";

		const string BotsLabel = "Game-ServerListLogic-PlayerLabel-Bot2";

		
		const string BotPlayer = "Game-ServerListLogic-BotPlayerDisplayName";

		const string SpectatorsLabel = "Game-ServerListLogic-PlayerLabel-Spectator2";

		
		const string Players = "Game-ServerListLogic-TeamBox-Players";

		const string TeamNumber = "Game-ServerListLogic-TeamBox-Team";

		
		const string NoTeam = "Game-ServerListLogic-TeamBox-NoTeam";

		
		const string Spectators = "Game-ServerListLogic-TeamBox-Spectators";

		const string OtherPlayers = "Game-ServerListLogic-OtherPlayers";

		
		const string Playing = "Game-ServerListLogic-Playing";

		
		const string Waiting = "Game-ServerListLogic-Waiting";

		const string InProgressOneMinute = "Game-ServerListLogic-GameStatus-Started2";

		const string InProgressMinutes = "Game-ServerListLogic-GameStatus-Started3";

		
		const string PasswordProtected = "Game-ServerListLogic-GameStatus-Waiting-PasswdReq";

		
		const string WaitingForPlayers = "Game-ServerListLogic-GameStatus-Waiting";

		
		const string ServerShuttingDown = "Game-ServerListLogic-GameStatus-ShuttingDown";

		
		const string UnknownServerState = "Game-ServerListLogic-GameStatus-Unknown";

		readonly string noServerSelected;
		readonly string mapStatusSearching;
		readonly string mapClassificationUnknown;
		readonly string playing;
		readonly string waiting;

		readonly Color incompatibleVersionColor;
		readonly Color incompatibleProtectedGameColor;
		readonly Color protectedGameColor;
		readonly Color incompatibleWaitingGameColor;
		readonly Color waitingGameColor;
		readonly Color incompatibleGameStartedColor;
		readonly Color gameStartedColor;
		readonly Color incompatibleGameColor;
		readonly ModData modData;
		readonly WebServices services;
		readonly Probe lanGameProbe;

		readonly Widget serverList;
		readonly ScrollItemWidget serverTemplate;
		readonly ScrollItemWidget headerTemplate;
		readonly Widget noticeContainer;
		readonly Widget clientContainer;
		readonly ScrollPanelWidget clientList;
		readonly ScrollItemWidget clientTemplate, clientHeader;
		readonly MapPreviewWidget mapPreview;
		readonly ButtonWidget joinButton;
		readonly int joinButtonY;

		readonly Action<GameServer> onJoin;

		GameServer currentServer;
		MapPreview currentMap;
		bool showNotices;
		int playerCount;

		enum SearchStatus { Fetching, Failed, NoGames, Hidden }

		SearchStatus searchStatus = SearchStatus.Fetching;

		bool activeQuery;
		IEnumerable<BeaconLocation> lanGameLocations;

		readonly CachedTransform<int, string> players;
		readonly CachedTransform<int, string> bots;
		readonly CachedTransform<int, string> spectators;

		readonly CachedTransform<double, string> minutes;
		readonly string passwordProtected;
		readonly string waitingForPlayers;
		readonly string serverShuttingDown;
		readonly string unknownServerState;

		public string ProgressLabelText()
		{
			switch (searchStatus)
			{
				case SearchStatus.Failed: return Game.Translate(SearchStatusFailed);
				case SearchStatus.NoGames: return Game.Translate(SearchStatusNoGames);
				default: return "";
			}
		}

		[ObjectCreator.UseCtor]
		public ServerListLogic(Widget widget, ModData modData, Action<GameServer> onJoin)
		{
			this.modData = modData;
			this.onJoin = onJoin;

			playing = Game.Translate(Playing);
			waiting = Game.Translate(Waiting);

			noServerSelected = Game.Translate(NoServerSelected);
			mapStatusSearching = Game.Translate(MapStatusSearching);
			mapClassificationUnknown = Game.Translate(MapClassificationUnknown);

			players = new CachedTransform<int, string>(i => Game.Translate(PlayersLabel, "0", i));
			bots = new CachedTransform<int, string>(i => Game.Translate(BotsLabel, "0", i));
			spectators = new CachedTransform<int, string>(i => Game.Translate(SpectatorsLabel, "0", i));

			minutes = new CachedTransform<double, string>(i =>
				Math.Abs(i - 1) < 0.001
					? Game.Translate(InProgressOneMinute, "0", i)
					: Game.Translate(InProgressMinutes, "0", i));
			passwordProtected = Game.Translate(PasswordProtected);
			waitingForPlayers = Game.Translate(WaitingForPlayers);
			serverShuttingDown = Game.Translate(ServerShuttingDown);
			unknownServerState = Game.Translate(UnknownServerState);

			services = modData.Manifest.Get<WebServices>();

			incompatibleVersionColor = ChromeMetrics.Get<Color>("IncompatibleVersionColor");
			incompatibleGameColor = ChromeMetrics.Get<Color>("IncompatibleGameColor");
			incompatibleProtectedGameColor = ChromeMetrics.Get<Color>("IncompatibleProtectedGameColor");
			protectedGameColor = ChromeMetrics.Get<Color>("ProtectedGameColor");
			waitingGameColor = ChromeMetrics.Get<Color>("WaitingGameColor");
			incompatibleWaitingGameColor = ChromeMetrics.Get<Color>("IncompatibleWaitingGameColor");
			gameStartedColor = ChromeMetrics.Get<Color>("GameStartedColor");
			incompatibleGameStartedColor = ChromeMetrics.Get<Color>("IncompatibleGameStartedColor");

			serverList = widget.Get<ScrollPanelWidget>("SERVER_LIST");
			headerTemplate = serverList.Get<ScrollItemWidget>("HEADER_TEMPLATE");
			serverTemplate = serverList.Get<ScrollItemWidget>("SERVER_TEMPLATE");

			noticeContainer = widget.GetOrNull("NOTICE_CONTAINER");
			if (noticeContainer != null)
			{
				noticeContainer.IsVisible = () => showNotices;
				noticeContainer.Get("OUTDATED_VERSION_LABEL").IsVisible = () => services.ModVersionStatus == ModVersionStatus.Outdated;
				noticeContainer.Get("UNKNOWN_VERSION_LABEL").IsVisible = () => services.ModVersionStatus == ModVersionStatus.Unknown;
				noticeContainer.Get("PLAYTEST_AVAILABLE_LABEL").IsVisible = () => services.ModVersionStatus == ModVersionStatus.PlaytestAvailable;
			}

			var noticeWatcher = widget.Get<LogicTickerWidget>("NOTICE_WATCHER");
			if (noticeWatcher != null && noticeContainer != null)
			{
				var containerHeight = noticeContainer.Bounds.Height;
				noticeWatcher.OnTick = () =>
				{
					var show = services.ModVersionStatus != ModVersionStatus.NotChecked && services.ModVersionStatus != ModVersionStatus.Latest;
					if (show != showNotices)
					{
						var dir = show ? 1 : -1;
						serverList.Bounds.Y += dir * containerHeight;
						serverList.Bounds.Height -= dir * containerHeight;
						showNotices = show;
					}
				};
			}

			joinButton = widget.GetOrNull<ButtonWidget>("JOIN_BUTTON");
			if (joinButton != null)
			{
				joinButton.IsVisible = () => currentServer != null;
				joinButton.IsDisabled = () => !currentServer.IsJoinable;
				joinButton.OnClick = () => onJoin(currentServer);
				joinButtonY = joinButton.Bounds.Y;
			}

			// Display the progress label over the server list
			// The text is only visible when the list is empty
			var progressText = widget.Get<LabelWidget>("PROGRESS_LABEL");
			progressText.IsVisible = () => searchStatus != SearchStatus.Hidden;
			progressText.GetText = ProgressLabelText;

			var gs = Game.Settings.Game;
			void ToggleFilterFlag(MPGameFilters f)
			{
				gs.MPGameFilters ^= f;
				Game.Settings.Save();
				RefreshServerList();
			}

			var filtersButton = widget.GetOrNull<DropDownButtonWidget>("FILTERS_DROPDOWNBUTTON");
			if (filtersButton != null)
			{
				// HACK: MULTIPLAYER_FILTER_PANEL doesn't follow our normal procedure for dropdown creation
				// but we still need to be able to set the dropdown width based on the parent
				// The yaml should use PARENT_WIDTH instead of DROPDOWN_WIDTH
				var filtersPanel = Ui.LoadWidget("MULTIPLAYER_FILTER_PANEL", filtersButton, new WidgetArgs());
				filtersButton.Children.Remove(filtersPanel);

				var showWaitingCheckbox = filtersPanel.GetOrNull<CheckboxWidget>("WAITING_FOR_PLAYERS");
				if (showWaitingCheckbox != null)
				{
					showWaitingCheckbox.IsChecked = () => gs.MPGameFilters.HasFlag(MPGameFilters.Waiting);
					showWaitingCheckbox.OnClick = () => ToggleFilterFlag(MPGameFilters.Waiting);
				}

				var showEmptyCheckbox = filtersPanel.GetOrNull<CheckboxWidget>("EMPTY");
				if (showEmptyCheckbox != null)
				{
					showEmptyCheckbox.IsChecked = () => gs.MPGameFilters.HasFlag(MPGameFilters.Empty);
					showEmptyCheckbox.OnClick = () => ToggleFilterFlag(MPGameFilters.Empty);
				}

				var showAlreadyStartedCheckbox = filtersPanel.GetOrNull<CheckboxWidget>("ALREADY_STARTED");
				if (showAlreadyStartedCheckbox != null)
				{
					showAlreadyStartedCheckbox.IsChecked = () => gs.MPGameFilters.HasFlag(MPGameFilters.Started);
					showAlreadyStartedCheckbox.OnClick = () => ToggleFilterFlag(MPGameFilters.Started);
				}

				var showProtectedCheckbox = filtersPanel.GetOrNull<CheckboxWidget>("PASSWORD_PROTECTED");
				if (showProtectedCheckbox != null)
				{
					showProtectedCheckbox.IsChecked = () => gs.MPGameFilters.HasFlag(MPGameFilters.Protected);
					showProtectedCheckbox.OnClick = () => ToggleFilterFlag(MPGameFilters.Protected);
				}

				var showIncompatibleCheckbox = filtersPanel.GetOrNull<CheckboxWidget>("INCOMPATIBLE_VERSION");
				if (showIncompatibleCheckbox != null)
				{
					showIncompatibleCheckbox.IsChecked = () => gs.MPGameFilters.HasFlag(MPGameFilters.Incompatible);
					showIncompatibleCheckbox.OnClick = () => ToggleFilterFlag(MPGameFilters.Incompatible);
				}

				filtersButton.IsDisabled = () => searchStatus == SearchStatus.Fetching;
				filtersButton.OnMouseDown = _ =>
				{
					filtersButton.RemovePanel();
					filtersButton.AttachPanel(filtersPanel);
				};
			}

			var reloadButton = widget.GetOrNull<ButtonWidget>("RELOAD_BUTTON");
			if (reloadButton != null)
			{
				reloadButton.IsDisabled = () => searchStatus == SearchStatus.Fetching;
				reloadButton.OnClick = RefreshServerList;

				var reloadIcon = reloadButton.GetOrNull<ImageWidget>("IMAGE_RELOAD");
				if (reloadIcon != null)
				{
					var disabledFrame = 0;
					var disabledImage = "disabled-" + disabledFrame.ToStringInvariant();
					reloadIcon.GetImageName = () => searchStatus == SearchStatus.Fetching ? disabledImage : reloadIcon.ImageName;

					var reloadTicker = reloadIcon.Get<LogicTickerWidget>("ANIMATION");
					if (reloadTicker != null)
					{
						reloadTicker.OnTick = () =>
						{
							disabledFrame = searchStatus == SearchStatus.Fetching ? (disabledFrame + 1) % 12 : 0;
							disabledImage = "disabled-" + disabledFrame.ToStringInvariant();
						};
					}
				}
			}

			var playersLabel = widget.GetOrNull<LabelWidget>("PLAYER_COUNT");
			if (playersLabel != null)
			{
				var playersText = new CachedTransform<int, string>(p => Game.Translate(PlayersOnline, "0", p));
				playersLabel.IsVisible = () => playerCount != 0;
				playersLabel.GetText = () => playersText.Update(playerCount);
			}

			mapPreview = widget.GetOrNull<MapPreviewWidget>("SELECTED_MAP_PREVIEW");
			if (mapPreview != null)
				mapPreview.Preview = () => currentMap;

			var mapTitle = widget.GetOrNull<LabelWithTooltipWidget>("SELECTED_MAP");
			if (mapTitle != null)
			{
				var font = Game.Renderer.Fonts[mapTitle.Font];
				var title = new CachedTransform<MapPreview, string>(m =>
				{
					var full = m.Translate(m.Title);
					var truncated = WidgetUtils.TruncateText(full, mapTitle.Bounds.Width, font);

					if (full != truncated)
						mapTitle.GetTooltipText = () => full;
					else
						mapTitle.GetTooltipText = null;

					return truncated;
				});

				mapTitle.GetText = () =>
				{
					if (currentMap == null)
						return noServerSelected;

					if (currentMap.Status == MapStatus.Searching)
						return mapStatusSearching;

					if (currentMap.Class == MapClassification.Unknown)
						return mapClassificationUnknown;

					return title.Update(currentMap);
				};
			}

			var ip = widget.GetOrNull<LabelWidget>("SELECTED_IP");
			if (ip != null)
			{
				ip.IsVisible = () => currentServer != null;
				ip.GetText = () => currentServer.Address;
			}

			var status = widget.GetOrNull<LabelWidget>("SELECTED_STATUS");
			if (status != null)
			{
				status.IsVisible = () => currentServer != null;
				status.GetText = () => GetStateLabel(currentServer);
				status.GetColor = () => GetStateColor(currentServer, status);
			}

			var modVersion = widget.GetOrNull<LabelWidget>("SELECTED_MOD_VERSION");
			if (modVersion != null)
			{
				modVersion.IsVisible = () => currentServer != null;
				modVersion.GetColor = () => currentServer.IsCompatible ? modVersion.TextColor : incompatibleVersionColor;

				var font = Game.Renderer.Fonts[modVersion.Font];
				var version = new CachedTransform<GameServer, string>(s => WidgetUtils.TruncateText(s.ModLabel, modVersion.Bounds.Width, font));
				modVersion.GetText = () => version.Update(currentServer);
			}

			var selectedPlayers = widget.GetOrNull<LabelWidget>("SELECTED_PLAYERS");
			if (selectedPlayers != null)
			{
				selectedPlayers.IsVisible = () => currentServer != null && (clientContainer == null || currentServer.Clients.Length == 0);
				selectedPlayers.GetText = () => PlayerLabel(currentServer);
			}

			clientContainer = widget.GetOrNull("CLIENT_LIST_CONTAINER");
			if (clientContainer != null)
			{
				clientList = Ui.LoadWidget("MULTIPLAYER_CLIENT_LIST", clientContainer, new WidgetArgs()) as ScrollPanelWidget;
				clientList.IsVisible = () => currentServer != null && currentServer.Clients.Length > 0;
				clientHeader = clientList.Get<ScrollItemWidget>("HEADER");
				clientTemplate = clientList.Get<ScrollItemWidget>("TEMPLATE");
				clientList.RemoveChildren();
			}

			lanGameLocations = new List<BeaconLocation>();
			try
			{
				lanGameProbe = new Probe("OpenRALANGame");
				lanGameProbe.BeaconsUpdated += locations => lanGameLocations = locations;
				lanGameProbe.Start();
			}
			catch (Exception ex)
			{
				Log.Write("debug", "BeaconLib.Probe: " + ex.Message);
			}

			RefreshServerList();
		}

		string PlayerLabel(GameServer game)
		{
			var label = players.Update(game.Players);

			if (game.Bots > 0)
				label += " " + bots.Update(game.Bots);

			if (game.Spectators > 0)
				label += " " + spectators.Update(game.Spectators);

			return label;
		}

		public void RefreshServerList()
		{
			// Query in progress
			if (activeQuery)
				return;

			searchStatus = SearchStatus.Fetching;

			var queryURL = new HttpQueryBuilder(services.ServerList)
			{
				{ "protocol", GameServer.ProtocolVersion },
				{ "engine", Game.EngineVersion },
				{ "mod", Game.ModData.Manifest.Id },
				{ "version", Game.ModData.Manifest.Metadata.Version }
			}.ToString();

			Task.Run(async () =>
			{
				List<GameServer> games = null;
				activeQuery = true;

				try
				{
					var client = HttpClientFactory.Create();
					var httpResponseMessage = await client.GetAsync(queryURL);
					var result = await httpResponseMessage.Content.ReadAsStreamAsync();

					var yaml = MiniYaml.FromStream(result, queryURL);
					games = new List<GameServer>();
					foreach (var node in yaml)
					{
						try
						{
							var gs = new GameServer(node.Value);
							if (gs.Address != null)
								games.Add(gs);
						}
						catch
						{
							// Ignore any invalid games advertised.
						}
					}
				}
				catch (Exception e)
				{
					searchStatus = SearchStatus.Failed;
					Log.Write("debug", $"Failed to query server list with exception: {e}");
				}

				var lanGames = new List<GameServer>();
				var stringPool = new HashSet<string>(); // Reuse common strings in YAML
				foreach (var bl in lanGameLocations)
				{
					try
					{
						if (string.IsNullOrEmpty(bl.Data))
							continue;

						var game = new MiniYamlBuilder(MiniYaml.FromString(
							bl.Data, $"BeaconLocation_{bl.Address}_{bl.LastAdvertised:s}", stringPool: stringPool)[0].Value);
						var idNode = game.NodeWithKeyOrDefault("Id");

						// Skip beacons created by this instance and replace Id by expected int value
						if (idNode != null && idNode.Value.Value != Platform.SessionGUID.ToString())
						{
							idNode.Value.Value = "-1";

							// Rewrite the server address with the correct IP
							var addressNode = game.NodeWithKeyOrDefault("Address");
							if (addressNode != null)
								addressNode.Value.Value = bl.Address.ToString().Split(':')[0] + ":" + addressNode.Value.Value.Split(':')[1];

							game.Nodes.Add(new MiniYamlNodeBuilder("Location", "Local Network"));

							lanGames.Add(new GameServer(game.Build()));
						}
					}
					catch
					{
						// Ignore any invalid LAN games advertised.
					}
				}

				var groupedLanGames = lanGames.GroupBy(gs => gs.Address).Select(g => g.Last());
				if (games != null)
					games.AddRange(groupedLanGames);
				else if (groupedLanGames.Any())
					games = groupedLanGames.ToList();

				Game.RunAfterTick(() => RefreshServerListInner(games));

				activeQuery = false;
			});
		}

		int GroupSortOrder(GameServer testEntry)
		{
			// Games that we can't join are sorted last
			if (!testEntry.IsCompatible)
				return testEntry.Mod == modData.Manifest.Id ? 1 : 0;

			// Games for the current mod+version are sorted first
			if (testEntry.Mod == modData.Manifest.Id)
				return testEntry.Version == modData.Manifest.Metadata.Version ? 4 : 3;

			// Followed by games for different mods that are joinable
			return 2;
		}

		void SelectServer(GameServer server)
		{
			currentServer = server;
			currentMap = server != null ? modData.MapCache[server.Map] : null;

			// Can only show factions if the server is running the same mod
			if (server != null && mapPreview != null)
			{
				var spawns = currentMap.SpawnPoints;
				var occupants = server.Clients
					.Where(c => (c.SpawnPoint - 1 >= 0) && (c.SpawnPoint - 1 < spawns.Length))
					.ToDictionary(c => c.SpawnPoint, c => new SpawnOccupant(c, server.Mod != modData.Manifest.Id));

				mapPreview.SpawnOccupants = () => occupants;
				mapPreview.DisabledSpawnPoints = () => server.DisabledSpawnPoints;
			}

			if (server == null || server.Clients.Length == 0)
			{
				if (joinButton != null)
					joinButton.Bounds.Y = joinButtonY;

				return;
			}

			if (joinButton != null)
				joinButton.Bounds.Y = clientContainer.Bounds.Bottom;

			if (clientList == null)
				return;

			clientList.RemoveChildren();

			var players = server.Clients
				.Where(c => !c.IsSpectator)
				.GroupBy(p => p.Team)
				.OrderBy(g => g.Key)
				.ToList();

			var teams = new Dictionary<string, IEnumerable<GameClient>>();
			var noTeams = players.Count == 1;
			foreach (var p in players)
			{
				var label = noTeams ? Game.Translate(Players) : p.Key > 0
					? Game.Translate(TeamNumber, "0", p.Key)
					: Game.Translate(NoTeam);
				teams.Add(label, p);
			}

			if (server.Clients.Any(c => c.IsSpectator))
				teams.Add(Game.Translate(Spectators), server.Clients.Where(c => c.IsSpectator));

			var factionInfo = modData.DefaultRules.Actors[SystemActors.World].TraitInfos<FactionInfo>();
			foreach (var kv in teams)
			{
				var group = kv.Key;
				if (group.Length > 0)
				{
					var header = ScrollItemWidget.Setup(clientHeader, () => false, () => { });
					header.Get<LabelWidget>("LABEL").GetText = () => group;
					clientList.AddChild(header);
				}

				foreach (var option in kv.Value)
				{
					var o = option;
					var playerName = new CachedTransform<(MapStatus, int, SpriteFont), string>(s =>
					{
						var name = o.IsBot
							? currentMap.TryGetMessage(o.Name, out var msg) ? msg : Game.Translate(BotPlayer)
							: o.Name;

						return WidgetUtils.TruncateText(name, s.Item2, s.Item3);
					});

					var item = ScrollItemWidget.Setup(clientTemplate, () => false, () => { });
					if (!o.IsSpectator && server.Mod == modData.Manifest.Id)
					{
						var label = item.Get<LabelWidget>("LABEL");
						var font = Game.Renderer.Fonts[label.Font];
						label.GetText = () => playerName.Update((currentMap.Status, label.Bounds.Width, font));
						label.GetColor = () => o.Color;

						var flag = item.Get<ImageWidget>("FLAG");
						flag.IsVisible = () => true;
						flag.GetImageCollection = () => "flags";
						flag.GetImageName = () => (factionInfo != null && factionInfo.Any(f => f.InternalName == o.Faction)) ? o.Faction : "Random";
					}
					else
					{
						var label = item.Get<LabelWidget>("NOFLAG_LABEL");
						var font = Game.Renderer.Fonts[label.Font];

						// Force spectator color to prevent spoofing by the server
						var color = o.IsSpectator ? Color.White : o.Color;
						label.GetText = () => playerName.Update((currentMap.Status, label.Bounds.Width, font));
						label.GetColor = () => color;
					}

					clientList.AddChild(item);
				}
			}
		}

		void RefreshServerListInner(List<GameServer> games)
		{
			ScrollItemWidget nextServerRow = null;
			List<Widget> rows = null;

			if (games != null)
				rows = LoadGameRows(games, out nextServerRow);

			Game.RunAfterTick(() =>
			{
				serverList.RemoveChildren();
				SelectServer(null);

				if (games == null)
				{
					searchStatus = SearchStatus.Failed;
					return;
				}

				if (rows.Count == 0)
				{
					searchStatus = SearchStatus.NoGames;
					return;
				}

				searchStatus = SearchStatus.Hidden;

				// Search for any unknown maps
				if (Game.Settings.Game.AllowDownloading)
					modData.MapCache.QueryRemoteMapDetails(services.MapRepository, games.Where(g => !Filtered(g)).Select(g => g.Map));

				foreach (var row in rows)
					serverList.AddChild(row);

				nextServerRow?.OnClick();

				playerCount = games.Sum(g => g.Players);
			});
		}

		List<Widget> LoadGameRows(List<GameServer> games, out ScrollItemWidget nextServerRow)
		{
			nextServerRow = null;
			var rows = new List<Widget>();
			var mods = games.GroupBy(g => g.ModLabel)
				.OrderByDescending(g => GroupSortOrder(g.First()))
				.ThenByDescending(g => g.Count());

			foreach (var modGames in mods)
			{
				if (modGames.All(Filtered))
					continue;

				var header = ScrollItemWidget.Setup(headerTemplate, () => false, () => { });

				var headerTitle = modGames.First().ModLabel;
				header.Get<LabelWidget>("LABEL").GetText = () => headerTitle;
				rows.Add(header);

				static int ListOrder(GameServer g)
				{
					// Servers waiting for players are always first
					if (g.State == (int)ServerState.WaitingPlayers && g.Players > 0)
						return 0;

					// Then servers with spectators
					if (g.State == (int)ServerState.WaitingPlayers && g.Spectators > 0)
						return 1;

					// Then active games
					if (g.State >= (int)ServerState.GameStarted)
						return 2;

					// Empty servers are shown at the end because a flood of empty servers
					// at the top of the game list make the community look dead
					return 3;
				}

				foreach (var modGamesByState in modGames.GroupBy(ListOrder).OrderBy(g => g.Key))
				{
					// Sort 'Playing' games by Started, others by number of players
					foreach (var game in modGamesByState.Key == 2 ? modGamesByState.OrderByDescending(g => g.Started) : modGamesByState.OrderByDescending(g => g.Players))
					{
						if (Filtered(game))
							continue;

						var canJoin = game.IsJoinable;
						var item = ScrollItemWidget.Setup(serverTemplate, () => currentServer == game, () => SelectServer(game), () => onJoin(game));
						var title = item.GetOrNull<LabelWithTooltipWidget>("TITLE");
						if (title != null)
						{
							WidgetUtils.TruncateLabelToTooltip(title, game.Name);
							title.GetColor = () => canJoin ? title.TextColor : incompatibleGameColor;
						}

						var password = item.GetOrNull<ImageWidget>("PASSWORD_PROTECTED");
						if (password != null)
						{
							password.IsVisible = () => game.Protected;
							password.GetImageName = () => canJoin ? "protected" : "protected-disabled";
						}

						var auth = item.GetOrNull<ImageWidget>("REQUIRES_AUTHENTICATION");
						if (auth != null)
						{
							auth.IsVisible = () => game.Authentication;
							auth.GetImageName = () => canJoin ? "authentication" : "authentication-disabled";

							if (game.Protected && password != null)
								auth.Bounds.X -= password.Bounds.Width + 5;
						}

						var players = item.GetOrNull<LabelWithTooltipWidget>("PLAYERS");
						if (players != null)
						{
							var label =
								$"{game.Players + game.Bots} / {game.MaxPlayers + game.Bots}"
								+ (game.Spectators > 0 ? $" + {game.Spectators}" : "");

							var color = canJoin ? players.TextColor : incompatibleGameColor;
							players.GetText = () => label;
							players.GetColor = () => color;

							if (game.Clients.Length > 0)
							{
								var preview = modData.MapCache[game.Map];
								var tooltip = new CachedTransform<MapStatus, string>(s =>
								{
									var displayClients = game.Clients.Select(c => c.IsBot
										? preview.TryGetMessage(c.Name, out var msg) ? msg : Game.Translate(BotPlayer)
										: c.Name);

									if (game.Clients.Length > 10)
										displayClients = displayClients
											.Take(9)
											.Append(Game.Translate(OtherPlayers, "0", game.Clients.Length - 9));

									return displayClients.JoinWith("\n");
								});

								players.GetTooltipText = () => tooltip.Update(preview.Status);
							}
							else
								players.GetTooltipText = null;
						}

						var state = item.GetOrNull<LabelWidget>("STATUS");
						if (state != null)
						{
							var label = game.State >= (int)ServerState.GameStarted ? playing : waiting;
							state.GetText = () => label;

							var color = GetStateColor(game, state, !canJoin);
							state.GetColor = () => color;
						}

						var location = item.GetOrNull<LabelWidget>("LOCATION");
						if (location != null)
						{
							var font = Game.Renderer.Fonts[location.Font];
							var label = WidgetUtils.TruncateText(Game.Translate(EncodeCommonLocationName(game.Location)), location.Bounds.Width, font);
							location.GetText = () => label;
							location.GetColor = () => canJoin ? location.TextColor : incompatibleGameColor;
						}

						if (currentServer != null && game.Address == currentServer.Address)
							nextServerRow = item;

						rows.Add(item);
					}
				}
			}

			return rows;
		}

		static string EncodeCommonLocationName(string locationName)
		{
			switch (locationName)
			{
				case "United States of America": return "Location-US";
				case "United States": return "Location-US";
				case "China": return "Location-CN";
				case "Australia": return "Location-AU";
				case "Japan": return "Location-JP";
				case "Thailand": return "Location-TH";
				case "India": return "Location-IN";
				case "Malaysia": return "Location-MY";
				case "Korea (Republic of)": return "Location-KR";
				case "Singapore": return "Location-SG";
				case "Hong Kong": return "Location-HK";
				case "Taiwan (Province of China)": return "Location-TW";
				case "Cambodia": return "Location-KH";
				case "Philippines": return "Location-PH";
				case "Viet Nam": return "Location-VN";
				case "Norway": return "Location-NO";
				case "Spain": return "Location-ES";
				case "France": return "Location-FR";
				case "Netherlands": return "Location-NL";
				case "Czechia": return "Location-CZ";
				case "Czech Republic": return "Location-CZ";
				case "United Kingdom of Great Britain and Northern Ireland": return "Location-GB";
				case "United Kingdom": return "Location-GB";
				case "Germany": return "Location-DE";
				case "Austria": return "Location-AT";
				case "Switzerland": return "Location-CH";
				case "Brazil": return "Location-BR";
				case "Italy": return "Location-IT";
				case "Greece": return "Location-GR";
				case "Poland": return "Location-PL";
				case "Sweden": return "Location-SE";
				case "Denmark": return "Location-DK";
				case "Portugal": return "Location-PT";
				case "Peru": return "Location-PE";
				case "Ghana": return "Location-GH";
				case "Turkey": return "Location-TR";
				case "Russian Federation": return "Location-RU";
				case "Belgium": return "Location-BE";
				case "Cameroon": return "Location-CM";
				case "Ireland": return "Location-IE";
				case "South Africa": return "Location-ZA";
				case "Finland": return "Location-FI";
				case "United Arab Emirates": return "Location-AE";
				case "Hungary": return "Location-HU";
				case "Jordan": return "Location-JO";
				case "Romania": return "Location-RO";
				case "Luxembourg": return "Location-LU";
				case "Argentina": return "Location-AR";
				case "Uganda": return "Location-UG";
				case "Armenia": return "Location-AM";
				case "Tanzania, United Republic of": return "Location-TZ";
				case "Burundi": return "Location-BI";
				case "Uruguay": return "Location-UY";
				case "Chile": return "Location-CL";
				case "Bulgaria": return "Location-BG";
				case "Ukraine": return "Location-UA";
				case "Egypt": return "Location-EG";
				case "Canada": return "Location-CA";
				case "Israel": return "Location-IL";
				case "Qatar": return "Location-QA";
				case "Moldova (Republic of)": return "Location-MD";
				case "Seychelles": return "Location-SC";
				case "Iraq": return "Location-IQ";
				case "Latvia": return "Location-LV";
				case "Lithuania": return "Location-LT";
				case "Uzbekistan": return "Location-UZ";
				case "Slovakia": return "Location-SK";
				case "Kazakhstan": return "Location-KZ";
				case "Georgia": return "Location-GE";
				case "Estonia": return "Location-EE";
				case "Croatia": return "Location-HR";
				case "Albania": return "Location-AL";
				case "Palestine, State of": return "Location-PS";
				case "Saudi Arabia": return "Location-SA";
				case "Cyprus": return "Location-CY";
				case "Malta": return "Location-MT";
				case "Costa Rica": return "Location-CR";
				case "Iran (Islamic Republic of)": return "Location-IR";
				case "Indonesia": return "Location-ID";
				case "Bahrain": return "Location-BH";
				case "Mexico": return "Location-MX";
				case "Colombia": return "Location-CO";
				case "Syrian Arab Republic": return "Location-SY";
				case "Lebanon": return "Location-LB";
				case "Azerbaijan": return "Location-AZ";
				case "Zambia": return "Location-ZM";
				case "Zimbabwe": return "Location-ZW";
				case "Oman": return "Location-OM";
				case "Serbia": return "Location-RS";
				case "Iceland": return "Location-IS";
				case "Slovenia": return "Location-SI";
				case "North Macedonia": return "Location-MK";
				case "Liechtenstein": return "Location-LI";
				case "Jersey": return "Location-JE";
				case "Bosnia and Herzegovina": return "Location-BA";
				case "Kyrgyzstan": return "Location-KG";
				case "Isle of Man": return "Location-IM";
				case "Guernsey": return "Location-GG";
				case "Gibraltar": return "Location-GI";
				case "Libya": return "Location-LY";
				case "Yemen": return "Location-YE";
				case "Belarus": return "Location-BY";
				case "Reunion": return "Location-RE";
				case "Martinique": return "Location-MQ";
				case "Kuwait": return "Location-KW";
				case "Sri Lanka": return "Location-LK";
				case "Eswatini": return "Location-SZ";
				case "Congo (Democratic Republic of the)": return "Location-CD";
				case "Guadeloupe": return "Location-GP";
				case "Bangladesh": return "Location-BD";
				case "Bhutan": return "Location-BT";
				case "Brunei Darussalam": return "Location-BN";
				case "Saint Pierre and Miquelon": return "Location-PM";
				case "Panama": return "Location-PA";
				case "Lao People's Democratic Republic": return "Location-LA";
				case "Guam": return "Location-GU";
				case "Northern Mariana Islands": return "Location-MP";
				case "Dominican Republic": return "Location-DO";
				case "Nigeria": return "Location-NG";
				case "New Zealand": return "Location-NZ";
				case "Ecuador": return "Location-EC";
				case "Venezuela (Bolivarian Republic of)": return "Location-VE";
				case "Puerto Rico": return "Location-PR";
				case "Bolivia (Plurinational State of)": return "Location-BO";
				case "Virgin Islands (U.S.)": return "Location-VI";
				case "Pakistan": return "Location-PK";
				case "Papua New Guinea": return "Location-PG";
				case "Timor-Leste": return "Location-TL";
				case "Solomon Islands": return "Location-SB";
				case "Vanuatu": return "Location-VU";
				case "Fiji": return "Location-FJ";
				case "Cook Islands": return "Location-CK";
				case "Tonga": return "Location-TO";
				case "Nepal": return "Location-NP";
				case "Kenya": return "Location-KE";
				case "Macao": return "Location-MO";
				case "Trinidad and Tobago": return "Location-TT";
				case "Lesotho": return "Location-LS";
				case "Morocco": return "Location-MA";
				case "Virgin Islands (British)": return "Location-VG";
				case "Saint Kitts and Nevis": return "Location-KN";
				case "Antigua and Barbuda": return "Location-AG";
				case "Jamaica": return "Location-JM";
				case "Saint Vincent and The Grenadines": return "Location-VC";
				case "Bahamas": return "Location-BS";
				case "Dominica": return "Location-DM";
				case "Cayman Islands": return "Location-KY";
				case "Saint Lucia": return "Location-LC";
				case "Myanmar": return "Location-MM";
				case "Grenada": return "Location-GD";
				case "Curacao": return "Location-CW";
				case "Barbados": return "Location-BB";
				case "Paraguay": return "Location-PY";
				case "Guatemala": return "Location-GT";
				case "United States Minor Outlying Islands": return "Location-UM";
				case "Turkmenistan": return "Location-TM";
				case "Tokelau": return "Location-TK";
				case "Maldives": return "Location-MV";
				case "Afghanistan": return "Location-AF";
				case "New Caledonia": return "Location-NC";
				case "Mongolia": return "Location-MN";
				case "Wallis and Futuna": return "Location-WF";
				case "San Marino": return "Location-SM";
				case "Montenegro": return "Location-ME";
				case "El Salvador": return "Location-SV";
				case "Andorra": return "Location-AD";
				case "Monaco": return "Location-MC";
				case "Greenland": return "Location-GL";
				case "Tajikistan": return "Location-TJ";
				case "Faroe Islands": return "Location-FO";
				case "Haiti": return "Location-HT";
				case "Saint Martin (French Part)": return "Location-MF";
				case "Liberia": return "Location-LR";
				case "Mauritius": return "Location-MU";
				case "Botswana": return "Location-BW";
				case "Mozambique": return "Location-MZ";
				case "Tunisia": return "Location-TN";
				case "Madagascar": return "Location-MG";
				case "Angola": return "Location-AO";
				case "Namibia": return "Location-NA";
				case "Cote D'ivoire": return "Location-CI";
				case "Sudan": return "Location-SD";
				case "Malawi": return "Location-MW";
				case "Gabon": return "Location-GA";
				case "Mali": return "Location-ML";
				case "Benin": return "Location-BJ";
				case "Cabo Verde": return "Location-CV";
				case "Rwanda": return "Location-RW";
				case "Congo": return "Location-CG";
				case "Gambia": return "Location-GM";
				case "Guinea": return "Location-GN";
				case "Burkina Faso": return "Location-BF";
				case "Somalia": return "Location-SO";
				case "Sierra Leone": return "Location-SL";
				case "Niger": return "Location-NE";
				case "Central African Republic": return "Location-CF";
				case "Togo": return "Location-TG";
				case "South Sudan": return "Location-SS";
				case "Equatorial Guinea": return "Location-GQ";
				case "Senegal": return "Location-SN";
				case "Algeria": return "Location-DZ";
				case "American Samoa": return "Location-AS";
				case "Mauritania": return "Location-MR";
				case "Djibouti": return "Location-DJ";
				case "Comoros": return "Location-KM";
				case "British Indian Ocean Territory": return "Location-IO";
				case "Chad": return "Location-TD";
				case "Mayotte": return "Location-YT";
				case "Nauru": return "Location-NR";
				case "Samoa": return "Location-WS";
				case "Micronesia (Federated States of)": return "Location-FM";
				case "French Polynesia": return "Location-PF";
				case "Honduras": return "Location-HN";
				case "Nicaragua": return "Location-NI";
				case "Bermuda": return "Location-BM";
				case "Belize": return "Location-BZ";
				case "French Guiana": return "Location-GF";
				case "Niue": return "Location-NU";
				case "Tuvalu": return "Location-TV";
				case "Palau": return "Location-PW";
				case "Marshall Islands": return "Location-MH";
				case "Kiribati": return "Location-KI";
				case "Korea (Democratic People's Republic of)": return "Location-KP";
				case "Aruba": return "Location-AW";
				case "Cuba": return "Location-CU";
				case "Suriname": return "Location-SR";
				case "Guyana": return "Location-GY";
				case "Holy See": return "Location-VA";
				case "Sao Tome and Principe": return "Location-ST";
				case "Ethiopia": return "Location-ET";
				case "Eritrea": return "Location-ER";
				case "Guinea-Bissau": return "Location-GW";
				case "Falkland Islands (Malvinas)": return "Location-FK";
				case "Saint Barthelemy": return "Location-BL";
				case "Anguilla": return "Location-AI";
				case "Turks and Caicos Islands": return "Location-TC";
				case "Sint Maarten (Dutch Part)": return "Location-SX";
				case "Aland Islands": return "Location-AX";
				case "Bouvet Island": return "Location-BV";
				case "Svalbard and Jan Mayen": return "Location-SJ";
				case "Norfolk Island": return "Location-NF";
				case "Montserrat": return "Location-MS";
				case "Bonaire, Sint Eustatius and Saba": return "Location-BQ";
				case "Pitcairn": return "Location-PN";
				case "South Georgia and The South Sandwich Islands": return "Location-GS";
				case "Mars": return "Location-MARS";
				case "Moon": return "Location-MOON";
				case "Local Network": return "Location-LOCAL";
				case "Unknown": return "Location-UNKNOWN";
				case null: return "Location-UNKNOWN";
				default: return locationName;
			}
		}

		string GetStateLabel(GameServer game)
		{
			if (game == null)
				return string.Empty;

			if (game.State == (int)ServerState.GameStarted)
			{
				var totalMinutes = Math.Ceiling(game.PlayTime / 60.0);
				return minutes.Update(totalMinutes);
			}

			if (game.State == (int)ServerState.WaitingPlayers)
				return game.Protected ? passwordProtected : waitingForPlayers;

			if (game.State == (int)ServerState.ShuttingDown)
				return serverShuttingDown;

			return unknownServerState;
		}

		Color GetStateColor(GameServer game, LabelWidget label, bool darkened = false)
		{
			if (!game.Protected && game.State == (int)ServerState.WaitingPlayers)
				return darkened ? incompatibleWaitingGameColor : waitingGameColor;

			if (game.Protected && game.State == (int)ServerState.WaitingPlayers)
				return darkened ? incompatibleProtectedGameColor : protectedGameColor;

			if (game.State == (int)ServerState.GameStarted)
				return darkened ? incompatibleGameStartedColor : gameStartedColor;

			return label.TextColor;
		}

		bool Filtered(GameServer game)
		{
			var filters = Game.Settings.Game.MPGameFilters;
			if (game.State == (int)ServerState.GameStarted && !filters.HasFlag(MPGameFilters.Started))
				return true;

			if (game.State == (int)ServerState.WaitingPlayers && !filters.HasFlag(MPGameFilters.Waiting) && game.Players + game.Spectators != 0)
				return true;

			if (game.Players + game.Spectators == 0 && !filters.HasFlag(MPGameFilters.Empty))
				return true;

			if (!game.IsCompatible && !filters.HasFlag(MPGameFilters.Incompatible))
				return true;

			if (game.Protected && !filters.HasFlag(MPGameFilters.Protected))
				return true;

			return false;
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				lanGameProbe?.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}
