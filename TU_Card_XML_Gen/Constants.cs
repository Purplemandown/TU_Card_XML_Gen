using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TUComparatorLibrary
{
    internal class Constants
    {

        public static string GetRarityName(int rarity)
        {
            switch (rarity)
            {
                case 1:
                    return "Common";
                case 2:
                    return "Rare";
                case 3:
                    return "Epic";
                case 4:
                    return "Legendary";
                case 5:
                    return "Vindicator";
                case 6:
                    return "Mythic";
                default:
                    return "Other";
            }
        }
    }
}
