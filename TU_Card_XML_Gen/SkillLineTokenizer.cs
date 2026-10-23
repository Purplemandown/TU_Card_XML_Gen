using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using TUComparatorLibrary;

namespace TU_Card_XML_Gen
{
    /// <summary>
    /// A skill line broken into normalised tokens, plus its trigger (if any).
    /// Tokens are: the skill id first (lower case, e.g. "armored"), then numbers,
    /// "all", "every", "to", faction names (e.g. "Righteous") and skill ids for
    /// Enhance/Evolve. For Summon, the second token is the summoned card's name as typed.
    /// </summary>
    internal class SkillLine
    {
        public string[] Tokens = Array.Empty<string>();
        public string? Trigger;

        /// <summary>The line as typed, for error messages.</summary>
        public string Original = string.Empty;

        public string SkillId => Tokens.Length > 0 ? Tokens[0] : string.Empty;
    }

    /// <summary>
    /// Turns a typed skill line ("RALLY all rt 100 On Attacked") into tokens the updater
    /// understands (["rally", "all", "Righteous", "100"] with trigger "attacked").
    /// Everything is case-insensitive. Matching is done per word, so card names in a
    /// Summon line are never mangled by skill-name replacement.
    /// </summary>
    internal static class SkillLineTokenizer
    {
        public static readonly string[] Factions = { "Imperial", "Raider", "Bloodthirsty", "Xeno", "Righteous", "Progenitor" };

