using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TU_Card_XML_Gen
{
    internal class OutputSkillData
    {
        public string skillId;
        public int[] x;
        public int[] y;
        public int[] all;
        public int[] c;
        public string[] s;
        public string[] s2;
        public int[] n;
        public string[] trigger;

        public OutputSkillData(string skillId, int[] x, int[] y, int[] all, int[] c, string[] s, string[] s2, int[] n, string[] trigger)
        {
            this.skillId = skillId;
            this.x = x;
            this.y = y;
            this.all = all;
            this.c = c;
            this.s = s;
            this.s2 = s2;
            this.n = n;
            this.trigger = trigger;
        }
    }
}
