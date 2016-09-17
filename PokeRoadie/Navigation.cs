#region " Imports "

using System;
using System.Threading.Tasks;

using PokeRoadie.Api;
using PokeRoadie.Api.Logging;
using PokeRoadie.Api.Helpers;

using POGOProtos.Networking.Responses;
using POGOProtos.Map.Fort;

using PokeRoadie.Extensions;

#endregion


namespace PokeRoadie
{
    public class Navigation
    {

        #region " Members "

        private const double SpeedDownTo = 2 / 3.6;
        private DateTime? _lastSaveDate;
        private DateTime? _lastPingDate;
        private Random random = new Random(DateTime.Now.Millisecond);
        public event Action<LocationData> OnChangeLocation;
        public double LastKnownSpeed { get; set; }

        #endregion
        #region " Properties "

        public Context Context { get; set; }

        #endregion
        #region " Constructors "
        public Navigation(Context context)
        {
            Context = context;
        }

        #endregion
        #region " Utility Methods "

        private static double ToBearing(double radians)
        {
            // convert radians to degrees (as bearing: 0...360)
            return (ToDegrees(radians) + 360) % 360;
        }

        private static double ToDegrees(double radians)
        {
            return radians * 180 / Math.PI;
        }
        private static double ToRad(double degrees)
        {
            return degrees * (Math.PI / 180);
        }
        public static double CalculateDistanceInMeters(double sourceLat, double sourceLng, double destLat, double destLng)
        // from http://stackoverflow.com/questions/6366408/calculating-distance-between-two-latitude-and-longitude-geocoordinates
        {
            var sourceLocation = new GeoCoordinate(sourceLat, sourceLng);
            var targetLocation = new GeoCoordinate(destLat, destLng);
            return sourceLocation.GetDistanceTo(targetLocation);
        }
        private double GenRandom(double num, double min, double max)
        {
            return random.NextDouble() * (max - min) + min; 
        }

        #endregion
        #region " Primary Methods "

        private float GetLastKnownSpeedConverted()
        {
            return ((float)(LastKnownSpeed * (int)100)) / 100;
        }
        private async Task<PlayerUpdateResponse> UpdatePlayerLocation(LocationData destination)
        {
            return await UpdatePlayerLocation(destination.Latitude,destination.Longitude,destination.Altitude);
        }
        private async Task<PlayerUpdateResponse> UpdatePlayerLocation(GeoCoordinate geo)
        {
            return await UpdatePlayerLocation(geo.Latitude, geo.Longitude, geo.Altitude);
        }
        private async Task<PlayerUpdateResponse> UpdatePlayerLocation(double lat, double lng, double alt)
        {

            //randomize altitude
            alt = Math.Round((alt < 5) ? GenRandom(alt, 2, (5 * (1 + .02))) : 
                  (alt > 25) ? GenRandom(alt, (alt * (1 - .02)), 1) : 
                  GenRandom(alt, (alt * (1 - .03)), (alt * (1 + .03))),1);

            Context.Settings.CurrentLatitude = lat;
            Context.Settings.CurrentLongitude = lng;
            Context.Settings.CurrentAltitude = alt;

            if (!_lastSaveDate.HasValue || _lastSaveDate.Value < DateTime.Now)
            {
                Context.Settings.Save();
                Context.Session.Save();
                _lastSaveDate = DateTime.Now.AddSeconds(10);
            }

            if (!_lastPingDate.HasValue || _lastPingDate.Value < DateTime.Now)
            {
                Context.Utility.Save(new GeoCoordinate(lat, lng, alt), Context.Statistics.PlayerName, Context.Statistics.Currentlevel, System.IO.Path.Combine(Context.Directories.PingDirectory, DateTime.Now.Hour.ToString().PadLeft(2,'0') + DateTime.Now.Minute.ToString().PadLeft(2, '0') + DateTime.Now.Second.ToString().PadLeft(2, '0') + ".xml"));
                _lastPingDate = DateTime.Now.AddMinutes(1);
            }

            var r = await Context.Client.Player.UpdatePlayerLocation(lat, lng, alt, GetLastKnownSpeedConverted());
            OnChangeLocation?.Invoke(new LocationData(lat, lng, alt));
            return r;
        }


