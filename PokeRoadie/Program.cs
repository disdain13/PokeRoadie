#region " Imports "

using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Logging;

#endregion


namespace PokeRoadie
{
    internal class Program
    {
        public static void ExitApplication(int exitCode)
        {
            Application.Exit();
        }

        private static PokeRoadieLogic CreateLogic()
        {
            var logic = new PokeRoadieLogic();
            logic.OnPromptForCredentials += PokeRoadieSettings.Current.PromptForCredentials;
            logic.OnPromptForCoords += PokeRoadieSettings.Current.PromptForCoords;
            try
            {
                logic.Initialize();
            }
            catch (Exception ex)
            {
                Logger.Write($"Logic Initialization Exception: {ex}", LogLevel.Error);
            }
            return logic;
        }

        ///// <summary>
        ///// The main entry point for the application.
        ///// </summary>
        //[STAThread]
        //static void Main()
        //{
        //    Application.EnableVisualStyles();
        //    Application.SetCompatibleTextRenderingDefault(false);
        //    Application.Run(new Form1());
        //}

        private static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException
                += delegate (object sender, UnhandledExceptionEventArgs eargs)
                {
                    Exception exception = (Exception)eargs.ExceptionObject;
                    System.Console.WriteLine("Unhandled Exception: " + exception);
                    //Environment.Exit(1);
                };

            ServicePointManager.ServerCertificateValidationCallback = Validator;
            Logger.SetLogger();

            Task.Run(() =>
            {
                try
                {
                    CreateLogic().Execute().Wait();
                }
                catch (Exception ex)
                {
                    Logger.Write($"Fatal Exception: {ex}", LogLevel.Error);
                }
            });
            System.Console.ReadLine();
        }

        public static bool Validator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;


    }
}