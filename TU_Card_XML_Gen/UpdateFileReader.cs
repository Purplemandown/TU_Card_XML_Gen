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
    /// Works with both Windows (CRLF) and Unix (LF) line endings.
    /// </summary>
    internal static class UpdateFileReader
    {
        private static readonly Regex SummonSeparator = new Regex(@"^-{3,}$");

        public static List<UpdateBlock> Read(string path)
        {
            return Parse(File.ReadAllLines(path));
        }

        public static List<UpdateBlock> Parse(IEnumerable<string> rawLines)
        {
            List<UpdateBlock> blocks = new List<UpdateBlock>();
            UpdateBlock? current = null;
            bool gapHasSummonSeparator = false;
            int lineNumber = 0;

            foreach (string rawLine in rawLines)
            {
                lineNumber++;

                bool hadComment = rawLine.Contains("//");
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
                        SummonedByPrevious = gapHasSummonSeparator
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
