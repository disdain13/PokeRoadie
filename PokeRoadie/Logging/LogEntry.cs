using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRoadie.Logging
{
    public class LogEntry
    {
        public DateTime Date { get; set; }
        public string Message { get; set; }
        public PokeRoadie.Api.Logging.LogLevel Level { get; set; }
        public ConsoleColor Color { get; set; }
    }
}
