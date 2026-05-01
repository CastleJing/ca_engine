#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software.
 */
#endregion

using System;

namespace OpenRA
{
	/// <summary>Hooks <see cref="Primitives.CachedTransform{T,U}"/> into UI language reloads.</summary>
	public static class TranslationCache
	{
		public static Func<int> UiGeneration;
	}
}
