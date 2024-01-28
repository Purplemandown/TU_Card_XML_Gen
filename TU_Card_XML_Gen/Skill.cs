using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TUComparatorLibrary
{
    internal class Skill
    {
        public string id;
        public bool all;
        public int value;
        public int number;
        public int cooldown;
        public int faction;
        public string subSkill;
        public string subSkill2;
        public string subCard;
        public string trigger;

        internal Skill(XElement skillNode)
        {
            this.id = skillNode.Attribute("id").Value;

            int tempBool = int.Parse(skillNode.Attribute("all")?.Value ?? "0");
            this.all = tempBool == 1 ? true : false;

            this.value = int.Parse(skillNode.Attribute("x")?.Value ?? "1");
            this.number = int.Parse(skillNode.Attribute("n")?.Value ?? "1");
            this.cooldown = int.Parse(skillNode.Attribute("c")?.Value ?? "1");
            this.faction = int.Parse(skillNode.Attribute("y")?.Value ?? "-1");
            this.subSkill = skillNode.Attribute("s")?.Value ?? string.Empty;
            this.subSkill2 = skillNode.Attribute("s2")?.Value ?? string.Empty;
            this.subCard = skillNode.Attribute("card_id")?.Value ?? string.Empty;
            this.trigger = skillNode.Attribute("trigger")?.Value ?? string.Empty;
        }

        public override string ToString()
        {
            string skillName = Updater.skillData.Where(x => x.Element("id")?.Value.Equals(this.id) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";
            string allString = all ? " All" : "";
            string faction = Updater.factionData.Where(x => x.Element("id")?.Value.Equals(this.faction.ToString()) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";

            string subSkillName = "";
            if (!String.IsNullOrEmpty(this.subSkill))
            {
                subSkillName = Updater.skillData.Where(x => x.Element("id")?.Value.Equals(this.subSkill) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";
            }

            string subSkill2Name = "";
            if (!String.IsNullOrEmpty(this.subSkill2))
            {
                subSkill2Name = Updater.skillData.Where(x => x.Element("id")?.Value.Equals(this.subSkill2) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";
            }

            StringBuilder stringBuilder = new StringBuilder();

            if (!String.IsNullOrEmpty(this.trigger))
            {
                stringBuilder.Append($@"On {this.trigger}: ");
            }

            stringBuilder.Append(skillName);
            stringBuilder.Append(allString);

            if(faction != "")
            {
                stringBuilder.Append($@" {faction}");
            }

            if(!String.IsNullOrEmpty(subSkillName))
            {
                stringBuilder.Append($@" {subSkillName}");
            }

            if (!String.IsNullOrEmpty(subSkill2Name))
            {
                stringBuilder.Append($@" into {subSkill2Name}");
            }

            if (this.number != -1)
            {
                stringBuilder.Append($@" {this.number}");
            }

            if(this.number != -1 && this.value != -1)
            {
                stringBuilder.Append(" for");
            }

            if(this.value != -1)
            {
                stringBuilder.Append($@" {this.value}");
            }

            if(this.cooldown != -1)
            {
                stringBuilder.Append($@" every {this.cooldown}");
            }

            if (!String.IsNullOrEmpty(this.subCard))
            {
                stringBuilder.Append($@" {this.subCard}");
            }

            return stringBuilder.ToString();
        }
    }
}
