using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TU_Card_XML_Gen.NewCards
{
    /// <summary>What the game data already holds, for id checks, name checks, summons and fusion-recipe checks.</summary>
    internal class ExistingCardIndex
    {
        public class Entry
        {
            public int BaseId;
            public int MaxLevelId;
            public string Name = string.Empty;
            public int? Set;
            public List<int> LevelIds = new List<int>();
        }

        public Dictionary<int, Entry> ById = new Dictionary<int, Entry>();
        public Dictionary<string, Entry> ByName = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, List<int>> RecipeByProduct = new Dictionary<int, List<int>>();

        public static ExistingCardIndex Build(IEnumerable<XDocument> sections, IEnumerable<XElement> fusionRecipes)
        {
            ExistingCardIndex index = new ExistingCardIndex();

            foreach (XDocument doc in sections)
            {
                foreach (XElement unit in doc.Descendants("unit"))
                {
                    if (!int.TryParse(unit.Element("id")?.Value, out int baseId))
                    {
                        continue;
                    }

                    Entry entry = new Entry
                    {
                        BaseId = baseId,
                        MaxLevelId = baseId,
                        Name = unit.Element("name")?.Value ?? string.Empty,
                        Set = int.TryParse(unit.Element("set")?.Value, out int set) ? set : null,
                    };
                    entry.LevelIds.Add(baseId);

                    List<(int Level, int? Id)> upgrades = unit.Elements("upgrade")
                        .Select(u => (Level: int.TryParse(u.Element("level")?.Value, out int l) ? l : -1, Id: int.TryParse(u.Element("card_id")?.Value, out int c) ? (int?)c : null))
                        .OrderBy(u => u.Level)
                        .ToList();

                    foreach ((int _, int? id) in upgrades.Where(u => u.Id != null))
                    {
                        entry.LevelIds.Add(id!.Value);
                    }

                    if (upgrades.Count > 0 && upgrades[^1].Id != null)
                    {
                        entry.MaxLevelId = upgrades[^1].Id!.Value;
                    }

                    foreach (int id in entry.LevelIds)
                    {
                        index.ById[id] = entry;
                    }

                    if (!string.IsNullOrEmpty(entry.Name) && !index.ByName.ContainsKey(entry.Name))
                    {
                        index.ByName[entry.Name] = entry;
                    }
                }
            }

            foreach (XElement recipe in fusionRecipes)
            {
                if (!int.TryParse(recipe.Element("card_id")?.Value, out int product))
                {
                    continue;
                }

                index.RecipeByProduct[product] = recipe.Elements("resource")
                    .Select(r => int.TryParse(r.Attribute("card_id")?.Value, out int id) ? id : -1)
                    .ToList();
            }

            return index;
        }
    }

    internal class NewCardResult
    {
        public List<XElement> Units = new List<XElement>();
        public List<XElement> Recipes = new List<XElement>();
        public List<PictureReplacement> PictureReplacements = new List<PictureReplacement>();
        public List<(NewCardSpec Spec, List<int> BaseIds)> Built = new List<(NewCardSpec, List<int>)>();
        public List<(string Name, string Reason)> Failed = new List<(string, string)>();
        public List<string> Warnings = new List<string>();
        public List<string> Notes = new List<string>();
        public Dictionary<string, int> NextIds = new Dictionary<string, int>();
    }

    /// <summary>
    /// Builds full player cards: CardBuilder.ps1's New-CardUnit / Build-CardsFromBlock / summon resolution,
    /// with the same level curves, rounding, id blocks, recipes and element order.
    /// </summary>
    internal class NewCardBuilder
    {
        // Base Fusion: roots in 1000, fused tiers in 2500.  Chain: roots in these, fused tiers in 2000.
        private const int BaseFusionRootSet = 1000;
        private const int BaseFusionTierSet = 2500;
        private static readonly int[] ChainRootSets = { 3000, 3001, 4250, 4500, 4750, 9000 };
        private const int ChainTierSet = 2000;

        private static readonly Dictionary<string, int> IdCeiling = new Dictionary<string, int> { { "Structure", 24999 } };

        private readonly ExistingCardIndex existing;
        private readonly Dictionary<string, int> nextId;
        private readonly Dictionary<string, List<int>> nameToLevelIds = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SummonPatch> pendingSummons = new List<SummonPatch>();
        private readonly NewCardResult result = new NewCardResult();

        private class SummonPatch
        {
            public XElement Node = null!;
            public string TargetName = string.Empty;
            public string CardName = string.Empty;
            public int AbsolutePos;
            public int ChainTotal;
        }

        private class UnitInfo
        {
            public XElement Unit = null!;
            public int BaseId;
            public int MaxLevelId;
        }

        public NewCardBuilder(ExistingCardIndex existing, int nextAssaultId, int nextStructureId)
        {
            this.existing = existing;
            nextId = new Dictionary<string, int> { { "Assault", nextAssaultId }, { "Structure", nextStructureId } };
        }

        /// <summary>
        /// Builds every spec.  Specs that can't be built are reported in Failed and take no ids.
        /// </summary>
        public NewCardResult Build(List<NewCardSpec> specs)
        {
            List<NewCardSpec> ok = CheckBeforeBuilding(specs);

            foreach (NewCardSpec spec in ok)
            {
                CheckFusionRecipe(spec);

                List<int> baseIds = BuildSpec(spec);
                result.Built.Add((spec, baseIds));
                result.Warnings.AddRange(spec.Warnings.Select(w => $"{spec.DisplayName}: {w}"));
                result.Notes.AddRange(spec.Notes.Select(n => $"{spec.DisplayName}: {n}"));
                result.PictureReplacements.AddRange(spec.ReplacePictures);
            }

            ResolveSummons();

            result.NextIds = new Dictionary<string, int>(nextId);
            return result;
        }

        // ---- checks that must pass before any ids are taken ----

        private List<NewCardSpec> CheckBeforeBuilding(List<NewCardSpec> specs)
        {
            Dictionary<string, NewCardSpec> batchNames = new Dictionary<string, NewCardSpec>(StringComparer.OrdinalIgnoreCase);
            List<NewCardSpec> ok = new List<NewCardSpec>();

            foreach (NewCardSpec spec in specs)
            {
                string? problem = null;

                foreach (string name in spec.Names)
                {
                    if (existing.ByName.TryGetValue(name, out ExistingCardIndex.Entry? e))
                    {
                        problem = $"a card named \"{name}\" already exists (id {e.BaseId}). If this update file was already run, re-download the XMLs in TUO before running it again; otherwise pick another name.";
                        break;
                    }

                    if (batchNames.ContainsKey(name))
                    {
                        problem = $"the name \"{name}\" is used twice in this update file.";
                        break;
                    }
                }

                if (problem != null)
                {
                    result.Failed.Add((spec.DisplayName, problem));
                    continue;
                }

                foreach (string name in spec.Names)
                {
                    batchNames[name] = spec;
                }

                ok.Add(spec);
            }

            // Summon targets must be a card in this batch or in the game data.  A card whose target failed fails too.
            bool changed = true;
            while (changed)
            {
                changed = false;
                HashSet<string> buildable = new HashSet<string>(ok.SelectMany(x => x.Names), StringComparer.OrdinalIgnoreCase);

                foreach (NewCardSpec spec in ok.ToList())
                {
                    NewCardSkill? missing = spec.Skills.FirstOrDefault(s => s.Id == "summon" && s.SummonTargetName != null
                        && !buildable.Contains(s.SummonTargetName) && !existing.ByName.ContainsKey(s.SummonTargetName));

                    if (missing != null)
                    {
                        string why = batchNames.ContainsKey(missing.SummonTargetName!)
                            ? $"it summons \"{missing.SummonTargetName}\", which couldn't be built."
                            : $"it summons \"{missing.SummonTargetName}\", which isn't a card in this update file or in the game data. Check the spelling.";
                        result.Failed.Add((spec.DisplayName, why));
                        ok.Remove(spec);
                        changed = true;
                    }
                }
            }

            return ok;
        }

        /// <summary>CardBuilder's Confirm-FusionRecipeResources - warnings only.</summary>
        private void CheckFusionRecipe(NewCardSpec spec)
        {
            if (spec.Recipe == null)
            {
                return;
            }

            List<int> baseIds = new List<int>();
            foreach (RecipeResource res in spec.Recipe)
            {
                if (!existing.ById.TryGetValue(res.Id, out ExistingCardIndex.Entry? info))
                {
                    spec.Warnings.Add($"Fusion Recipe resource {res.Id} was not found in any cards_section_*.xml. Typo, or a card that doesn't exist yet?");
                    continue;
                }

                baseIds.Add(info.BaseId);
                if (res.Id != info.MaxLevelId)
                {
                    string how = res.Id == info.BaseId ? "the BASE id" : "an upgrade level, but not the max one";
                    spec.Warnings.Add($"Fusion Recipe resource {res.Id} is {how} of \"{info.Name}\" - fusion resources should be the MAX (fully-upgraded) level. Did you mean {info.MaxLevelId}?");
                }
            }

            List<string> families = new List<string>();
            foreach (int baseId in baseIds.Distinct())
            {
                string? family = GetFusionFamily(baseId, new Dictionary<int, string?>());
                if (family == null)
                {
                    spec.Warnings.Add($"Fusion Recipe resource \"{existing.ById[baseId].Name}\" ({baseId}) doesn't resolve cleanly to either fusion family (Base Fusion: root set 1000, tiers 2500; Chain: root set 3000/3001/4250/4500/4750/9000, tiers 2000). Double-check this recipe.");
                }
                else
                {
                    families.Add(family);
                }
            }

            if (families.Distinct().Count() > 1)
            {
                spec.Warnings.Add("this Fusion Recipe mixes resources from BOTH fusion families (Base Fusion and Chain) - these should never mix.");
            }
        }

        private string? GetFusionFamily(int baseId, Dictionary<int, string?> visited)
        {
            if (visited.TryGetValue(baseId, out string? seen))
            {
                return seen;
            }

            visited[baseId] = null;

            if (!existing.ById.TryGetValue(baseId, out ExistingCardIndex.Entry? entry))
            {
                return null;
            }

            int? set = entry.Set;
            if (set == BaseFusionRootSet) { visited[baseId] = "BaseFusion"; return "BaseFusion"; }
            if (set != null && ChainRootSets.Contains(set.Value)) { visited[baseId] = "Chain"; return "Chain"; }
            if (set != BaseFusionTierSet && set != ChainTierSet) return null;
            if (!existing.RecipeByProduct.TryGetValue(baseId, out List<int>? resources)) return null;

            string? family = null;
            foreach (int resId in resources)
            {
                if (!existing.ById.TryGetValue(resId, out ExistingCardIndex.Entry? res)) return null;
                string? f = GetFusionFamily(res.BaseId, visited);
                if (f == null) return null;
                if (family != null && family != f) return null;
                family = f;
            }

            int expectedTier = family == "BaseFusion" ? BaseFusionTierSet : ChainTierSet;
            if (set != expectedTier) return null;

            visited[baseId] = family;
            return family;
        }

        // ---- building ----

        /// <returns>The base id of each card built from the spec.</returns>
        private List<int> BuildSpec(NewCardSpec spec)
        {
            List<int> baseIds = new List<int>();

            if (spec.Names.Count == 1)
            {
                int tier = spec.ExplicitFusionTier ?? (spec.Recipe != null ? 2 : -1);
                int numUp = spec.Upgrades?[0] ?? 5;
                UnitInfo unit = NewUnit(spec, spec.Names[0], tier, numUp, 0, 0);
                baseIds.Add(unit.BaseId);

                if (spec.Recipe != null)
                {
                    AddRecipe(unit.BaseId, spec.Recipe);
                }

                return baseIds;
            }

            int[] tiers = spec.Names.Count == 3 ? new[] { 0, 1, 2 } : new[] { 1, 2 };
            List<int> stageLevels = Enumerable.Range(0, spec.Names.Count).Select(j => spec.Upgrades != null ? spec.Upgrades[j] + 1 : 6).ToList();
            int chainTotal = stageLevels.Sum();
            int chainOffset = 0;
            UnitInfo? prev = null;

            for (int i = 0; i < spec.Names.Count; i++)
            {
                UnitInfo unit = NewUnit(spec, spec.Names[i], tiers[i], stageLevels[i] - 1, chainOffset, chainTotal);
                baseIds.Add(unit.BaseId);

                if (i == 0 && spec.Recipe != null)
                {
                    AddRecipe(unit.BaseId, spec.Recipe);
                }
                else if (prev != null)
                {
                    AddRecipe(unit.BaseId, new List<RecipeResource> { new RecipeResource { Id = prev.MaxLevelId, Number = 2 } });
                }

                chainOffset += stageLevels[i];
                prev = unit;
            }

            return baseIds;
        }

        /// <summary>Scale factor per level (index 1 = level 1).  tier -1 = standalone / summoned.</summary>
        public static double[] ScaleFactors(int tier, int numUpgrades)
        {
            (double b, double span) = tier switch
            {
                2 => (0.8, 0.2),
                1 => (0.6, 0.2),
                0 => (0.5, 0.1),
                _ => (0.5, 0.5),
            };

            if (numUpgrades <= 0)
            {
                return new[] { 0.0, b };
            }

            double[] result = new double[numUpgrades + 2];
            for (int level = 1; level <= numUpgrades + 1; level++)
            {
                result[level] = b + span * (double)(level - 1) / (double)numUpgrades;
            }

            return result;
        }

        /// <summary>Round up; a typed 0 stays 0.</summary>
        public static int Scale(double raw, double factor) => raw == 0 ? 0 : (int)Math.Ceiling(raw * factor);

        private static int LinearScaled(double start, double end, int level, int maxLevel)
        {
            if (maxLevel <= 1)
            {
                return (int)Math.Round(start);
            }

            double t = (double)(maxLevel - level) / (double)(maxLevel - 1);
            return (int)Math.Round(end + (start - end) * t);
        }

        private UnitInfo NewUnit(NewCardSpec spec, string name, int tier, int numUpgrades, int chainOffset, int chainTotal)
        {
            string pool = spec.CardType;
            int reserve = numUpgrades + 1;
            int baseId = nextId[pool];

            if (IdCeiling.TryGetValue(pool, out int ceiling) && baseId + reserve - 1 > ceiling)
            {
                spec.Warnings.Add($"\"{name}\" pushes {pool} ids past the current band ceiling ({ceiling}). A new band needs to be opened in the header before these ids are usable.");
            }

            nextId[pool] = baseId + reserve;

            int maxLevel = numUpgrades + 1;
            double[] scale = ScaleFactors(tier, numUpgrades);

            XElement unit = new XElement("unit",
                new XElement("id", baseId),
                new XElement("name", name));

            nameToLevelIds[name] = numUpgrades > 0 ? Enumerable.Range(0, maxLevel).Select(x => baseId + x).ToList() : new List<int> { baseId };

            if (!string.IsNullOrEmpty(spec.Picture))
            {
                unit.Add(new XElement("picture", spec.Picture));
                if (spec.Bundle != null)
                {
                    unit.Add(new XElement("asset_bundle", spec.Bundle));
                }
            }

            if (tier >= 1)
            {
                unit.Add(new XElement("fusion_level", tier));
            }

            unit.Add(new XElement("rarity", spec.Rarity));
            unit.Add(new XElement("type", spec.Faction));

            if (spec.CardType != "Structure" && spec.Attack != null)
            {
                unit.Add(new XElement("attack", Scale(spec.Attack.Value, scale[1])));
            }

            unit.Add(new XElement("health", Scale(spec.Health, scale[1])));
            unit.Add(new XElement("cost", spec.Cost));

            foreach (NewCardSkill skill in spec.Skills)
            {
                unit.Add(SkillNode(skill, scale[1], name, 1, maxLevel, chainOffset, chainTotal));
            }

            unit.Add(new XElement("set", spec.SetId));

            int maxLevelId = baseId;
            for (int level = 2; level <= maxLevel; level++)
            {
                int upId = baseId + (level - 1);
                XElement up = new XElement("upgrade",
                    new XElement("card_id", upId),
                    new XElement("level", level));

                if (spec.CardType != "Structure" && spec.Attack != null)
                {
                    up.Add(new XElement("attack", Scale(spec.Attack.Value, scale[level])));
                }

                up.Add(new XElement("health", Scale(spec.Health, scale[level])));

                foreach (NewCardSkill skill in spec.Skills)
                {
                    up.Add(SkillNode(skill, scale[level], name, level, maxLevel, chainOffset, chainTotal));
                }

                unit.Add(up);
                maxLevelId = upId;
            }

            result.Units.Add(unit);
            return new UnitInfo { Unit = unit, BaseId = baseId, MaxLevelId = maxLevelId };
        }

        /// <summary>CardBuilder's Write-SkillNode (player mode).  Attribute order matches: id all y x trigger c n s s2 card_id.</summary>
        private XElement SkillNode(NewCardSkill skill, double factor, string cardName, int level, int maxLevel, int chainOffset, int chainTotal)
        {
            XElement node = new XElement("skill", new XAttribute("id", skill.Id));

            if (skill.All) node.SetAttributeValue("all", "1");
            if (skill.Y != null) node.SetAttributeValue("y", skill.Y.Value);
            if (skill.X != null) node.SetAttributeValue("x", Scale(skill.X.Value, skill.XConstant ? 1.0 : factor));
            if (skill.Trigger != null) node.SetAttributeValue("trigger", skill.Trigger);

            // c counts DOWN: level 1 = typed c + 4, max level = typed c.
            if (skill.C != null) node.SetAttributeValue("c", LinearScaled(skill.C.Value + 4, skill.C.Value, level, maxLevel));

            // jam's n counts UP from 1 to the typed n; every other skill's n stays as typed.
            if (skill.N != null) node.SetAttributeValue("n", skill.Id == "jam" ? LinearScaled(1, skill.N.Value, level, maxLevel) : skill.N.Value);

            if (skill.S != null) node.SetAttributeValue("s", skill.S.ToLowerInvariant());
            if (skill.S2 != null) node.SetAttributeValue("s2", skill.S2.ToLowerInvariant());

            if (skill.Id == "summon" && skill.SummonTargetName != null)
            {
                node.SetAttributeValue("card_id", "PENDING");
                pendingSummons.Add(new SummonPatch
                {
                    Node = node,
                    TargetName = skill.SummonTargetName,
                    CardName = cardName,
                    AbsolutePos = chainTotal > 0 ? chainOffset + level : level,
                    ChainTotal = chainTotal > 0 ? chainTotal : maxLevel,
                });
            }

            return node;
        }

        private void AddRecipe(int cardId, List<RecipeResource> resources)
        {
            XElement recipe = new XElement("fusion_recipe", new XElement("card_id", cardId));
            foreach (RecipeResource r in resources)
            {
                recipe.Add(new XElement("resource", new XAttribute("card_id", r.Id), new XAttribute("number", r.Number)));
            }

            result.Recipes.Add(recipe);
        }

        /// <summary>
        /// Summon targets: level-for-level for a single card; for a chain, the target's levels are spread equally
        /// over the whole chain.  Targets in this batch win; otherwise the existing card's level ids are used.
        /// </summary>
        private void ResolveSummons()
        {
            foreach (SummonPatch patch in pendingSummons)
            {
                List<int>? levels = null;
                if (nameToLevelIds.TryGetValue(patch.TargetName, out List<int>? batchLevels))
                {
                    levels = batchLevels;
                }
                else if (existing.ByName.TryGetValue(patch.TargetName, out ExistingCardIndex.Entry? e))
                {
                    levels = e.LevelIds;
                }

                if (levels == null || levels.Count == 0)
                {
                    // Can't happen - checked before building - but never leave PENDING silently.
                    throw new InvalidOperationException($"Summon target \"{patch.TargetName}\" for \"{patch.CardName}\" wasn't resolved.");
                }

                int targetMax = levels.Count;
                int groupSize = (int)Math.Max(1, Math.Ceiling((double)patch.ChainTotal / targetMax));
                int targetLevel = (int)Math.Ceiling((double)patch.AbsolutePos / groupSize);
                int idx = Math.Min(targetLevel, targetMax) - 1;
                patch.Node.SetAttributeValue("card_id", levels[idx]);
            }
        }
    }
}
