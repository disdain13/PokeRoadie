#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Exceptions;

using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;

using PokeRoadie.Extensions;
using PokemonGo.RocketAPI.Rpc;

#endregion

namespace PokeRoadie.Utils
{
    public class ApiFailureStrategy : IApiFailureStrategy
    {
        private int _retryCount;
        public Context Context { get; private set; }

        public ApiFailureStrategy(Context context)
        {
            Context = context;
        }

        public async Task<ApiOperation> HandleApiFailure()
        {
            if (_retryCount == 11)
            {

                return ApiOperation.Abort;
            }
                

            //I do not like hard delays
            await Task.Delay(1000);

            _retryCount++;

            if (_retryCount % 5 == 0)
            {
                Logger.Write($"The ApiOperation call failed {_retryCount} times, attempting re-authentication...", LogLevel.Error);
                return (await DoLogin(_retryCount));
            }
            else
            {
                return ApiOperation.Retry;
            }
        }

        public void HandleApiSuccess()
        {
            _retryCount = 0;
        }

        private async Task<ApiOperation> DoLogin(int retryCount)
        {
            //wait a second
            await Task.Delay(1000);
            //atempt re-auth
            var loginResponse = await Context.Client.Login.AttemptLogin();
            //if success return
            if (loginResponse.Result == LoginResponseTypes.Success)
            {
                Logger.Append("Success!");
                return ApiOperation.Retry;
            }
            //log
            Logger.Append("Failed!");
            Logger.Write($"Re-Authentication failed : {loginResponse.Result}{(loginResponse.Message == null ? "" : " - " + loginResponse.Message)}", LogLevel.Error);
            //set context for another login attempt
            Context.Logic.NeedsNewLogin = true;
            //determine failure response
            var delay = 5000;
            var exitCode = 0;
            switch (loginResponse.Result)
            {
                case LoginResponseTypes.GoogleOffline:
                    delay = 30000;
                    break;
                case LoginResponseTypes.PtcOffline:
                    delay = 30000;
                    break;
                case LoginResponseTypes.GoogleTwoStepAuthError:
                    exitCode = 2;
                    break;
                case LoginResponseTypes.AccountNotVerified:
                    exitCode = 3;
                    break;
                default:
                    break;
            }

            //if we have an exit code, close the application
            if (exitCode > 0) await Context.Logic.CloseApplication(exitCode);

            //wait for delay if needed
            if (delay > 0) await Task.Delay(delay);

            //return abort
            return (retryCount > 5) ? ApiOperation.Abort : ApiOperation.Retry;

        }
        public void HandleApiSuccess(RequestEnvelope request, ResponseEnvelope response)
        {
            if (response.StatusCode == 3)
            {
                for (int i = 0; i < request.Requests.Count; i++)
                {
                    //detect ban based on empty inventory
                    if (request.Requests[i].RequestType == RequestType.GetInventory && response.Returns[i].IsEmpty)
                    {
                        Logger.Write($"(BAN) No inventory response was returned from the server, which generally means you have been banned. Shutting down the application...", LogLevel.None, ConsoleColor.Red);
                        //wait
                        for (int y = 0; y < 20; y++)
                            System.Threading.Thread.Sleep(1000);
                        //exit
                        Environment.Exit(0);
                    }
                }
            }
            _retryCount = 0;
        }

        public async Task<ApiOperation> HandleApiFailure(RequestEnvelope request, ResponseEnvelope response)
        {
            if (_retryCount == 11)
                return ApiOperation.Abort;

            await Task.Delay(1000);
            _retryCount++;

            if (_retryCount % 5 == 0)
            {
                Logger.Write($"The API request/response call failed {_retryCount} times, attempting re-authentication...", LogLevel.Error);
                return (await DoLogin(_retryCount));
            }
            return ApiOperation.Retry;
        }
    }
}

