using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

namespace McpNetDll.Helpers;

public static class EnglishWordIndex
{
	private static readonly Lazy<HashSet<string>> _wordSet = new(() => new HashSet<string>(LoadWords(), StringComparer.OrdinalIgnoreCase));
	private static readonly Lazy<string[]> _sortedWords = new(() =>
	{
		var words = new List<string>(LoadWords());
		words.Sort(StringComparer.OrdinalIgnoreCase);
		return words.ToArray();
	});

	private static readonly ConcurrentDictionary<string, bool> _lookupCache = new(StringComparer.OrdinalIgnoreCase);

	public static bool Contains(string word)
	{
		if (string.IsNullOrWhiteSpace(word)) return false;
		return _lookupCache.GetOrAdd(word, static w =>
		{
			if (_wordSet.Value.Contains(w)) return true;
			return Array.BinarySearch(_sortedWords.Value, w, StringComparer.OrdinalIgnoreCase) >= 0;
		});
	}

	private static IEnumerable<string> LoadWords()
	{
		var path = ResolveDictionaryPath();
		if (path is not null && File.Exists(path))
		{
			foreach (var line in File.ReadLines(path))
			{
				var w = line.Trim();
				if (w.Length > 0) yield return w;
			}
			yield break;
		}

		yield return "get";
		yield return "set";
		yield return "add";
		yield return "remove";
		yield return "create";
		yield return "update";
		yield return "delete";
		yield return "name";
		yield return "value";
		yield return "string";
	}

	private static string? ResolveDictionaryPath()
	{
		var baseDir = AppContext.BaseDirectory;
		var candidates = new List<string>
		{
			Path.Combine(baseDir, "words_alpha.txt"),
			Path.Combine(baseDir, "..", "words_alpha.txt"),
			Path.Combine(baseDir, "..", "..", "words_alpha.txt"),
			Path.Combine(baseDir, "..", "..", "..", "words_alpha.txt"),
			Path.Combine(baseDir, "..", "..", "..", "..", "words_alpha.txt"),
			Path.Combine(baseDir, "..", "..", "..", "McpNetDll", "words_alpha.txt"),
			Path.Combine(baseDir, "..", "..", "..", "..", "McpNetDll", "words_alpha.txt")
		};

		foreach (var c in candidates)
		{
			var full = Path.GetFullPath(c);
			if (File.Exists(full)) return full;
		}

		var cwdCandidate = Path.Combine(Environment.CurrentDirectory, "words_alpha.txt");
		return File.Exists(cwdCandidate) ? cwdCandidate : null;
	}
}



