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

using System.Collections.Generic;

namespace OpenRA
{
	public class FontData
	{
		public readonly string Font;
		public readonly int Size;
		public readonly int Ascender;
	}

	public class Fonts : IGlobalModData
	{
		[FieldLoader.LoadUsing(nameof(LoadFonts))]
		public readonly Dictionary<string, FontData> FontList;

		static object LoadFonts(MiniYaml y)
		{
			return ParseFontDefinitions(y);
		}

		/// <summary>Parse a <c>Fonts</c> YAML section (child nodes are font slot names).</summary>
		public static Dictionary<string, FontData> ParseFontDefinitions(MiniYaml fontsSection)
		{
			var ret = new Dictionary<string, FontData>();
			if (fontsSection?.Nodes == null)
				return ret;

			foreach (var node in fontsSection.Nodes)
				ret.Add(node.Key, FieldLoader.Load<FontData>(node.Value));

			return ret;
		}

		/// <summary>Build a <see cref="Fonts"/> module for trait validation when only per-language font files exist.</summary>
		public static Fonts FromFontsSection(ObjectCreator oc, MiniYaml fontsSection)
		{
			var f = (Fonts)oc.CreateObject<IGlobalModData>("Fonts");
			FieldLoader.Load(f, fontsSection);
			return f;
		}
	}
}