        private static readonly Dictionary<string, string> FactionAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "imp", "Imperial" },
            { "rd", "Raider" },
            { "bt", "Bloodthirsty" },
            { "xn", "Xeno" },
            { "rt", "Righteous" },
            { "prog", "Progenitor" },
        };

        private static readonly Dictionary<string, string> SkillAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "zerk", "berserk" },
        };

        private static readonly Dictionary<string, string> TriggerWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "play", "play" },
            { "onplay", "play" },
            { "attacked", "attacked" },
            { "onattacked", "attacked" },
            { "death", "death" },
            { "ondeath", "death" },
        };

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "all", "every", "to" };

        private static Dictionary<string, string>? skillLookup;
        private static int longestSkillName = 1;

        /// <summary>
        /// Tokenises a skill line.
        /// </summary>
        /// <param name="line">The skill line, comments already removed.</param>
        /// <param name="isCardName">Used for Summon lines, so a card whose name ends in a trigger word
        /// (e.g. "... of Death") isn't mistaken for a trigger. May be null.</param>
        public static SkillLine Tokenize(string line, Func<string, bool>? isCardName = null)
        {
            List<string> words = Regex.Split(line.Trim(), @"\s+").Where(w => w.Length > 0).ToList();

            if (words.Count == 0)
            {
                throw new FormatException("Empty skill line.");
            }

            // A trigger may be written first ("On Play: Jam all") or last ("Jam all play").
            List<string> wordsBeforeTrigger = new List<string>(words);
            string? trigger = TakeLeadingTrigger(words) ?? TakeTrailingTrigger(words);

            // Skill name (may be more than one word).
            int skillWordCount = MatchSkill(words, 0, out string? skillId);
            if (skillId == null)
            {
                throw new FormatException($"Unknown skill \"{words[0]}\" in line \"{line}\".");
            }

            List<string> tokens = new List<string> { skillId };

            if (skillId == "summon")
            {
                string cardName = string.Join(" ", words.Skip(skillWordCount));

                // "Summon Angel of Death": if stripping a trailing trigger broke a real card name, put it back.
                if (trigger != null && isCardName != null && !isCardName(cardName))
                {
                    string unstripped = string.Join(" ", wordsBeforeTrigger.Skip(skillWordCount));
                    if (isCardName(unstripped))
                    {
                        cardName = unstripped;
                        trigger = null;
                    }
                }

                if (cardName.Length > 0)
                {
                    tokens.Add(cardName);
                }

                return new SkillLine { Tokens = tokens.ToArray(), Trigger = trigger, Original = line.Trim() };
            }

            for (int i = skillWordCount; i < words.Count;)
            {
                string word = words[i].TrimEnd(':', ',');

                if (int.TryParse(word, out _))
                {
                    tokens.Add(word);
                    i++;
                    continue;
                }

                if (word.Equals("into", StringComparison.OrdinalIgnoreCase))
                {
                    // "Evolve Armor into Counter" - same as "to"
                    tokens.Add("to");
                    i++;
                    continue;
                }

                if (Keywords.Contains(word))
                {
                    tokens.Add(word.ToLowerInvariant());
                    i++;
                    continue;
                }

                string? faction = ResolveFaction(word);
                if (faction != null)
                {
                    tokens.Add(faction);
                    i++;
                    continue;
                }

                int consumed = MatchSkill(words, i, out string? subSkillId);
                if (subSkillId != null)
                {
                    tokens.Add(subSkillId);
                    i += consumed;
                    continue;
                }

                throw new FormatException($"Don't understand \"{words[i]}\" in line \"{line}\".");
            }

            return new SkillLine { Tokens = tokens.ToArray(), Trigger = trigger, Original = line.Trim() };
        }

        /// <summary>Returns the canonical faction name for a name or abbreviation (any case), or null.</summary>
        public static string? ResolveFaction(string word)
        {
            string? faction = Factions.FirstOrDefault(f => f.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (faction != null)
            {
                return faction;
            }

            return FactionAbbreviations.TryGetValue(word, out string? full) ? full : null;
        }

        public static bool IsFaction(string token) => Factions.Contains(token, StringComparer.OrdinalIgnoreCase);

        /// <summary>Faction id from skills_set.xml for a faction name (any case).</summary>
        public static int GetFactionId(string factionName)
        {
            XElement? faction = Updater.factionData.FirstOrDefault(x => x.XPathSelectElement("name")?.Value.Equals(factionName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (faction == null)
            {
                throw new FormatException($"Faction \"{factionName}\" isn't in skills_set.xml.");
            }

            return int.Parse(faction.XPathSelectElement("id")!.Value);
        }

        public static bool IsSkillId(string token)
        {
            return GetSkillLookup().ContainsValue(token);
        }

        private static string? TakeLeadingTrigger(List<string> words)
        {
            // "On Play:" / "OnPlay" / "Play:" at the start, colon optional.
            // No skill is called on/play/attacked/death, so this can't eat a skill name.
            string first = words[0].TrimEnd(':');

            if (first.Equals("on", StringComparison.OrdinalIgnoreCase) && words.Count > 2
                && TriggerWords.TryGetValue("on" + words[1].TrimEnd(':'), out string? spaced))
            {
                words.RemoveRange(0, 2);
                return spaced;
            }

            if (words.Count > 1 && TriggerWords.TryGetValue(first, out string? joined))
            {
                words.RemoveAt(0);
                return joined;
            }

            return null;
        }

        private static string? TakeTrailingTrigger(List<string> words)
        {
            if (words.Count < 2)
            {
                return null;
            }

            string last = words[^1].TrimEnd(':');

            // "... On Play"
            if (words.Count >= 3 && words[^2].Equals("on", StringComparison.OrdinalIgnoreCase)
                && TriggerWords.TryGetValue("on" + last, out string? spaced))
            {
                words.RemoveRange(words.Count - 2, 2);
                return spaced;
            }

            // "... play" / "... OnPlay"
            if (TriggerWords.TryGetValue(last, out string? trigger))
            {
                words.RemoveAt(words.Count - 1);
                return trigger;
            }

            return null;
        }

        /// <summary>
        /// Tries to match a skill name or id starting at words[start], longest name first.
        /// Returns the number of words used (0 if no match).
        /// </summary>
        private static int MatchSkill(List<string> words, int start, out string? skillId)
        {
            Dictionary<string, string> lookup = GetSkillLookup();

            for (int length = Math.Min(longestSkillName, words.Count - start); length >= 1; length--)
            {
                string candidate = string.Join(" ", words.Skip(start).Take(length)).TrimEnd(':', ',');

                if (SkillAliases.TryGetValue(candidate, out string? aliasTarget))
                {
                    candidate = aliasTarget;
                }

                if (lookup.TryGetValue(candidate, out string? id))
                {
                    skillId = id;
                    return length;
                }
            }

            skillId = null;
            return 0;
        }

        /// <summary>Skill display names and ids from skills_set.xml, both mapping to the lower-case id.</summary>
        private static Dictionary<string, string> GetSkillLookup()
        {
            if (skillLookup != null)
            {
                return skillLookup;
            }

            skillLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // ids first, then names, so a display name wins if it happens to equal another skill's id.
            foreach (XElement skill in Updater.skillData)
            {
                string? id = skill.XPathSelectElement("id")?.Value.Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    skillLookup[id] = id.ToLowerInvariant();
                }
            }

            foreach (XElement skill in Updater.skillData)
            {
                string? id = skill.XPathSelectElement("id")?.Value.Trim();
                string? name = skill.XPathSelectElement("name")?.Value.Trim();

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || name.Equals("Attack", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                skillLookup[name] = id.ToLowerInvariant();
                longestSkillName = Math.Max(longestSkillName, name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            }

            return skillLookup;
        }
    }
}
