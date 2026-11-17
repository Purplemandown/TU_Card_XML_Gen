using System.Xml.Linq;

namespace TU_Card_XML_Gen
{
    /// <summary>
    /// How a buff sets a skill's c (cooldown, "every c") and n (number of targets) on each level.  x is not touched here.
    ///
    /// c: if the update keeps the card's current c (compared at the top card's max level), every level keeps the c it
    ///    has now.  If the update changes c, each stage counts down from c+4 at level 1 to the new c at its max level
    ///    (CardBuilder's rule for new cards).  c=1 is the same as no c.
    ///
    /// n: if the update keeps the card's current n, every level keeps the n (or all) it has now.  If it changes n, the
    ///    "n-3" ladder is used: counting stages from the top, the top stage has n-1 on its levels and n on its last two
    ///    levels, the stage below n-2 and n-1, and so on, never below 1.  Typed "all" counts as n=5, so 5 and up become
    ///    all.  For a 3-stage card with n=4: 111122 222233 333344; with all: 222233 333344 4444,all,all.  n=1 is the
    ///    same as no n.  Switching a skill between single (no n) and all keeps that typed choice on every level.
    /// </summary>
    internal sealed class CountAndCooldown
    {
        private const int All = int.MaxValue;
        private const int AllAsNumber = 5;

        private enum Mode { AsTyped, Keep, Rule }

        private readonly List<XElement> typed;
        private readonly List<Mode> cMode = new List<Mode>();
        private readonly List<Mode> nMode = new List<Mode>();

        /// <summary>What was decided for each skill that has a c or an n, for the console.</summary>
        public List<string> Notes { get; } = new List<string>();

        /// <param name="typedSkills">The update's skills at their typed (max-level) values, in output order.</param>
        /// <param name="oldTopSkills">The card's skills now, at the top stage's max level.</param>
        public CountAndCooldown(List<XElement> typedSkills, List<XElement> oldTopSkills)
        {
            typed = typedSkills;

            foreach (XElement skill in typedSkills)
            {
                string id = skill.Attribute("id")?.Value ?? "?";
                XElement? old = Find(oldTopSkills, skill);

                // c
                int newC = C(skill);
                int? oldC = old == null ? null : C(old);
                if (newC <= 1 && (oldC ?? 1) <= 1)
                {
                    cMode.Add(Mode.AsTyped);
                }
                else if (old != null && newC == oldC)
                {
                    cMode.Add(Mode.Keep);
                    Notes.Add($"{id}: c unchanged ({Show(newC)}), each level keeps its current c.");
                }
                else
                {
                    cMode.Add(Mode.Rule);
                    Notes.Add($"{id}: c {(old == null ? "new" : Show(oldC!.Value) + " ->")} {Show(newC)}, c+4 rule (level 1 = {newC + 4}, max level = {Show(newC)}).");
                }

                // n
                int newN = N(skill);
                int? oldN = old == null ? null : N(old);
                bool numeric = (newN != All && newN > 1) || (oldN != null && oldN != All && oldN > 1);
                if (old != null && newN == oldN)
                {
                    nMode.Add(newN == 1 ? Mode.AsTyped : Mode.Keep);
                    if (newN != 1 && newN != All)
                    {
                        Notes.Add($"{id}: n unchanged ({ShowN(newN)}), each level keeps its current n.");
                    }
                }
                else if (numeric)
                {
                    nMode.Add(Mode.Rule);
                    Notes.Add($"{id}: n {(old == null ? "new" : ShowN(oldN!.Value) + " ->")} {ShowN(newN)}, n-3 rule.");
                }
                else
                {
                    nMode.Add(Mode.AsTyped);
                }
            }
        }

