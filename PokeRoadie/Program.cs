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
        static int exitCode = 0;

        public static void ExitApplication(int exitCode)
        {
            Program.exitCode = exitCode;
            Application.Exit();
        }

        private static PokeRoadieLogic CreateLogic()
        {
            var logic = new PokeRoadieLogic();
            logic.OnPromptForCredentials += PokeRoadieSettings.Current.PromptForCredentials;
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
                    System.Console.WriteLine("Unhandled exception: " + exception);
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
                catch (PtcOfflineException)
                {
                    Logger.Write("PTC Servers are probably down OR your credentials are wrong. Try google", LogLevel.Error);
                    Logger.Write("Trying again in 60 seconds...");
                    Thread.Sleep(60000);
                    CreateLogic().Execute().Wait();
                }
                catch (AccountNotVerifiedException)
                {
                    Logger.Write("Account not verified. - Exiting");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Unhandled exception: {ex}", LogLevel.Error);
                    CreateLogic().Execute().Wait();
                }
            });
            System.Console.ReadLine();
        }

        public static bool Validator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;


    }
}