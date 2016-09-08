using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokeRoadie.Logging.Providers;

namespace PokeRoadie.Logging
{
    public class Logger
    {

        #region " Singleton "

        private static Logger _current;
        public static Logger Current { get { _current = _current ?? new Logger(); return _current; } }

        #endregion
        #region " Properties "

        //list of providers which process logging requests. This will allow for 
        //anyone to implement the ILogProvider interface, and output to anything.
        public List<ILogProvider> LogProviders { get; private set; } = new List<ILogProvider>();

        #endregion
        #region " Methods "

       public void Initialize()
        {
            //initialize each provider
            foreach (var provider in LogProviders)
                if (provider != null)
                    provider.Initialize();
        }
        public static void Write(string message, PokeRoadie.Api.Logging.LogLevel level = PokeRoadie.Api.Logging.LogLevel.None, ConsoleColor color = ConsoleColor.White)
        {
            //create new log entry
            Logger.Current.Write(
                new LogEntry()
                {
                    Date = DateTime.Now,
                    Message = message,
                    Level = level,
                    Color = color
                });
        }
        public void Write(LogEntry entry)
        {
            //allow each log provider to handle it
            foreach (var provider in LogProviders)
                if (provider != null)
                    provider.Write(entry);
        }
        public void Close()
        {
            //close each provider
            foreach (var provider in LogProviders)
                if (provider != null)
                    provider.Close();
        }

        #endregion

    }
}