        /// <summary>Sets c, n and all on one level's freshly built skills.</summary>
        /// <param name="output">This level's skills, in the same order as the typed skills.</param>
        /// <param name="oldLevelSkills">The card's skills now, at this stage and level.</param>
        /// <param name="stageFromTop">0 for the top card, 1 for the stage below it, ...</param>
        public void Apply(List<XElement> output, List<XElement> oldLevelSkills, int stageFromTop, int level, int maxLevel)
        {
            for (int j = 0; j < output.Count && j < typed.Count; j++)
            {
                XElement node = output[j];
                XElement t = typed[j];
                XElement? old = Find(oldLevelSkills, t);

                // c
                int c = cMode[j] switch
                {
                    Mode.Keep => old != null ? C(old) : C(t),
                    Mode.Rule => NewCards.NewCardBuilder.LinearScaled(C(t) + 4, C(t), level, maxLevel),
                    _ => C(t),
                };
                node.SetAttributeValue("c", c > 1 ? c : null);

                // n / all
                int n = nMode[j] switch
                {
                    Mode.Keep => old != null ? N(old) : N(t),
                    Mode.Rule => Ladder(N(t), stageFromTop, level, maxLevel),
                    _ => N(t),
                };
                if (n == All)
                {
                    node.SetAttributeValue("all", "1");
                }
                else if (node.Attribute("all")?.Value == "1")
                {
                    node.SetAttributeValue("all", null);   // an all="0" the tool writes for single-target skills stays
                }

                node.SetAttributeValue("n", n != All && n > 1 ? n : null);

                PutAttributesInOrder(node);
            }
        }

        /// <summary>Each level's skills as the game sees them: an upgrade with no skills keeps the level before's.</summary>
        public static List<List<XElement>> EffectiveLevels(XElement unit)
        {
            List<List<XElement>> levels = new List<List<XElement>> { unit.Elements("skill").ToList() };
            foreach (XElement upgrade in unit.Elements("upgrade"))
            {
                List<XElement> skills = upgrade.Elements("skill").ToList();
                levels.Add(skills.Count > 0 ? skills : levels[levels.Count - 1]);
            }

            return levels;
        }

        private static int Ladder(int target, int stageFromTop, int level, int maxLevel)
        {
            bool typedAll = target == All;
            int top = typedAll ? AllAsNumber : target;
            bool lastTwo = level > maxLevel - 2;
            int value = top - stageFromTop - (lastTwo ? 0 : 1);
            if (typedAll && value >= AllAsNumber)
            {
                return All;
            }

            return Math.Max(1, value);
        }

        /// <summary>The same skill: same id and trigger, else the first with the same id.</summary>
        private static XElement? Find(List<XElement> skills, XElement skill)
        {
            string? id = skill.Attribute("id")?.Value;
            string? trigger = skill.Attribute("trigger")?.Value;
            return skills.FirstOrDefault(s => s.Attribute("id")?.Value == id && s.Attribute("trigger")?.Value == trigger)
                ?? skills.FirstOrDefault(s => s.Attribute("id")?.Value == id);
        }

        private static int C(XElement skill) => int.TryParse(skill.Attribute("c")?.Value, out int c) && c > 1 ? c : 1;

        private static int N(XElement skill)
        {
            if (skill.Attribute("all")?.Value == "1")
            {
                return All;
            }

            return int.TryParse(skill.Attribute("n")?.Value, out int n) && n > 1 ? n : 1;
        }

        private static string Show(int c) => c <= 1 ? "none" : c.ToString();

        private static string ShowN(int n) => n == All ? "all" : n <= 1 ? "none" : n.ToString();

        /// <summary>The buff tool's attribute order, so changed nodes look like the others.</summary>
        private static readonly string[] Order = { "id", "x", "y", "all", "c", "s", "s2", "n", "card_id", "trigger" };

        private static void PutAttributesInOrder(XElement node)
        {
            List<XAttribute> attributes = node.Attributes().ToList();
            node.RemoveAttributes();
            foreach (XAttribute a in attributes.OrderBy(a => Array.IndexOf(Order, a.Name.LocalName) is int i && i >= 0 ? i : Order.Length))
            {
                node.Add(a);
            }
        }
    }
}
