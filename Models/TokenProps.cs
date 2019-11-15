using System.Collections.Generic;
using System.Linq;

namespace ICFaucet.Models
{
    public class TokenProps
    {
        public string name { get; set; }
        public string denom { get; set; }
        /// <summary>
        /// Coin index: https://github.com/satoshilabs/slips/blob/master/slip-0044.md
        /// </summary>
        public int index { get; set; } = -1;

        /// <summary>
        /// Destination Address
        /// </summary>
        public string address { get; set; }

        /// <summary>
        /// Source Address
        /// </summary>
        public string origin { get; set; }

        public string prefix { get; set; }

        public string lcd { get; set; }
        public string network { get; set; }

        public long amount { get; set; } = 100000;
        public long gas { get; set; } = 200000;
        public long fees { get; set; } = 50000;

        public string memo { get; set; }
    }
}
