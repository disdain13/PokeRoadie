using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Login;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;

namespace PokemonGo.RocketAPI.Rpc
{
    //public delegate void GoogleDeviceCodeDelegate(string code, string uri);
    public enum LoginResponseTypes
    {
        NoResponse,
        Success,
        LoginFailed,
        AccessTokenExpired,
        UnhandledException,
        PtcOffline,
        GoogleOffline,
        InvalidResponse,
        GoogleTwoStepAuthError,
        AccountNotVerified

    }

    public class LoginAttempt
    {
        public string Message { get; set; }
        public LoginResponseTypes Result { get; set; }
        public int Attempt { get; set; }
    }

    public class Login : BaseRpc
    {
        //public event GoogleDeviceCodeDelegate GoogleDeviceCodeEvent;
        private ILoginType login;
        public Login(Client client) : base(client)
        {
            _client = client;
            login = SetLoginType(_client.Settings);
        }

        private static ILoginType SetLoginType(ISettings settings)
        {
            switch (settings.AuthType)
            {
                case AuthType.Google:
                    return new GoogleLogin(settings.GoogleUsername, settings.GooglePassword);
                case AuthType.Ptc:
                    return new PtcLogin(settings);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.AuthType), "Unknown AuthType");
            }
        }

        public async Task DoLogin()
        {
            _client.GenerateNewSessionHash();
            _client.AuthToken = await login.GetAccessToken().ConfigureAwait(false);
            await SetServer().ConfigureAwait(false);
        }

        private async Task SetServer()
        {
            #region Standard intial request messages in right Order

            var getPlayerMessage = new GetPlayerMessage();
            var getHatchedEggsMessage = new GetHatchedEggsMessage();
            var getInventoryMessage = new GetInventoryMessage
            {
                LastTimestampMs = DateTime.UtcNow.ToUnixTime()
            };
            var checkAwardedBadgesMessage = new CheckAwardedBadgesMessage();
            var downloadSettingsMessage = new DownloadSettingsMessage
            {
                Hash = "05daf51635c82611d1aac95c0b051d3ec088a930"
            };

            #endregion

            var serverRequest = RequestBuilder.GetInitialRequestEnvelope(
                new Request
                {
                    RequestType = RequestType.GetPlayer,
                    RequestMessage = getPlayerMessage.ToByteString()
                }, new Request
                {
                    RequestType = RequestType.GetHatchedEggs,
                    RequestMessage = getHatchedEggsMessage.ToByteString()
                }, new Request
                {
                    RequestType = RequestType.GetInventory,
                    RequestMessage = getInventoryMessage.ToByteString()
                }, new Request
                {
                    RequestType = RequestType.CheckAwardedBadges,
                    RequestMessage = checkAwardedBadgesMessage.ToByteString()
                }, new Request
                {
                    RequestType = RequestType.DownloadSettings,
                    RequestMessage = downloadSettingsMessage.ToByteString()
                });


            var serverResponse = await PostProto<Request>(Resources.RpcUrl, serverRequest);

            if (serverResponse.AuthTicket == null)
            {
                _client.AuthToken = null;
                throw new AccessTokenExpiredException();
            }

            _client.AuthTicket = serverResponse.AuthTicket;
            _client.ApiUrl = serverResponse.ApiUrl;
        }

        public async Task<LoginAttempt> AttemptLogin()
        {
            var loginAttempt = new LoginAttempt();
            loginAttempt.Result = LoginResponseTypes.NoResponse;
            try
            {
                await DoLogin();
                loginAttempt.Result = LoginResponseTypes.Success;
            }
            catch (AggregateException ae)
            {
                loginAttempt.Message = ae.Flatten().InnerException.ToString();
                loginAttempt.Result = LoginResponseTypes.UnhandledException;
            }
            catch (NullReferenceException nre)
            {
                loginAttempt.Message = $"NullReferenceException - Causing Method: {nre.TargetSite} | Source: {nre.Source} | Data: {nre.Data}";
                loginAttempt.Result = LoginResponseTypes.UnhandledException;
            }
            catch (AccountNotVerifiedException)
            {
                loginAttempt.Message = $"Your {_client.Settings.AuthType} account does not seem to be verified yet, please check your email.";
                loginAttempt.Result = LoginResponseTypes.AccountNotVerified;
            }
            catch (LoginFailedException)
            {
                loginAttempt.Message = $"Login Failed, please check your credentials.";
                loginAttempt.Result = LoginResponseTypes.LoginFailed;
            }
            catch (AccessTokenExpiredException)
            {
                loginAttempt.Message = $"Access token expired.";
                loginAttempt.Result = LoginResponseTypes.AccessTokenExpired;
            }
            catch (PtcOfflineException)
            {
                loginAttempt.Message = $"The Ptc server is currently offline.";
                loginAttempt.Result = LoginResponseTypes.PtcOffline;
            }
            catch (GoogleOfflineException)
            {
                loginAttempt.Message = $"The Google authentication server is currently offline.";
                loginAttempt.Result = LoginResponseTypes.GoogleOffline;
            }
            catch (InvalidResponseException ire)
            {
                loginAttempt.Message = $"InvalidResponseException - Causing Method: {ire.TargetSite} | Source: {ire.Source} | Data: {ire.Data}";
                loginAttempt.Result = LoginResponseTypes.InvalidResponse;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("NeedsBrowser"))
                {
                    loginAttempt.Message = $"Google Two-Step Authentication is currently active. Please login to your Google account and turn off 'Two-Step Authentication' under security settings. If you do NOT want to disable your two-factor auth, please visit the following link and setup an app password. This is the only way of using the bot without disabling two-factor authentication: https://security.google.com/settings/security/apppasswords.";
                    loginAttempt.Result = LoginResponseTypes.GoogleTwoStepAuthError;
                }
                else if (ex.Message.Contains("BadAuthentication"))
                {
                    loginAttempt.Message = "$Login Failed, please check your credentials.";
                    loginAttempt.Result = LoginResponseTypes.LoginFailed;
                }
                else
                {
                    loginAttempt.Message = $"{ex.GetType().Name} - {ex.Message} | Causing Method: {ex.TargetSite} | Source: {ex.Source} | Data: {ex.Data}";
                    loginAttempt.Result = LoginResponseTypes.UnhandledException;
                }
            }
            return loginAttempt;
        }

    }
}