        public async Task<PlayerUpdateResponse> HumanLikeWalking(GeoCoordinate targetLocation,
           double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking, bool allowRandomization = true)
        {

            //randomize speed for less detection
            if (allowRandomization && Context.Settings.EnableSpeedRandomizer)
            {
                if (walkingSpeedInKilometersPerHour > 3)
                {
                    walkingSpeedInKilometersPerHour += RandomHelper.RandomNumber(-2, 3);
                }
                else
                {
                    walkingSpeedInKilometersPerHour += RandomHelper.RandomNumber(0, 2);
                }
            }

            var speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
            var sourceLocation = new GeoCoordinate(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude);
            var distanceToTarget = sourceLocation.CalculateDistanceInMeters(targetLocation);
            var dynamicLandingDistance = distanceToTarget > 18 ? random.Next(3, 18) : distanceToTarget > 3 ? random.Next(1, 3) : 2;
            distanceToTarget = distanceToTarget - dynamicLandingDistance;
            if (distanceToTarget < 1) distanceToTarget = 1;
            var seconds = distanceToTarget / speedInMetersPerSecond;
            //adjust speed to try and keep the trip under a minute, might not be possible
            if (walkingSpeedInKilometersPerHour < Context.Settings.MaxSpeed && Context.Settings.EnableSpeedAdjustment)
            {
                while (seconds > Context.Settings.MaxSecondsBetweenStops && walkingSpeedInKilometersPerHour < Context.Settings.MaxSpeed)
                {
                    walkingSpeedInKilometersPerHour++;
                    speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
                    seconds = distanceToTarget / speedInMetersPerSecond;
                }
            }

            //record last known speed
            LastKnownSpeed = walkingSpeedInKilometersPerHour;

            //log distance and time
            if (seconds > 300)
            {
                Logger.Write($"(NAVIGATION) Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.None, ConsoleColor.Red);
            }
            else
            {
                Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.Navigation);
            }

            //get next bearing
            var nextWaypointBearing = sourceLocation.DegreeBearing(targetLocation);

            //log debug messages
            if (Context.Client.Settings.ShowDebugMessages)
            {
                Logger.Write($"From {sourceLocation} to {targetLocation} bearing {Math.Round(nextWaypointBearing,1)}", LogLevel.Debug);
                var distanceFromStart = Navigation.CalculateDistanceInMeters(
                Context.Client.CurrentLatitude, Context.Client.CurrentLongitude,
                Context.Settings.WaypointLatitude, Context.Settings.WaypointLongitude);
                Logger.Write($"{Math.Round(distanceFromStart, 1)} meters from current waypoint ({Context.Settings.MaxDistance} meters max)", LogLevel.Debug);
            }

            var nextWaypointDistance = speedInMetersPerSecond;
            var waypoint = sourceLocation.CreateWaypoint(nextWaypointDistance, nextWaypointBearing);

            //Initial walking
            var requestSendDateTime = DateTime.Now;
            var result =
                await
                    UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, Context.Client.Settings.CurrentAltitude);

            var minMetersPerSecond = Context.Client.Settings.MinSpeed / 3.6;
            var enableCurve = distanceToTarget > (Context.Client.Settings.SpeedCurveDistance * 2);
            
            do
            {
                var millisecondsUntilGetUpdatePlayerLocationResponse =
                    (DateTime.Now - requestSendDateTime).TotalMilliseconds;

                sourceLocation = new GeoCoordinate(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude);
                var currentDistanceToTarget = sourceLocation.CalculateDistanceInMeters(targetLocation);
                //if (Context.Client.Settings.DebugMode)
                //Logger.Write($"Distance to target location: {currentDistanceToTarget:0.##} meters. Will take {currentDistanceToTarget / speedInMetersPerSecond:0.##} seconds!", LogLevel.Navigation);

                if (enableCurve && currentDistanceToTarget < Context.Client.Settings.SpeedCurveDistance  && speedInMetersPerSecond > (minMetersPerSecond + .2))
                {
                    speedInMetersPerSecond -= .55;
                    LastKnownSpeed = speedInMetersPerSecond * 3.6;
                    if (Context.Client.Settings.ShowDebugMessages)
                        Logger.Write($"{Math.Round(currentDistanceToTarget,1)} meters from the target, slowing down to {LastKnownSpeed}...", LogLevel.Debug);
                }

                nextWaypointDistance = Math.Min(currentDistanceToTarget,
                    millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
                nextWaypointBearing = sourceLocation.DegreeBearing(targetLocation);
                waypoint = sourceLocation.CreateWaypoint(nextWaypointDistance, nextWaypointBearing);

                requestSendDateTime = DateTime.Now;
                result =
                    await
                        UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude,
                            Context.Client.Settings.CurrentAltitude);

                if (functionExecutedWhileWalking != null)
                    await functionExecutedWhileWalking();// look for pokemon

                //check for needs new login
                if (PokeRoadieLogic.NeedsNewLogin) return result;

                await Task.Delay(500);
            } while (sourceLocation.CalculateDistanceInMeters(targetLocation) >= dynamicLandingDistance);

