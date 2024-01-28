using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TUComparatorLibrary
{
    internal class SingleComparison
    {
        Card oldCard;
        Card newCard;

        public enum ComparisonType
        {
            REMOVED = 0,
            ADDED = 1,
            CHANGED = 2
        }

        public SingleComparison(Card oldCard, Card newCard)
        {
            this.oldCard = oldCard;
            this.newCard = newCard;
        }

        public ComparisonType GetType()
        {
            if (oldCard != null && newCard == null)
            {
                return ComparisonType.REMOVED;
            }
            else if (oldCard == null && newCard != null)
            {
                return ComparisonType.ADDED;
            }
            else
            {
                return ComparisonType.CHANGED;
            }
        }

        public string Compare()
        {
            StringBuilder sb = new StringBuilder();

            switch (this.GetType())
            {
                case ComparisonType.ADDED:
                    sb.Append(this.newCard.ToString());

                    sb.AppendLine();
                    sb.AppendLine();

                    break;
                case ComparisonType.REMOVED:
                    sb.Append($@"Card removed: {this.oldCard.name}");

                    sb.AppendLine();
                    sb.AppendLine();

                    break;
                default:
                    // alright, time to figure out what changed.
                    StringBuilder mainCardChanges = new StringBuilder();

                    if (!this.oldCard.name.Equals(this.newCard.name))
                    {
                        mainCardChanges.AppendLine($@"Name changed to {this.newCard.name} (was {this.oldCard.name})");
                    }

                    if (!this.oldCard.rarity.Equals(this.newCard.rarity))
                    {
                        mainCardChanges.AppendLine($@"Rarity changed to {Constants.GetRarityName(this.newCard.rarity)} (was {Constants.GetRarityName(this.oldCard.rarity)})");
                    }

                    if (!this.oldCard.faction.Equals(this.newCard.faction))
                    {
                        // get faction string
                        string oldFaction = Updater.factionData.Where(x => x.Element("id")?.Value.Equals(this.oldCard.faction) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";
                        string newFaction = Updater.factionData.Where(x => x.Element("id")?.Value.Equals(this.newCard.faction) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";

                        mainCardChanges.AppendLine($@"Faction changed to {newFaction} (was {oldFaction})");
                    }

                    if (!this.oldCard.delay.Equals(this.newCard.delay))
                    {
                        mainCardChanges.AppendLine($@"Delay changed to {this.newCard.delay} (was {this.oldCard.delay})");
                    }

                    // upgrade levels.  Assume that levels aren't going to be removed for now.
                    foreach(int newLevelKey in this.newCard.upgradeLevels.Keys )
                    {
                        StringBuilder levelChanges = new StringBuilder();

                        var newLevel = this.newCard.upgradeLevels[newLevelKey];
                        UpgradeLevel oldLevel = null;
                        try
                        {
                            oldLevel = this.oldCard.upgradeLevels[newLevelKey];
                        } catch (Exception ex)
                        {
                            // do nothing
                        }

                        //if old level is null, it's a new upgrade level.  If it's not, do a compare.
                        if(oldLevel == null)
                        {
                            levelChanges.AppendLine("New upgrade level:");
                            levelChanges.AppendLine(newLevel.ToString());
                        }
                        else
                        {
                            if (!oldLevel.attack.Equals(newLevel.attack))
                            {
                                levelChanges.AppendLine($@"Attack changed to {newLevel.attack} (was {oldLevel.attack})");
                            }

                            if (!oldLevel.health.Equals(newLevel.health))
                            {
                                levelChanges.AppendLine($@"Health changed to {newLevel.health} (was {oldLevel.health})");
                            }

                            // check skills.
                            var oldSkills = oldLevel.skillList;
                            var newSkills = newLevel.skillList;

                            // same as before, loop over the new skills, try to pair them off with the old ones.  Then loop the old ones
                            foreach(var newSkill in newSkills)
                            {
                                var equivalentOldSkill = oldSkills.Where(x => x.id == newSkill.id).FirstOrDefault();

                                if(equivalentOldSkill != null)
                                {
                                    // found a match, compare one-to-one
                                    if (!newSkill.ToString().Equals(equivalentOldSkill.ToString()))
                                    {
                                        levelChanges.AppendLine($@"Skill changed: {newSkill.ToString()} (was {equivalentOldSkill.ToString()})");
                                    } // if they are equal, then there was no change.

                                    oldSkills.Remove(equivalentOldSkill);
                                }
                                else
                                {
                                    // no matching old skill - this one is new.
                                    levelChanges.AppendLine($@"New skill: {newSkill.ToString()}");
                                }
                            }

                            foreach(var oldSkill in oldSkills)
                            {
                                // these skills were removed.
                                levelChanges.AppendLine($@"Skill removed: {oldSkill.ToString()}");
                            }
                        }
                        
                        // If we found anything, add the interstitials.
                        if(levelChanges.Length > 0)
                        {
                            mainCardChanges.AppendLine($@"Upgrade level {newLevelKey} changed:");
                            mainCardChanges.AppendLine(levelChanges.ToString());
                        }
                    }

                    // if any changes were found, build the interstitials.
                    if(mainCardChanges.Length > 0)
                    {
                        sb.AppendLine($@"Card changed: {this.oldCard.name}");
                        sb.AppendLine(mainCardChanges.ToString());
                        sb.AppendLine("--------");

                        sb.AppendLine();
                        sb.AppendLine();
                    }

                    break;
            }

            return sb.ToString();
        }
    }
}
