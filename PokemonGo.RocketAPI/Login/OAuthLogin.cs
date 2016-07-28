//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json;
//using System.IO;
//using System.Net;
//using System.Net.Sockets;
//using System.Security.Cryptography;
//using System.Runtime.InteropServices;

//namespace PokemonGo.RocketAPI.Login
//{
//    public class OAuthLogin
//    {

//        #region " Constants "

//        // client configuration
//        //const string clientID = "581786658708-elflankerquo1a6vsckabbhn25hclla0.apps.googleusercontent.com";
//        //const string clientSecret = "3f6NggMbPtrmIBpgx-MK2xXK";
//        const string clientID = "848232511240-73ri3t7plvk96pj4f85uj8otdat2alem.apps.googleusercontent.com";
//        const string clientSecret = "NCjF1TLi2CcY6t5mt0ZveuL7";
//        const string authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
//        const string tokenEndpoint = "https://www.googleapis.com/oauth2/v4/token";
//        const string userInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

//        #endregion
//        #region " Utitlity Methods "

//        /// <summary>
//        /// Returns URI-safe data with a given input length.
//        /// </summary>
//        /// <param name="length">Input length (nb. output will be longer)</param>
//        /// <returns></returns>
//        public static string randomDataBase64url(uint length)
//        {
//            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
//            byte[] bytes = new byte[length];
//            rng.GetBytes(bytes);
//            return base64urlencodeNoPadding(bytes);
//        }

//        /// <summary>
//        /// Returns the SHA256 hash of the input string.
//        /// </summary>
//        /// <param name="inputStirng"></param>
//        /// <returns></returns>
//        public static byte[] sha256(string inputStirng)
//        {
//            byte[] bytes = Encoding.ASCII.GetBytes(inputStirng);
//            SHA256Managed sha256 = new SHA256Managed();
//            return sha256.ComputeHash(bytes);
//        }

//        /// <summary>
//        /// Base64url no-padding encodes the given input buffer.
//        /// </summary>
//        /// <param name="buffer"></param>
//        /// <returns></returns>
//        public static string base64urlencodeNoPadding(byte[] buffer)
//        {
//            string base64 = Convert.ToBase64String(buffer);

//            // Converts base64 to base64url.
//            base64 = base64.Replace("+", "-");
//            base64 = base64.Replace("/", "_");
//            // Strips padding.
//            base64 = base64.Replace("=", "");

//            return base64;
//        }

//        // Hack to bring the Console window to front.
//        // ref: http://stackoverflow.com/a/12066376

//        [DllImport("kernel32.dll", ExactSpelling = true)]
//        public static extern IntPtr GetConsoleWindow();

//        [DllImport("user32.dll")]
//        [return: MarshalAs(UnmanagedType.Bool)]
//        public static extern bool SetForegroundWindow(IntPtr hWnd);

//        public static void BringConsoleToFront()
//        {
//            SetForegroundWindow(GetConsoleWindow());
//        }

//        public static int GetRandomUnusedPort()
//        {
//            var listener = new TcpListener(IPAddress.Loopback, 0);
//            listener.Start();
//            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
//            listener.Stop();
//            return port;
//        }

//        #endregion
//        #region " Properties "

//        public static string Code { get; set; }
//        public static string AccessToken { get; set; }

//        #endregion
//        #region " Auth Methods "

//        public static async Task<string> GetToken()
//        {
//            // Generates state and PKCE values.
//            string state = randomDataBase64url(32);
//            string code_verifier = randomDataBase64url(32);
//            string code_challenge = base64urlencodeNoPadding(sha256(code_verifier));
//            const string code_challenge_method = "S256";

//            // Creates a redirect URI using an available port on the loopback address.
//            string redirectURI = string.Format("http://{0}:{1}/", IPAddress.Loopback, GetRandomUnusedPort());
//            //output("redirect URI: " + redirectURI);

//            // Creates an HttpListener to listen for requests on that redirect URI.
//            var http = new HttpListener();
//            http.Prefixes.Add(redirectURI);
//            //output("Listening..");
//            http.Start();

//            // Creates the OAuth 2.0 authorization request.
//            string authorizationRequest = string.Format("{0}?response_type=code&scope=openid%20profile&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}&code_challenge_method={5}",
//                authorizationEndpoint,
//                System.Uri.EscapeDataString(redirectURI),
//                clientID,
//                state,
//                code_challenge,
//                code_challenge_method);

//            // Opens request in the browser.
//            System.Diagnostics.Process.Start(authorizationRequest);

//            // Waits for the OAuth authorization response.
//            var context = await http.GetContextAsync();

//            // Brings the Console to Focus.
//            BringConsoleToFront();

