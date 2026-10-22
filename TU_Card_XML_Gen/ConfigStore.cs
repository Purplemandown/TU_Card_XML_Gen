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
                // Optional, so older config files still work: no <Scaling> means AutoStats with the built-in tiered ranges.
                XElement? scaling = config.XPathSelectElement("StandardOperationConfig/Scaling");
                Tiered = TieredScaling.BuiltInDefault();
                ScalingMode = ScalingModes.AutoStats;

                if (scaling != null)
                {
                    string mode = scaling.XPathSelectElement("Mode")?.Value.Trim() ?? "autostats";

                    if (mode.Equals("tiered", StringComparison.OrdinalIgnoreCase))
                    {
                        ScalingMode = ScalingModes.Tiered;
                    }
                    else if (!mode.Equals("autostats", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Scaling/Mode must be autostats or tiered, not \"{mode}\".");
                    }

                    XElement? tiered = scaling.XPathSelectElement("Tiered");
                    if (tiered != null)
                    {
                        List<ScaleRange> fusionLevels = tiered.Elements()
                            .Where(x => x.Name.LocalName.StartsWith("FusionLevel", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(x => int.Parse(x.Name.LocalName.Substring("FusionLevel".Length)))
                            .Select(x => ScaleRange.Parse(x.Value))
                            .ToList();

                        if (fusionLevels.Count > 0)
                        {
                            Tiered.FusionLevels = fusionLevels;
                        }

                        string? summoned = tiered.XPathSelectElement("Summoned")?.Value;
                        if (!string.IsNullOrWhiteSpace(summoned))
                        {
                            Tiered.Summoned = ScaleRange.Parse(summoned);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Can't parse StandardOperationConfig/Scaling: {e.Message}");
            }

            try
            {
                MassNerfFactor =decimal.Parse(config.XPathSelectElement("MassNerfPoisonConfig/Factor")?.Value);
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

        public ScalingModes ScalingMode { get; private set; }

        /// <summary>Ranges used when ScalingMode is Tiered, and the fallback for "// SCALE:" directives.</summary>
        public TieredScaling Tiered { get; private set; }

        public enum ScalingModes : int
        {
            AutoStats = 0,
            Tiered = 1
        };

        public decimal MassNerfFactor;
        public UpdaterOperation Operation { get; private set; }

        public enum UpdaterOperation : int
        {
            Standard = 0,
            MassNerfPoison = 1
        };
    }
}
