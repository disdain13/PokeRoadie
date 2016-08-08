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

#endregion

namespace PokeRoadie.Utils
{
    public class ApiFailureStrategy : IApiFailureStrategy
    {
        private int _retryCount;
        public PokeRoadieClient Client { get; set; }

        public ApiFailureStrategy()
        {
        }

        public async Task<ApiOperation> HandleApiFailure()
        {
            if (_retryCount == 11)
                return ApiOperation.Abort;

            //I do not like hard delays
            await Task.Delay(500);

            _retryCount++;

            if (_retryCount % 5 == 0)
            {
                DoLogin();
            }

            return ApiOperation.Retry;
        }

        public void HandleApiSuccess()
        {
            _retryCount = 0;
        }

        private async void DoLogin()
        {
            try
            {
                await Client.Login.DoLogin();
            }
            catch (PtcOfflineException)
            {
                Logger.Write("(API ERROR) The Ptc servers are currently offline. Waiting 30 seconds... ", LogLevel.None, ConsoleColor.Red);
                await Task.Delay(20000);
            }
            catch (AccessTokenExpiredException)
            {
                Logger.Write("(API ERROR) Access Token Expired. Waiting a couple seconds... ", LogLevel.None, ConsoleColor.Red);
                await Task.Delay(2000);
            }
            catch (Exception ex) when (ex is InvalidResponseException || ex is TaskCanceledException)
            {
                Logger.Write("(API ERROR) They don't like us pushing it... ", LogLevel.None, ConsoleColor.Red);
                await Task.Delay(350);
            }
            catch (AggregateException ae)
            {
                var fe = ae.Flatten()?.InnerException;
                Logger.Write($"(API ERROR) Aggregate Exception{(fe != null ? " - " + fe.ToString() : "")}", LogLevel.None, ConsoleColor.Red);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
        }
        public void HandleApiSuccess(RequestEnvelope request, ResponseEnvelope response)
        {
            _retryCount = 0;
        }

        public async Task<ApiOperation> HandleApiFailure(RequestEnvelope request, ResponseEnvelope response)
        {
            if (_retryCount == 11)
                return ApiOperation.Abort;

            await Task.Delay(500);
            _retryCount++;

            if (_retryCount % 5 == 0)
            {
                try
                {
                    DoLogin();
                }
                catch (PtcOfflineException)
                {
                    await Task.Delay(20000);
                }
                catch (AccessTokenExpiredException)
                {
                    await Task.Delay(2000);
                }
                catch (Exception ex) when (ex is InvalidResponseException || ex is TaskCanceledException)
                {
                    await Task.Delay(1000);
                }
            }

            return ApiOperation.Retry;
        }
    }
}

