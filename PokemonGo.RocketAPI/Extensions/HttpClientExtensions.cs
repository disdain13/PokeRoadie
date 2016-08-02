#region

using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf;
using System.Diagnostics;
using PokemonGo.RocketAPI.Exceptions;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Responses;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Data;

#endregion

namespace PokemonGo.RocketAPI.Extensions
{
    public static class HttpClientExtensions
    {
        //////////////////////////////////////////////
        // POKEROADIE CUSTOM CODE
        //////////////////////////////////////////////
        public static System.DateTime _delayTime = System.DateTime.Now;
        //////////////////////////////////////////////


        public static async Task<TResponsePayload> PostProtoPayload<TRequest, TResponsePayload>(this System.Net.Http.HttpClient client,
            string url, RequestEnvelope requestEnvelope) where TRequest : IMessage<TRequest>
            where TResponsePayload : IMessage<TResponsePayload>, new()
        {
            Debug.WriteLine($"Requesting {typeof(TResponsePayload).Name}");

            //////////////////////////////////////////////
            // POKEROADIE CUSTOM CODE
            //////////////////////////////////////////////
            var nowDate = System.DateTime.Now;
            if (_delayTime >= nowDate)
            {
                await Task.Delay(_delayTime.Subtract(nowDate));
            }

            var resend = false;
            var tryCount = 0;
           
            do
            {
                tryCount++;

                var response = await PostProto<TRequest>(client, url, requestEnvelope);
                _delayTime = System.DateTime.Now.AddMilliseconds(Helpers.RandomHelper.RandomNumber(300, 320));

                if (response.Returns.Count == 0)
                {
                    await Task.Delay(500 * tryCount);
                    resend = true;
                    continue;
                }
                if (tryCount > 1)
                {
                    //Logging.Logger.Write($"Request initially failed, resent on attempt {tryCount} (Status Code {requestEnvelope.StatusCode})", Logging.LogLevel.Warning);
                }
                var payload = response.Returns[0];
                var parsedPayload = new TResponsePayload();
                parsedPayload.MergeFrom(payload);
                return parsedPayload;

            } while (resend && tryCount < 5);
            Logging.Logger.Write($"(FATAL ERROR) Request failed after {tryCount} attempts - {requestEnvelope.StatusCode}", Logging.LogLevel.None, System.ConsoleColor.Red);
            throw new InvalidResponseException("The server is not responding to our requests...");

            //Decode payload
            //todo: multi-payload support

        }

        public static async Task<ResponseEnvelope> PostProto<TRequest>(this System.Net.Http.HttpClient client, string url,
            RequestEnvelope requestEnvelope) where TRequest : IMessage<TRequest>
        {
            //Encode payload and put in envelop, then send
            var data = requestEnvelope.ToByteString();
            var result = await client.PostAsync(url, new ByteArrayContent(data.ToByteArray()));

            //Decode message
            var responseData = await result.Content.ReadAsByteArrayAsync();
            var codedStream = new CodedInputStream(responseData);
            var decodedResponse = new ResponseEnvelope();
            decodedResponse.MergeFrom(codedStream);

            return decodedResponse;
        }
    }
}