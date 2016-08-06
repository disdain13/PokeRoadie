using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Logging;

namespace PokeRoadie.Logging.Providers
{
    public class ConsoleLogProvider : ILogProvider
    {
        public void Initialize()
        {
            Console.OutputEncoding = Encoding.Unicode;
        }
        public void Write(LogEntry entry)
        {
            var dateString = entry.Date.ToString("HH:mm:ss");
            switch (entry.Level)
            {
                case LogLevel.Info:
                    System.Console.ForegroundColor = ConsoleColor.DarkGreen;
                    System.Console.WriteLine($"[{dateString}] (INFO) {entry.Message}");
                    break;
                case LogLevel.Warning:
                    System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                    System.Console.WriteLine($"[{dateString}] (ATTENTION) {entry.Message}");
                    break;
                case LogLevel.Error:
                    System.Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine($"[{dateString}] (ERROR) {entry.Message}");
                    break;
                case LogLevel.Debug:
                    System.Console.ForegroundColor = ConsoleColor.Gray;
                    System.Console.WriteLine($"[{dateString}] (DEBUG) {entry.Message}");
                    break;
                case LogLevel.Navigation:
                    System.Console.ForegroundColor = ConsoleColor.DarkCyan;
                    System.Console.WriteLine($"[{dateString}] (NAVIGATION) {entry.Message}");
                    break;
                case LogLevel.Pokestop:
                    System.Console.ForegroundColor = ConsoleColor.Cyan;
                    System.Console.WriteLine($"[{dateString}] (POKESTOP) {entry.Message}");
                    break;
                case LogLevel.Pokemon:
                    System.Console.ForegroundColor = ConsoleColor.Yellow;
                    System.Console.WriteLine($"[{dateString}] (PKMN) {entry.Message}");
                    break;
                case LogLevel.Transfer:
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"[{dateString}] (TRANSFER) {entry.Message}");
                    break;
                case LogLevel.Evolve:
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"[{dateString}] (EVOLVE) {entry.Message}");
                    break;
                case LogLevel.Berry:
                    System.Console.ForegroundColor = ConsoleColor.Magenta;
                    System.Console.WriteLine($"[{dateString}] (BERRY) {entry.Message}");
                    break;
                case LogLevel.Egg:
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"[{dateString}] (EGG) {entry.Message}");
                    break;
                case LogLevel.Recycling:
                    System.Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    System.Console.WriteLine($"[{dateString}] (RECYCLING) {entry.Message}");
                    break;
                case LogLevel.None:
                    System.Console.ForegroundColor = entry.Color;
                    System.Console.WriteLine($"[{dateString}] {entry.Message}");
                    break;
                default:
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"[{dateString}] {entry.Message}");
                    break;
            }
        }
        public void Close()
        {
            //not needed for this type of logger
        }
    }
}
