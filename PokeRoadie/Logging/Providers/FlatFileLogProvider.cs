using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PokeRoadie.Logging.Providers
{
    public class FlatFileLogProvider : ILogProvider
    {
  
        string _currentFile = string.Empty;
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

        public void Initialize()
        {
            if (!Directory.Exists(path))
            {
                DirectoryInfo di = Directory.CreateDirectory(path);
            }
            _currentFile = DateTime.Now.ToString("yyyy-MM-dd - HH.mm.ss");
            Log($"Initializing Rocket logger @ {DateTime.Now}...");
        }

        public void Write(LogEntry entry)
        {
            Log($"[{entry.Date.ToString("HH:mm:ss")}] {(string.IsNullOrWhiteSpace(entry.Message) ? "[No Message]" : entry.Message)}");
        }

        private void Log(string message)
        {
            // maybe do a new log rather than appending?
            using (var log = File.AppendText(Path.Combine(path, _currentFile + ".txt")))
            {
                log.WriteLine(message);
                log.Flush();
            }
        }

        public void Close()
        {
            //currently not required. I think I could minimize existing file access calls
            //by maintaining a FIFO list of messages to write, and on a separate thread, 
            //dump them once in a while. When we do that, we will need this to ensure
            //everything is written to file before close. 
        }
    }
}
