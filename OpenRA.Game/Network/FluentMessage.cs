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

namespace OpenRA.Network
{
	// Kept as a lightweight network envelope for translated server notifications.
	public sealed class FluentMessage
	{
		public const int ProtocolVersion = 1;

		public readonly string Key = string.Empty;
		public readonly object[] Arguments = Array.Empty<object>();

		public FluentMessage(MiniYaml yaml)
		{
			if (yaml == null)
				return;

			var nodes = yaml.ToDictionary();
			if (nodes.TryGetValue("Key", out var keyNode))
				Key = keyNode.Value;

			if (!nodes.TryGetValue("Arguments", out var argsNode))
				return;

			var args = new List<object>(argsNode.Nodes.Length * 2);
			foreach (var node in argsNode.Nodes)
			{
				var values = node.Value.ToDictionary();
				if (!values.TryGetValue("Key", out var argKeyNode) || string.IsNullOrEmpty(argKeyNode.Value))
					continue;

				values.TryGetValue("Value", out var argValueNode);
				args.Add(argKeyNode.Value);
				args.Add(argValueNode?.Value ?? "");
			}

			Arguments = args.ToArray();
		}

		public static string Serialize(string key, object[] args)
		{
			var root = new List<MiniYamlNode>
			{
				new("Protocol", ProtocolVersion.ToStringInvariant()),
				new("Key", key)
			};

			if (args != null && args.Length > 0)
			{
				var argNodes = new List<MiniYamlNode>(args.Length / 2);
				for (var i = 0; i + 1 < args.Length; i += 2)
				{
					if (args[i] is not string argKey || string.IsNullOrEmpty(argKey))
						continue;

					var argValue = args[i + 1]?.ToString() ?? "";
					argNodes.Add(new MiniYamlNode(
						$"Argument@{i / 2}",
						new MiniYaml("", new[]
						{
							new MiniYamlNode("Key", argKey),
							new MiniYamlNode("Value", argValue)
						})));
				}

				root.Add(new MiniYamlNode("Arguments", new MiniYaml("", argNodes)));
			}

			return new MiniYaml("", root).ToLines("FluentMessage").JoinWith("\n");
		}
	}
}
