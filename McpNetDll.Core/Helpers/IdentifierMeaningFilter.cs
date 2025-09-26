using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace McpNetDll.Helpers;

public static class IdentifierMeaningFilter
{
	private static readonly Regex SplitRegex = new(
		@"(?<!^)(?=[A-Z])|[_\-\s]+|(?<=[a-zA-Z])(?=\d)|(?<=\d)(?=[a-zA-Z])",
		RegexOptions.Compiled);

	private static readonly Regex LooksRandomRegex = new("^[A-Za-z0-9]{6,}$", RegexOptions.Compiled);

	public static bool HasMeaningfulName(string? identifier)
	{
		if (string.IsNullOrWhiteSpace(identifier)) return false;

		if (identifier.StartsWith("get_", StringComparison.Ordinal) ||
			identifier.StartsWith("set_", StringComparison.Ordinal))
		{
			return true;
		}

		var raw = identifier!.Trim('_');
		if (raw.Length <= 2) return true;

		var tokens = Tokenize(raw);
		if (tokens.Count == 0) return false;

		int englishHits = 0;
		int dictionaryChecked = 0;

		foreach (var token in tokens)
		{
			if (string.IsNullOrWhiteSpace(token)) continue;
			var t = token.Trim('_').ToLowerInvariant();
			if (t.Length == 0) continue;
			if (t.Length <= 2 && t is not ("id" or "io" or "ui" or "db")) continue;
			dictionaryChecked++;
			if (EnglishWordIndex.Contains(t)) englishHits++;
		}

		if (englishHits > 0 && englishHits * 100 >= Math.Max(1, dictionaryChecked) * 40) return true;

		if (ContainsVowel(raw) && ContainsCommonDevSubstrings(raw)) return true;

		return false;
	}

	public static List<string> Tokenize(string identifier)
	{
		var parts = SplitRegex.Split(identifier)
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.SelectMany(SplitAllCaps)
			.ToList();
		return parts;
	}

	private static IEnumerable<string> SplitAllCaps(string token)
	{
		if (token.Length >= 4 && token.All(char.IsUpper))
		{
			yield return token;
			for (int size = 2; size <= 4; size++)
			{
				for (int i = 0; i + size <= token.Length; i += size)
				{
					yield return token.Substring(i, size);
				}
			}
		}
		else
		{
			yield return token;
		}
	}

	private static bool ContainsVowel(string s)
	{
		foreach (var ch in s)
		{
			var c = char.ToLowerInvariant(ch);
			if (c is 'a' or 'e' or 'i' or 'o' or 'u' or 'y') return true;
		}
		return false;
	}

	private static bool ContainsCommonDevSubstrings(string s)
	{
		var lower = s.ToLowerInvariant();
		return lower.Contains("get") || lower.Contains("set") || lower.Contains("add") || lower.Contains("remove") || lower.Contains("init") || lower.Contains("load") || lower.Contains("save") || lower.Contains("read") || lower.Contains("write") || lower.Contains("name") || lower.Contains("value");
	}
}



