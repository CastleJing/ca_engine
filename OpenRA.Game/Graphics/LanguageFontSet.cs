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
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	/// <summary>One language's UI fonts sharing a single glyph sheet.</summary>
	public sealed class LanguageFontSet : IDisposable
	{
		readonly SheetBuilder sheetBuilder;

		public IReadOnlyDictionary<string, SpriteFont> Fonts { get; }

		public LanguageFontSet(IPlatform platform, IReadOnlyFileSystem fs, Dictionary<string, OpenRA.FontData> fontList,
			float windowScale, int fontSheetSize)
		{
			sheetBuilder = new SheetBuilder(SheetType.BGRA, fontSheetSize);
			Fonts = fontList.ToDictionary(
				x => x.Key,
				x => new SpriteFont(platform, x.Value.Font, fs.Open(x.Value.Font).ReadAllBytes(),
					x.Value.Size, x.Value.Ascender, windowScale, sheetBuilder),
				StringComparer.OrdinalIgnoreCase);
		}

		public void SetScale(float scale)
		{
			foreach (var f in Fonts.Values)
				f.SetScale(scale);
		}

		public void Dispose()
		{
			foreach (var f in Fonts.Values)
				f.Dispose();
			sheetBuilder.Dispose();
		}
	}
}
