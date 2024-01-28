using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TUComparatorLibrary
{
    internal class UpgradeLevel
    {
        public int level;
        public int attack;
        public int health;
        public List<Skill> skillList;

        internal UpgradeLevel(XElement cardXml)
        {
            this.level = 1;

            // yes, this is jank, but it was being uncooperative and this is just a prototype)
            this.attack = int.Parse(cardXml.Element("attack")?.Value ?? "-1");
            this.health = int.Parse(cardXml.Element("health")?.Value ?? "-1");

            // add skills
            IEnumerable<XElement> skillXmls = cardXml.XPathSelectElements("skill");

            this.skillList = new List<Skill>();

            foreach (XElement skillXml in skillXmls)
            {
                skillList.Add(new Skill(skillXml));
            }
        }

        internal UpgradeLevel(XElement upgradeLevel, UpgradeLevel baseUpgradeData)
        {
            // for each value, if it is overridden in the upgrade node, use that.  If not, use the value from the previous upgrade level.
            this.level = int.Parse(upgradeLevel.Element("level")?.Value);

            this.attack = !string.IsNullOrEmpty(upgradeLevel.Element("attack")?.Value) ? int.Parse(upgradeLevel.Element("attack")?.Value) : baseUpgradeData.attack;
            this.health = !string.IsNullOrEmpty(upgradeLevel.Element("health")?.Value) ? int.Parse(upgradeLevel.Element("health")?.Value) : baseUpgradeData.health;

            this.skillList = new List<Skill>();

            // for skills, for now, start with the existing list.  If there is no update, assume it wasn't changed.  If there is an update, use the update.
            // Then add the skills that weren't used.
            List<XElement> upgradeLevelSkills = upgradeLevel.XPathSelectElements("skill").ToList();

            foreach (Skill skill in baseUpgradeData.skillList)
            {
                XElement equivalentUpgradeSkill = upgradeLevelSkills.Where(x => x.Attribute("id")?.Value.Equals(skill.id) ?? false).FirstOrDefault();

                if(equivalentUpgradeSkill != null)
                {
                    // skill is upgraded
                    this.skillList.Add(new Skill(equivalentUpgradeSkill));
                    upgradeLevelSkills.Remove(equivalentUpgradeSkill);
                }
                else
                {
                    // skill is old and unchanged
                    this.skillList.Add(skill);
                }
            }

            // at this point, we have the upgraded versions of all skills that were on the previous level.  Add the rest
            foreach(XElement upgradeLevelSkill in upgradeLevelSkills)
            {
                // skill is new
                this.skillList.Add(new Skill(upgradeLevelSkill));
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($@"Level {this.level}");
            if(this.attack != -1)
            {
                sb.AppendLine($@"  Attack: {this.attack}");
            }
            if(this.health != -1)
            {
                sb.AppendLine($@"  Health: {this.health}");
            }
            if(this.skillList != null && this.skillList.Count > 0)
            {
                sb.AppendLine("  Skills: ");

                foreach(Skill skill in this.skillList)
                {
                    sb.AppendLine($@"    {skill.ToString()}");
                }
            }

            sb.AppendLine("--------");

            return sb.ToString();
        }
    }
}
