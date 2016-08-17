using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRoadie
{
    [Serializable]
    public class SessionData
    {
        public DateTime StartDate { get; set; }
        public int CatchCount { get; set; }
        public int VisitCount { get; set; }
        public bool CatchEnabled { get; set; } = true;
        public bool VisitEnabled { get; set; } = true;
    }
}
