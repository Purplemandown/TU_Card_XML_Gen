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
            try
            {
                AutoStats = new AutoStatsClass(config.XPathSelectElement("StandardOperationConfig/AutoStats"));
            }
            catch (Exception e)
            {
                throw new Exception("Can't parse StandardOperationConfig/AutoStats");
            }

            try
            {
                MassNerfFactor = decimal.Parse(config.XPathSelectElement("MassNerfPoisonConfig/Factor")?.Value);
            }
            catch (Exception e)
            {
                throw new Exception("Can't parse MassNerfPoisonConfig/Factor");
            }

            switch (config.XPathSelectElement("Operation")?.Value)
            {
                case "standard":
                    Operation = UpdaterOperation.Standard;
                    break;
                case "massnerfpoison":
                    Operation = UpdaterOperation.MassNerfPoison;
                    break;
                default:
                    throw new Exception("Config file doesn't have a valid value for Operation.");
            }
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

        public decimal MassNerfFactor;
        public UpdaterOperation Operation { get; private set; }

        public enum UpdaterOperation : int
        {
            Standard = 0,
            MassNerfPoison = 1
        };
    }
}
