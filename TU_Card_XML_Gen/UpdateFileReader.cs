using System.Text.RegularExpressions;

namespace TU_Card_XML_Gen
{
    /// <summary>
    /// One card's worth of lines from the update file, with comments already removed.
    /// </summary>
    internal class UpdateBlock
    {
        public List<string> Lines = new List<string>();

        /// <summary>1-based line number in the update file where this card starts, for error messages.</summary>
        public int StartLine;

        /// <summary>True when the gap before this card contained a "---" line, i.e. this card is summoned by the card before it.</summary>
        public bool SummonedByPrevious;

        /// <summary>Tiered scaling set by a "// SCALE:" comment above this card, or null to use config.xml's setting.</summary>
        public TieredScaling? Scaling;

        public string Name => Lines.Count > 0 ? Lines[0] : string.Empty;
    }

    /// <summary>
    /// Splits an update file into cards.
    ///
    /// Rules:
    ///  - Everything from "//" to the end of a line is a comment and is ignored.
    ///  - A line that is only a comment is dropped entirely, so it never splits a card.
    ///  - Cards are separated by one or more blank lines and/or "---" lines (three or more dashes).
    ///    Any mix of blank, comment and "---" lines between two cards counts as one gap.
    ///  - If the gap contains a "---" line, the next card is marked as summoned by the previous one.
    ///  - A "// SCALE: ..." comment sets tiered scaling for every card that starts after it
    ///    (including a card whose name line it's on), until the next SCALE comment.
    /// Works with both Windows (CRLF) and Unix (LF) line endings.
    /// </summary>
    internal static class UpdateFileReader
    {
        private static readonly Regex SummonSeparator = new Regex(@"^-{3,}$");
        private static readonly Regex ScaleDirective = new Regex(@"^\s*scale\s*:(.*)$", RegexOptions.IgnoreCase);

        public static List<UpdateBlock> Read(string path, TieredScaling configTiered)
        {
            return Parse(File.ReadAllLines(path), configTiered);
        }

        public static List<UpdateBlock> Parse(IEnumerable<string> rawLines, TieredScaling configTiered)
        {
            List<UpdateBlock> blocks = new List<UpdateBlock>();
            UpdateBlock? current = null;
            bool gapHasSummonSeparator = false;
            TieredScaling? currentScaling = null;
            int lineNumber = 0;

            foreach (string rawLine in rawLines)
            {
                lineNumber++;

                bool hadComment = rawLine.Contains("//");

                if (hadComment)
                {
                    Match directive = ScaleDirective.Match(rawLine.Substring(rawLine.IndexOf("//", StringComparison.Ordinal) + 2));
                    if (directive.Success)
                    {
                        try
                        {
                            currentScaling = TieredScaling.ParseDirective(directive.Groups[1].Value, configTiered);
                            Console.WriteLine($"Line {lineNumber}: scaling for the cards that follow is now "
                                + (currentScaling == null ? "the config.xml default" : $"tiered ({currentScaling})") + ".");
                        }
                        catch (FormatException ex)
                        {
                            // A bad SCALE line would quietly mis-scale every card after it, so stop the run instead.
                            throw new FormatException($"Line {lineNumber} of the update file: {ex.Message}", ex);
                        }
                    }
                }

                string line = StripComment(rawLine).Trim();

                if (line.Length == 0 && hadComment)
                {
                    // comment-only line: ignore it completely, inside or between cards.
                    continue;
                }

                if (line.Length == 0 || SummonSeparator.IsMatch(line))
                {
                    // end of a card (or more of an existing gap)
                    if (SummonSeparator.IsMatch(line))
                    {
                        gapHasSummonSeparator = true;
                    }

                    current = null;
                    continue;
                }

                if (current == null)
                {
                    current = new UpdateBlock
                    {
                        StartLine = lineNumber,
                        SummonedByPrevious = gapHasSummonSeparator,
                        Scaling = currentScaling
                    };
                    blocks.Add(current);
                    gapHasSummonSeparator = false;
                }

                current.Lines.Add(line);
            }

            return blocks;
        }

        public static string StripComment(string line)
        {
            int commentStart = line.IndexOf("//", StringComparison.Ordinal);
            return commentStart >= 0 ? line.Substring(0, commentStart) : line;
        }
    }
}
