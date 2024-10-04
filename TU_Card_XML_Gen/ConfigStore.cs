using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TU_Card_XML_Gen
{
    public class ConfigStore
    {
        public ConfigStore(XElement config)
        {
            AutoStats = new AutoStatsClass(config.XPathSelectElement("AutoStats"));
        }

        public class AutoStatsClass
        {
            public int PercentAcrossTier;
            public int PercentBetweenTiers;

            public AutoStatsClass(XElement config)
            {
                PercentAcrossTier = int.Parse(config.XPathSelectElement("PercentAcrossTier").Value);
                PercentBetweenTiers = int.Parse(config.XPathSelectElement("PercentBetweenTiers").Value);
            }
        }

        public AutoStatsClass AutoStats;
    }
}
