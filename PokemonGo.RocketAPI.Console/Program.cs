#region

using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Logging;

#endregion


namespace PokemonGo.RocketAPI.Console
{
    internal class Program
    {
        private static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException
                += delegate (object sender, UnhandledExceptionEventArgs eargs)
                {
                    Exception exception = (Exception)eargs.ExceptionObject;
                    System.Console.WriteLine("Unhandled exception: " + exception);
                    Environment.Exit(1);
                };

            ServicePointManager.ServerCertificateValidationCallback = Validator;
            Logger.SetLogger();

            Task.Run(() =>
            {
                bool settingsLoaded = false;
                Settings settings = null;
                try
                {
                    settings = new Settings();
                    settings.Load();
                    settingsLoaded = true;
                }
                catch (Exception e)
                {
                    Logger.Write("Could not load settings from configuration from file. Continuing with default settings. Error: " + e.ToString(), LogLevel.Error);
                }
                if (settingsLoaded)
                {
                    try
                    {
                        new Logic.Logic(settings).Execute().Wait();
                    }
                    catch (PtcOfflineException)
                    {
                        Logger.Write("PTC Servers are probably down OR your credentials are wrong. Try google", LogLevel.Error);
                        Logger.Write("Trying again in 60 seconds...");
                        Thread.Sleep(60000);
                        new Logic.Logic(settings).Execute().Wait();
                    }
                    catch (AccountNotVerifiedException)
                    {
                        Logger.Write("Account not verified. - Exiting");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"Unhandled exception: {ex}", LogLevel.Error);
                        new Logic.Logic(settings).Execute().Wait();
                    }
                }
            });
            System.Console.ReadLine();
        }

        public static bool Validator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}