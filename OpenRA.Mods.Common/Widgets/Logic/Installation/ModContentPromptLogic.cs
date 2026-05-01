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
using System.IO;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class ModContentPromptLogic : ChromeLogic
	{
		
		const string Continue = "Game-ModContentPrompt-ContinueButton";

		
		const string Quit = "Chrome-MainMenu-Quit";

		readonly ModContent content;
		readonly Func<string, string> translation;
		bool requiredContentInstalled;

		[ObjectCreator.UseCtor]
		public ModContentPromptLogic(ModData modData, Widget widget, ModContent content, Action continueLoading, Func<string, string> translation = null)
		{
			this.content = content;
			this.translation = translation;
			CheckRequiredContentInstalled();

			var continueMessage = Game.Translate(Continue);
			var quitMessage = Game.Translate(Quit);

			var panel = widget.Get("CONTENT_PROMPT_PANEL");
			var headerLabel = panel.Get<LabelWidget>("HEADER_LABEL");
			headerLabel.IncreaseHeightToFitCurrentText();
			var headerHeight = headerLabel.Bounds.Height;

			panel.Bounds.Height += headerHeight;
			panel.Bounds.Y -= headerHeight / 2;

			var advancedButton = panel.Get<ButtonWidget>("ADVANCED_BUTTON");
			advancedButton.Bounds.Y += headerHeight;
			advancedButton.OnClick = () =>
			{
				var panelArgs = new WidgetArgs
				{
					{ "onCancel", CheckRequiredContentInstalled },
					{ "content", content },
				};

				if (translation != null)
					panelArgs.Add("translation", translation);

				Ui.OpenWindow("CONTENT_PANEL", panelArgs);
			};

			var quickButton = panel.Get<ButtonWidget>("QUICK_BUTTON");
			quickButton.IsVisible = () => !string.IsNullOrEmpty(content.QuickDownload);
			quickButton.Bounds.Y += headerHeight;
			quickButton.OnClick = () =>
			{
				var downloadYaml = MiniYaml.Load(modData.DefaultFileSystem, content.Downloads, null);
				var download = downloadYaml.FirstOrDefault(n => n.Key == content.QuickDownload);
				if (download == null)
					throw new InvalidOperationException($"Mod QuickDownload `{content.QuickDownload}` definition not found.");

				var dlArgs = new WidgetArgs
				{
					{ "download", new ModContent.ModDownload(download.Value) },
					{ "onSuccess", continueLoading }
				};

				if (translation != null)
					dlArgs.Add("translation", translation);

				Ui.OpenWindow("PACKAGE_DOWNLOAD_PANEL", dlArgs);
			};

			var quitButton = panel.Get<ButtonWidget>("QUIT_BUTTON");
			quitButton.GetText = () => requiredContentInstalled ? continueMessage : quitMessage;
			quitButton.Bounds.Y += headerHeight;
			quitButton.OnClick = () =>
			{
				if (requiredContentInstalled)
					continueLoading();
				else
					Game.Exit();
			};

			Game.RunAfterTick(Ui.ResetTooltips);
		}

		void CheckRequiredContentInstalled()
		{
			requiredContentInstalled = content.Packages
				.Where(p => p.Value.Required)
				.All(p => p.Value.TestFiles.All(f => File.Exists(Platform.ResolvePath(f))));
		}
	}
}