            return result;
        }

        public async Task<PlayerUpdateResponse> HumanPathWalking(GpxReader.Trkpt trk,
            double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking)
        {
            var targetLocation = new GeoCoordinate(Convert.ToDouble(trk.Lat), Convert.ToDouble(trk.Lon));

            var speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;

            var sourceLocation = new GeoCoordinate(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude);
            var distanceToTarget = sourceLocation.CalculateDistanceInMeters(targetLocation);
            Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {distanceToTarget / speedInMetersPerSecond:0.##} seconds!", LogLevel.Navigation);
            var seconds = distanceToTarget / speedInMetersPerSecond;
            //adjust speed to try and keep the trip under a minute, might not be possible
            if (walkingSpeedInKilometersPerHour < Context.Settings.MaxSpeed && Context.Settings.EnableSpeedAdjustment)
            {
                while (seconds > Context.Settings.MaxSecondsBetweenStops && walkingSpeedInKilometersPerHour < Context.Settings.MaxSpeed)
                {
                    walkingSpeedInKilometersPerHour++;
                    speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
                    seconds = distanceToTarget / speedInMetersPerSecond;
                }
            }

            LastKnownSpeed = walkingSpeedInKilometersPerHour;

            //log distance and time
            if (seconds > 60)
            {
                Logger.Write($"(NAVIGATION) Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.None, ConsoleColor.Red);
            }
            else
            {
                Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.Navigation);
            }
            var nextWaypointBearing = sourceLocation.DegreeBearing(targetLocation);
            var nextWaypointDistance = speedInMetersPerSecond;
            var waypoint = sourceLocation.CreateWaypoint(nextWaypointDistance, nextWaypointBearing, Convert.ToDouble(trk.Ele));

            //Initial walking

            var requestSendDateTime = DateTime.Now;
            var result =
                await
                    UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, waypoint.Altitude);

            do
            {
                var millisecondsUntilGetUpdatePlayerLocationResponse =
                    (DateTime.Now - requestSendDateTime).TotalMilliseconds;

                sourceLocation = new GeoCoordinate(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude);
                var currentDistanceToTarget = sourceLocation.CalculateDistanceInMeters(targetLocation);

                nextWaypointDistance = Math.Min(currentDistanceToTarget,
                    millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
                nextWaypointBearing = sourceLocation.DegreeBearing(targetLocation);
                waypoint = sourceLocation.CreateWaypoint(nextWaypointDistance, nextWaypointBearing);
                /* Added by Finfinder
                if (Context.Client.Settings.ShowDebugMessages)
                {
                    Logger.Write($"Distance to target - {currentDistanceToTarget} meters", LogLevel.Debug);
                }
                */

                requestSendDateTime = DateTime.Now;
                result =
                    await
                        UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude,
                            waypoint.Altitude);

                if (functionExecutedWhileWalking != null)
                    await functionExecutedWhileWalking();// look for pokemon
                await Task.Delay(500);
            } while (sourceLocation.CalculateDistanceInMeters(targetLocation) >= 13);

            return result;
        }

        public static FortData[] PathByNearestNeighbour(FortData[] pokeStops)
        {
            for (var i = 1; i < pokeStops.Length - 1; i++)
            {
                var closest = i + 1;
                var cloestDist = CalculateDistanceInMeters(pokeStops[i].Latitude, pokeStops[i].Longitude, pokeStops[closest].Latitude, pokeStops[closest].Longitude);
                for (var j = closest; j < pokeStops.Length; j++)
                {
                    var initialDist = cloestDist;
                    var newDist = CalculateDistanceInMeters(pokeStops[i].Latitude, pokeStops[i].Longitude, pokeStops[j].Latitude, pokeStops[j].Longitude);
                    if (initialDist > newDist)
                    {
                        cloestDist = newDist;
                        closest = j;
                    }

                }
                var tmpPok = pokeStops[closest];
                pokeStops[closest] = pokeStops[i + 1];
                pokeStops[i + 1] = tmpPok;
            }

            return pokeStops;
        }

        #endregion

    }
}
