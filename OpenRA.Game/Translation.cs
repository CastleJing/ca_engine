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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using OpenRA.FileSystem;

namespace OpenRA
{
	public class Translation
	{
		readonly List<ITranslator> translators;

		public Translation(string language, string[] filePath, IReadOnlyFileSystem fileSystem)
		{
			if (filePath == null || filePath.Length == 0)
				return;

			translators = new List<ITranslator>();
			var jsonFiles = filePath
				.Where(s => s.EndsWith(language + ".json5", StringComparison.Ordinal))
				.Concat(filePath.Where(s => s.EndsWith("en.json5", StringComparison.Ordinal)));
			foreach (var path in jsonFiles)
			{
				var t = new JsonTranslator();
				if (!fileSystem.TryOpen(path, out var s))
					continue;
				using (s)
				{
					if (t.TryLoad(s))
						translators.Add(t);
				}
			}

			var toFiles = filePath
				.Where(s => s.EndsWith(language + ".to", StringComparison.Ordinal))
				.Concat(filePath.Where(s => s.EndsWith("en.to", StringComparison.Ordinal)));
			foreach (var path in toFiles)
			{
				var t = new ToTranslator();
				if (!fileSystem.TryOpen(path, out var s))
					continue;
				if (t.TryLoad(s))
					translators.Add(t);
			}
		}

		public Translation(string language, string[] filePath, IReadOnlyPackage package)
		{
			if (filePath == null || filePath.Length == 0)
				return;

			translators = new List<ITranslator>();
			var jsonFiles = filePath
				.Where(s => s.EndsWith(language + ".json5", StringComparison.Ordinal))
				.Concat(filePath.Where(s => s.EndsWith("en.json5", StringComparison.Ordinal)));
			foreach (var path in jsonFiles)
			{
				var t = new JsonTranslator();
				var s = package.GetStream(path);
				if (s == null)
					continue;
				using (s)
				{
					if (t.TryLoad(s))
						translators.Add(t);
				}
			}

			var toFiles = filePath
				.Where(s => s.EndsWith(language + ".to", StringComparison.Ordinal))
				.Concat(filePath.Where(s => s.EndsWith("en.to", StringComparison.Ordinal)));
			foreach (var path in toFiles)
			{
				var t = new ToTranslator();
				var s = package.GetStream(path);
				if (s == null)
					continue;
				if (t.TryLoad(s))
					translators.Add(t);
			}
		}

		public string GetFormattedMessage(string key, IDictionary<string, object> args = null)
		{
			if (key == null)
				return "";

			if (translators == null)
				return key;

			foreach (var translator in translators)
			{
				if (!translator.Contain(key))
					continue;
				var str = translator.GetText(key);
				if (args == null)
					return str;
				foreach (var arg in args)
					str = str.Replace("{" + arg.Key + "}", arg.Value.ToString());
				return str;
			}

			return key;
		}

		public bool Contain(string s)
		{
			if (translators == null)
				return false;

			foreach (var i in translators)
			{
				if (i.Contain(s))
					return true;
			}

			return false;
		}
	}

	public interface ITranslator : IEnumerable<string>
	{
		bool TryLoad(Stream s);
		string GetText(string s);
		bool Contain(string s);
	}

	public class JsonTranslator : ITranslator
	{
		Dictionary<string, string> dict = new Dictionary<string, string>();

		public bool TryLoad(Stream s)
		{
			try
			{
				using var sr = new StreamReader(s, Encoding.UTF8, leaveOpen: true);
				dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(sr.ReadToEnd())
					?? new Dictionary<string, string>();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				return false;
			}

			return true;
		}

		public string GetText(string s)
		{
			if (!dict.ContainsKey(s))
				return s;
			return dict[s];
		}

		public bool Contain(string s)
		{
			return dict.ContainsKey(s);
		}

		public IEnumerator<string> GetEnumerator()
		{
			return dict.Keys.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	public class ToTranslator : ITranslator
	{
		Dictionary<string, (uint, uint)> indexDict = new Dictionary<string, (uint, uint)>();
		Stream fileStream;
		Dictionary<string, string> dict = new Dictionary<string, string>();

		public bool TryLoad(Stream s)
		{
			try
			{
				fileStream = s;
				while (true)
				{
					var intByteBuff = new byte[4];
					s.ReadExactly(intByteBuff, 0, 4);
					var next = BitConverter.ToUInt32(intByteBuff, 0);
					if (next == 0)
						break;
					s.ReadExactly(intByteBuff, 0, 4);
					var valueLength = BitConverter.ToUInt32(intByteBuff, 0);
					s.ReadExactly(intByteBuff, 0, 4);
					var index = BitConverter.ToUInt32(intByteBuff, 0);
					var keyByteBuff = new byte[next - s.Position];
					s.ReadExactly(keyByteBuff, 0, keyByteBuff.Length);
					var key = Encoding.UTF8.GetString(keyByteBuff);
					indexDict.Add(key, (index, valueLength));
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				return false;
			}

			return true;
		}

		public string GetText(string s)
		{
			if (dict.ContainsKey(s))
				return dict[s];
			if (fileStream == null)
				return s;
			if (!indexDict.ContainsKey(s))
				return s;
			var (index, length) = indexDict[s];
			var bytes = new byte[length];
			fileStream.Position = index;
			fileStream.ReadExactly(bytes, 0, (int)length);
			var value = Encoding.UTF8.GetString(bytes);
			dict.Add(s, value);
			return value;
		}

		public bool Contain(string s)
		{
			return indexDict.ContainsKey(s);
		}

		public IEnumerator<string> GetEnumerator()
		{
			return indexDict.Keys.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
