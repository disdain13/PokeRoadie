using System;
using System.Diagnostics;
using Google.Protobuf;
using POGOProtos.Networking.Signature;
using PokemonGo.RocketAPI.Enums;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using PokemonGo.RocketAPI.Extensions;
using static POGOProtos.Networking.Envelopes.RequestEnvelope.Types;

namespace PokemonGo.RocketAPI.Helpers
{
    public class RequestBuilder
    {
        private readonly Client _client;
        private readonly string _authToken;
        private readonly AuthType _authType;
        private readonly double _latitude;
        private readonly double _longitude;
        private readonly double _altitude;
        private readonly AuthTicket _authTicket;
        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly Stopwatch _internalWatch = new Stopwatch();
        private ulong _nextRequestId;
        private static readonly Random rnd = new Random();

        public RequestBuilder(Client client)
        {
            _client = client;
            _authToken = client.AuthToken;
            _authType = client.AuthType;
            _latitude = client.CurrentLatitude;
            _longitude = _client.CurrentLongitude;
            _altitude = _client.CurrentAltitude;
            _authTicket = _client.AuthTicket;
            _nextRequestId = Convert.ToUInt64(rnd.NextDouble() * Math.Pow(10, 18));
            if (!_internalWatch.IsRunning) _internalWatch.Start();
        }

        public RequestEnvelope SetRequestEnvelopeUnknown6(RequestEnvelope requestEnvelope)
        {
            var rnd32 = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(rnd32);

            var ticketBytes = requestEnvelope.AuthTicket.ToByteArray();

            var sig = new Signature()
            {
                LocationHash1 =
                    Utils.GenerateLocation1(ticketBytes, requestEnvelope.Latitude, requestEnvelope.Longitude,
                        requestEnvelope.Altitude),
                LocationHash2 =
                    Utils.GenerateLocation2(requestEnvelope.Latitude, requestEnvelope.Longitude,
                        requestEnvelope.Altitude),
                Unk22 = ByteString.CopyFrom(rnd32),
                Timestamp = (ulong)DateTime.UtcNow.ToUnixTime(),
                TimestampSinceStart = (ulong)(DateTime.UtcNow.ToUnixTime() - _startTime.ToUnixTime()),
                SensorInfo = new Signature.Types.SensorInfo()
                {
                    AccelNormalizedZ = GenRandom(9.8),
                    AccelNormalizedX = GenRandom(0.02),
                    AccelNormalizedY = GenRandom(0.3),
                    TimestampSnapshot = (ulong)_internalWatch.ElapsedMilliseconds - 230,
                    MagnetometerX = GenRandom(0.12271042913198471),
                    MagnetometerY = GenRandom(-0.015570580959320068),
                    MagnetometerZ = GenRandom(0.010850906372070313),
                    AngleNormalizedX = GenRandom(17.950439453125),
                    AngleNormalizedY = GenRandom(-23.36273193359375),
                    AngleNormalizedZ = GenRandom(-48.8250732421875),
                    AccelRawX = GenRandom(-0.0120010357350111),
                    AccelRawY = GenRandom(-0.04214850440621376),
                    AccelRawZ = GenRandom(0.94571763277053833),
                    GyroscopeRawX = GenRandom(7.62939453125e-005),
                    GyroscopeRawY = GenRandom(-0.00054931640625),
                    GyroscopeRawZ = GenRandom(0.0024566650390625),
                    AccelerometerAxes = 3
                }
            };
            //sig.DeviceInfo = _client.DeviceInfo;
            sig.LocationFix.Add(new POGOProtos.Networking.Signature.Signature.Types.LocationFix()
            {
                Provider = "network",

                //Unk4 = 120,
                Latitude = (float)_client.CurrentLatitude,
                Longitude = (float)_client.CurrentLongitude,
                Altitude = (float)_client.CurrentAltitude,
                TimestampSinceStart = (ulong)_internalWatch.ElapsedMilliseconds - 200,
                Floor = 3,
                LocationType = 1
            });

            foreach (var request in requestEnvelope.Requests)
            {
                sig.RequestHash.Add(
                    Utils.GenerateRequestHash(ticketBytes, request.ToByteArray())
                );
            }

            requestEnvelope.Unknown6.Add(new Unknown6()
            {
                RequestType = 6,
                Unknown2 = new Unknown6.Types.Unknown2()
                {
                    Unknown1 = ByteString.CopyFrom(Crypt.Encrypt(sig.ToByteArray()))
                }
            });

            return requestEnvelope;
        }

        public RequestEnvelope GetRequestEnvelope(params Request[] customRequests)
        {
            return SetRequestEnvelopeUnknown6(new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = _nextRequestId++, //3
                Requests = { customRequests }, //4

                //Unknown6 = , //6
                Latitude = _client.CurrentLatitude, //7
                Longitude = _client.CurrentLongitude, //8
                Altitude = _client.CurrentAltitude, //9
                AuthTicket = _client.AuthTicket, //11
                Unknown12 = 989 //12
            });
        }

        public RequestEnvelope GetInitialRequestEnvelope(params Request[] customRequests)
        {
            return new RequestEnvelope
            {
                StatusCode = 2, //1

                RequestId = _nextRequestId++, //3
                Requests = { customRequests }, //4

                //Unknown6 = , //6
                Latitude = _client.CurrentLatitude, //7
                Longitude = _client.CurrentLongitude, //8
                Altitude = _client.CurrentAltitude, //9
                AuthInfo = new AuthInfo
                {
                    Provider = _authType == AuthType.Google ? "google" : "ptc",
                    Token = new AuthInfo.Types.JWT
                    {
                        Contents = _authToken,
                        Unknown2 = 14
                    }
                }, //10
                Unknown12 = 989 //12
            };
        }

        public RequestEnvelope GetRequestEnvelope(RequestType type, IMessage message)
        {
            return GetRequestEnvelope(new Request()
            {
                RequestType = type,
                RequestMessage = message.ToByteString()
            });

        }

        public static double GenRandom(double num)
        {
            var randomFactor = 0.3f;
            var randomMin = (num * (1 - randomFactor));
            var randomMax = (num * (1 + randomFactor));
            var randomizedDelay = rnd.NextDouble() * (randomMax - randomMin) + randomMin; ;
            return randomizedDelay; ;
        }
    }
}