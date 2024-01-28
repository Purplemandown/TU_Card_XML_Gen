using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TUComparatorLibrary
{
    internal class Card
    {
        public int id;
        public int fileIndex;
        public string name;
        public int rarity;
        public int faction;
        public int delay;
        public Dictionary<int, UpgradeLevel> upgradeLevels;

        internal Card(XElement cardXml, int fileIndex)
        {
            this.id = int.Parse(cardXml.Element("id").Value);
            this.name = cardXml.Element("name").Value;
            this.rarity = int.Parse(cardXml.Element("rarity")?.Value);
            this.faction = int.Parse(cardXml.Element("type")?.Value);
            this.delay = int.Parse(!string.IsNullOrEmpty(cardXml.Element("cost")?.Value) ? cardXml.Element("cost")?.Value : "0");

            this.upgradeLevels = new Dictionary<int, UpgradeLevel>();

            // Build the first upgrade level
            this.upgradeLevels.Add(1, new UpgradeLevel(cardXml));

            // Build the other upgrade levels
            IEnumerable<XElement> upgradeXmls = cardXml.XPathSelectElements("upgrade").OrderBy(x => int.Parse(x.XPathSelectElement("level")?.Value ?? "-1"));

            var priorUpgradeLevel = this.upgradeLevels.GetValueOrDefault(1);

            if (this.id == 26)
            {
                int i = 0;
            }

            foreach (XElement upgradeXml in upgradeXmls)
            {
                UpgradeLevel upgradeLevel = new UpgradeLevel(upgradeXml, priorUpgradeLevel);

                this.upgradeLevels.Add(upgradeLevel.level, upgradeLevel);
                priorUpgradeLevel = upgradeLevel;
            }

            this.fileIndex = fileIndex;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($@"{this.name}");

            string faction = Updater.factionData.Where(x => x.Element("id")?.Value.Equals(this.faction.ToString()) ?? false).FirstOrDefault()?.Element("name")?.Value ?? "";

            sb.AppendLine($@"{Constants.GetRarityName(this.rarity)} {faction}");

            sb.AppendLine($@"Delay: {this.delay}");

            sb.AppendLine("----");

            foreach(int upgradeLevelKey in upgradeLevels.Keys)
            {
                sb.AppendLine(upgradeLevels[upgradeLevelKey].ToString());
            }

            return sb.ToString();
        }
    }
}
