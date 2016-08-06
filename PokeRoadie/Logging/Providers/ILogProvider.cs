using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRoadie.Logging.Providers
{
    public interface ILogProvider
    {
        void Initialize();
        void Write(LogEntry entry);
        void Close();
    }

}
