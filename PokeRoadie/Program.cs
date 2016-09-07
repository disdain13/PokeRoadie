﻿#region " Imports "

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
            //load settings
            var settings = new PokeRoadieSettings();

            //load settings
            settings.Load();

            //create context
            var context = new Context(settings);
            
            //create logic class
            var logic = new PokeRoadieLogic(context);

            //add custom event wiring
            logic.OnPromptForCredentials += settings.PromptForCredentials;
            //logic.OnPromptForCoords += settings.PromptForCoords;

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

            //unhandled exception...uh.. handler? does that make sense?
            AppDomain.CurrentDomain.UnhandledException
                += HandleException;

            //set validation callback 
            ServicePointManager.ServerCertificateValidationCallback = Validator;

            //configure logging
            Logger.SetLogger();

            Start();

        }

        private static void Start()
        {
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
        private static void HandleException(object sender, UnhandledExceptionEventArgs eargs)
        {
            Exception exception = (Exception)eargs.ExceptionObject;
            try
            {
                Logger.Write("Unhandled Exception: " + exception, LogLevel.Error);
            }
            catch
            {
                System.Console.WriteLine("Unhandled Exception: " + exception, LogLevel.Error);
            }
            Start();
        }
        public static bool Validator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;


    }
}