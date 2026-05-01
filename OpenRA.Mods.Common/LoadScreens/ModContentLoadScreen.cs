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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.LoadScreens
{
	public sealed class ModContentLoadScreen : SheetLoadScreen
	{
		Sprite sprite;
		Rectangle bounds;

		Sheet lastSheet;
		int lastDensity;
		Size lastResolution;

		public override void DisplayInner(Renderer r, Sheet s, int density)
		{
			if (s != lastSheet || density != lastDensity)
			{
				lastSheet = s;
				lastDensity = density;
				sprite = CreateSprite(s, density, new Rectangle(0, 0, 1024, 480));
			}

			if (r.Resolution != lastResolution)
			{
				lastResolution = r.Resolution;
				bounds = new Rectangle(0, 0, lastResolution.Width, lastResolution.Height);
			}

			WidgetUtils.FillRectWithSprite(bounds, sprite);
		}

		public override void StartGame(Arguments args)
		{
			var content = Game.ModData.Manifest.Get<ModContent>();
			var modId = args.GetValue("Content.Mod", null);
			if (modId != null && modId != content.Mod)
				throw new InvalidOperationException(
					$"`Content.Mod` ({modId}) does not match ModContent.Mod ({content.Mod}).");

			var contentTranslation = new Translation(
				Game.Settings.Player.Language,
				content.Translations,
				Game.ModData.DefaultFileSystem);

			Func<string, string> translate = key => contentTranslation.GetFormattedMessage(key);

			Ui.LoadWidget("MODCONTENT_BACKGROUND", Ui.Root, new WidgetArgs
			{
				{ "translation", translate }
			});
		}

		public override bool BeforeLoad()
		{
			return true;
		}
	}
}