//            // Sends an HTTP response to the browser.
//            var response = context.Response;
//            string responseString = string.Format("<html><head><meta http-equiv='refresh' content='10;url=https://google.com'></head><body>Please return to the app.</body></html>");
//            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
//            response.ContentLength64 = buffer.Length;
//            var responseOutput = response.OutputStream;
//            Task responseTask = responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
//            {
//                responseOutput.Close();
//                http.Stop();
//                //Console.WriteLine("HTTP server stopped.");
//            });

//            // Checks for errors.
//            if (context.Request.QueryString.Get("error") != null)
//            {
//                throw new Exceptions.InvalidResponseException(String.Format("OAuth authorization error: {0}.", context.Request.QueryString.Get("error")));
//            }
//            if (context.Request.QueryString.Get("code") == null
//                || context.Request.QueryString.Get("state") == null)
//            {
//                throw new Exceptions.InvalidResponseException("Malformed authorization response. " + context.Request.QueryString);
//            }

//            // extracts the code
//            var code = context.Request.QueryString.Get("code");
//            var incoming_state = context.Request.QueryString.Get("state");

//            // Compares the receieved state to the expected value, to ensure that
//            // this app made the request which resulted in authorization.
//            if (incoming_state != state)
//            {
//                throw new Exceptions.AccountNotVerifiedException(String.Format("Received request with invalid state ({0})", incoming_state));

//            }

//            //output("Authorization code: " + code);
//            Code = code;

//            // Starts the code exchange at the Token Endpoint.
//            var result = await performCodeExchange(code, code_verifier, redirectURI);
//            return result;
//        }

//        private static async Task<string> performCodeExchange(string code, string code_verifier, string redirectURI)
//        {
//            //output("Exchanging code for tokens...");

//            // builds the  request
//            string tokenRequestURI = tokenEndpoint;
//            string tokenRequestBody = string.Format("code={0}&redirect_uri={1}&client_id={2}&code_verifier={3}&client_secret={4}&scope=&grant_type=authorization_code",
//                code,
//                System.Uri.EscapeDataString(redirectURI),
//                clientID,
//                code_verifier,
//                clientSecret
//                );

//            // sends the request
//            HttpWebRequest tokenRequest = (HttpWebRequest)WebRequest.Create(tokenRequestURI);
//            tokenRequest.Method = "POST";
//            tokenRequest.ContentType = "application/x-www-form-urlencoded";
//            tokenRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
//            byte[] _byteVersion = Encoding.ASCII.GetBytes(tokenRequestBody);
//            tokenRequest.ContentLength = _byteVersion.Length;
//            Stream stream = tokenRequest.GetRequestStream();
//            await stream.WriteAsync(_byteVersion, 0, _byteVersion.Length);
//            stream.Close();

//            try
//            {
//                // gets the response
//                WebResponse tokenResponse = await tokenRequest.GetResponseAsync();
//                using (StreamReader reader = new StreamReader(tokenResponse.GetResponseStream()))
//                {
//                    // reads response body
//                    string responseText = await reader.ReadToEndAsync();
//                    //Console.WriteLine(responseText);

//                    // converts to dictionary
//                    Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);
//                    //set access token
//                     AccessToken  = tokenEndpointDecoded["access_token"];
//                }
//                return AccessToken;
//            }
//            catch (WebException ex)
//            {
//                if (ex.Status == WebExceptionStatus.ProtocolError)
//                {
//                    var response = ex.Response as HttpWebResponse;
//                    if (response != null)
//                    {
//                        var builder = new StringBuilder();
//                        builder.Append("HTTP: " + response.StatusCode + " - ");
//                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
//                        {
//                            // reads response body
//                            string responseText = await reader.ReadToEndAsync();
//                            builder.Append(responseText);
//                        }
//                        var message = builder.ToString();
//                        builder.Clear();
//                        builder = null;
//                        throw new Exceptions.InvalidResponseException(message);
//                    }

//                }
//            }
//            return string.Empty;
//        }


//        public static async Task<string> GetUserInfo(string accessToken)
//        {
//            //output("Making API Call to Userinfo...");

//            // builds the  request
//            string userinfoRequestURI = userInfoEndpoint;

//            // sends the request
//            HttpWebRequest userinfoRequest = (HttpWebRequest)WebRequest.Create(userinfoRequestURI);
//            userinfoRequest.Method = "GET";
//            userinfoRequest.Headers.Add(string.Format("Authorization: Bearer {0}", accessToken));
//            userinfoRequest.ContentType = "application/x-www-form-urlencoded";
//            userinfoRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

//            // gets the response
//            string userinfoResponseText = "";
//            WebResponse userinfoResponse = await userinfoRequest.GetResponseAsync();
//            using (StreamReader userinfoResponseReader = new StreamReader(userinfoResponse.GetResponseStream()))
//            {
//                // reads response body
//                userinfoResponseText = await userinfoResponseReader.ReadToEndAsync();
//                //output(userinfoResponseText);
//            }
//            return userinfoResponseText;
//        }


//        #endregion

//    }
//}
