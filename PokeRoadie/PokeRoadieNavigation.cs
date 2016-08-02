#region " Imports "

using System;
using System.Threading.Tasks;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Helpers;

using POGOProtos.Networking.Responses;
using POGOProtos.Map.Fort;


#endregion


namespace PokeRoadie
{
    public class PokeRoadieNavigation
    {

        private const double SpeedDownTo = 10 / 3.6;
        private readonly Client _client;
        private DateTime? _lastSaveDate;

        public PokeRoadieNavigation(Client client)
        {
            _client = client;
        }

        private async Task<PlayerUpdateResponse> UpdatePlayerLocation(DestinationData destination)
        {
            return await UpdatePlayerLocation(destination.Latitude,destination.Longitude,destination.Altitude);
        }
        private async Task<PlayerUpdateResponse> UpdatePlayerLocation(GeoCoordinate geo)
        {
            return await UpdatePlayerLocation(geo.Latitude, geo.Longitude, geo.Altitude);
        }
        private async Task<PlayerUpdateResponse> UpdatePlayerLocation(double lat, double lng, double alt)
        {
            PokeRoadieSettings.Current.CurrentLatitude = lat;
            PokeRoadieSettings.Current.CurrentLongitude = lng;
            PokeRoadieSettings.Current.CurrentAltitude = alt;
            if (!_lastSaveDate.HasValue || _lastSaveDate.Value < DateTime.Now)
            {
                PokeRoadieSettings.Current.Save();
                _lastSaveDate = DateTime.Now.AddSeconds(10);
            }

            return await _client.Player.UpdatePlayerLocation(lat, lng, alt);

        }

        public async Task<PlayerUpdateResponse> HumanLikeWalkingGetCloser(GeoCoordinate targetLocation,
            double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking, double fraction)
        {

            //randomize speed for less detection
            if (PokeRoadieSettings.Current.EnableSpeedRandomizer)
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
            var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
            var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
            if (distanceToTarget > 4) distanceToTarget = distanceToTarget * fraction;
            var seconds = distanceToTarget / speedInMetersPerSecond;

            //adjust speed to try and keep the trip under a minute, might not be possible
            if (walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed && PokeRoadieSettings.Current.EnableSpeedAdjustment)
            {
                while (seconds > PokeRoadieSettings.Current.MaxSecondsBetweenStops && walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed)
                {
                    walkingSpeedInKilometersPerHour++;
                    speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
                    seconds = distanceToTarget / speedInMetersPerSecond;
                }
            }

            //log distance and time
            if (seconds > 60)
            {
                Logger.Write($"(NAVIGATION) Waypoint closer to target: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.None, ConsoleColor.Red);
            }
            else
            {
                Logger.Write($"Waypoint closer to target: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.Navigation);
            }

            var nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
            var waypoint = LocationUtils.CreateWaypoint(sourceLocation, distanceToTarget, nextWaypointBearing);

            return await HumanLikeWalking(waypoint, walkingSpeedInKilometersPerHour, functionExecutedWhileWalking);
        }

        //public async Task<PlayerUpdateResponse> HumanLikeWalking(GeoCoordinate targetLocation,
        //    double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking)
        //{

        //    //randomize speed for less detection
        //    if (PokeRoadieSettings.Current.EnableSpeedRandomizer)
        //    {
        //        if (walkingSpeedInKilometersPerHour > 3)
        //        {
        //            walkingSpeedInKilometersPerHour += RandomHelper.RandomNumber(-2, 3);
        //        }
        //        else
        //        {
        //            walkingSpeedInKilometersPerHour += RandomHelper.RandomNumber(0, 2);
        //        }
        //    }

        //    var speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
        //    var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
        //    var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
        //    var seconds = distanceToTarget / speedInMetersPerSecond;
        //    //adjust speed to try and keep the trip under a minute, might not be possible
        //    if (walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed && PokeRoadieSettings.Current.EnableSpeedAdjustment)
        //    {
        //        while (seconds > PokeRoadieSettings.Current.MaxSecondsBetweenStops && walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed)
        //        {
        //            walkingSpeedInKilometersPerHour++;
        //            speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
        //            seconds = distanceToTarget / speedInMetersPerSecond;
        //        }
        //    }

        //    //log distance and time
        //    if (seconds > 60)
        //    {
        //        Logger.Write($"(NAVIGATION) Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour,PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.None, ConsoleColor.Red);
        //    } 
        //    else
        //    {
        //        Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.Navigation);
        //    }

        //    var nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
        //    var nextWaypointDistance = speedInMetersPerSecond;
        //    var waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

        //    //Initial walking
        //    var requestSendDateTime = DateTime.Now;
        //    var result =
        //        await
        //            UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, PokeRoadieSettings.Current.CurrentAltitude);
        //    do
        //    {
        //        var millisecondsUntilGetUpdatePlayerLocationResponse =
        //            (DateTime.Now - requestSendDateTime).TotalMilliseconds;

        //        sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
        //        var currentDistanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);

        //        if (currentDistanceToTarget < 30 && speedInMetersPerSecond > SpeedDownTo)
        //        {
                    
        //            //Logger.Write($"We are within 40 meters of the target. Speeding down to 10 km/h to not pass the target.", LogLevel.Navigation);
        //            speedInMetersPerSecond = SpeedDownTo;
        //        }

        //        nextWaypointDistance = Math.Min(currentDistanceToTarget,
        //            millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
        //        nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
        //        waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

        //        requestSendDateTime = DateTime.Now;
        //        result =
        //            await
        //                UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude,
        //                    PokeRoadieSettings.Current.CurrentAltitude);
        //        if (functionExecutedWhileWalking != null)
        //            await functionExecutedWhileWalking();// look for pokemon
        //        await Task.Delay(Math.Min((int)(distanceToTarget / speedInMetersPerSecond * 100) , 1000));
        //    } while (LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation) >= 30);

        //    return result;
        //}

        //public async Task<PlayerUpdateResponse> HumanPathWalking(GpxReader.Trkpt trk,
        //    double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking)
        //{
        //    //PlayerUpdateResponse result = null;

        //    var targetLocation = new GeoCoordinate(Convert.ToDouble(trk.Lat), Convert.ToDouble(trk.Lon));

        //    var speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;

        //    var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
        //    var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
        //    // Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {distanceToTarget/speedInMetersPerSecond:0.##} seconds!", LogLevel.Info);

        //    var nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
        //    var nextWaypointDistance = speedInMetersPerSecond;
        //    var waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing, Convert.ToDouble(trk.Ele));

        //    //Initial walking

        //    var requestSendDateTime = DateTime.Now;
        //    var result =
        //        await
        //            UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, waypoint.Altitude);

        //    do
        //    {
        //        var millisecondsUntilGetUpdatePlayerLocationResponse =
        //            (DateTime.Now - requestSendDateTime).TotalMilliseconds;

        //        sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
        //        var currentDistanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);

        //        //if (currentDistanceToTarget < 40)
        //        //{
        //        //    if (speedInMetersPerSecond > SpeedDownTo)
        //        //    {
        //        //        //Logger.Write("We are within 40 meters of the target. Speeding down to 10 km/h to not pass the target.", LogLevel.Info);
        //        //        speedInMetersPerSecond = SpeedDownTo;
        //        //    }
        //        //}

        //        nextWaypointDistance = Math.Min(currentDistanceToTarget,
        //            millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
        //        nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
        //        waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

        //        requestSendDateTime = DateTime.Now;
        //        result =
        //            await
        //                UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude,
        //                    waypoint.Altitude);
        //        if (functionExecutedWhileWalking != null)
        //            await functionExecutedWhileWalking();// look for pokemon
        //        await Task.Delay(Math.Min((int)(distanceToTarget / speedInMetersPerSecond * 1000), 3000));
        //    } while (LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation) >= 30);

        //    return result;
        //}

        public async Task<PlayerUpdateResponse> HumanLikeWalking(GeoCoordinate targetLocation,
           double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking)
        {

            //randomize speed for less detection
            if (PokeRoadieSettings.Current.EnableSpeedRandomizer)
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

            var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
            var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
            var seconds = distanceToTarget / speedInMetersPerSecond;
            //adjust speed to try and keep the trip under a minute, might not be possible
            if (walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed && PokeRoadieSettings.Current.EnableSpeedAdjustment)
            {
                while (seconds > PokeRoadieSettings.Current.MaxSecondsBetweenStops && walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed)
                {
                    walkingSpeedInKilometersPerHour++;
                    speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
                    seconds = distanceToTarget / speedInMetersPerSecond;
                }
            }

            //log distance and time
            if (seconds > 60)
            {
                Logger.Write($"(NAVIGATION) Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.None, ConsoleColor.Red);
            }
            else
            {
                Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.Navigation);
            }
            var nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
            var nextWaypointDistance = speedInMetersPerSecond;
            var waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

            //Initial walking
            var requestSendDateTime = DateTime.Now;
            var result =
                await
                    UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, _client.Settings.DefaultAltitude);
            do
            {
                var millisecondsUntilGetUpdatePlayerLocationResponse =
                    (DateTime.Now - requestSendDateTime).TotalMilliseconds;

                sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
                var currentDistanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
                //if (_client.Settings.DebugMode)
                //Logger.Write($"Distance to target location: {currentDistanceToTarget:0.##} meters. Will take {currentDistanceToTarget / speedInMetersPerSecond:0.##} seconds!", LogLevel.Navigation);

                if (currentDistanceToTarget < 30 && speedInMetersPerSecond > SpeedDownTo)
                {
                    //if (_client.Settings.DebugMode)
                    //Logger.Write($"We are within 30 meters of the target. Speeding down to 10 km/h to not pass the target.", LogLevel.Navigation);
                    speedInMetersPerSecond = SpeedDownTo;
                }

                nextWaypointDistance = Math.Min(currentDistanceToTarget,
                    millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
                nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
                waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

                requestSendDateTime = DateTime.Now;
                result =
                    await
                        UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude,
                            _client.Settings.DefaultAltitude);

                if (functionExecutedWhileWalking != null)
                    await functionExecutedWhileWalking();// look for pokemon
                await Task.Delay(500);
            } while (LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation) >= 30);

            return result;
        }

        public async Task<PlayerUpdateResponse> HumanPathWalking(GpxReader.Trkpt trk,
            double walkingSpeedInKilometersPerHour, Func<Task> functionExecutedWhileWalking)
        {
            var targetLocation = new GeoCoordinate(Convert.ToDouble(trk.Lat), Convert.ToDouble(trk.Lon));

            var speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;

            var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
            var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
            Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {distanceToTarget / speedInMetersPerSecond:0.##} seconds!", LogLevel.Navigation);
            var seconds = distanceToTarget / speedInMetersPerSecond;
            //adjust speed to try and keep the trip under a minute, might not be possible
            if (walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed && PokeRoadieSettings.Current.EnableSpeedAdjustment)
            {
                while (seconds > PokeRoadieSettings.Current.MaxSecondsBetweenStops && walkingSpeedInKilometersPerHour < PokeRoadieSettings.Current.MaxSpeed)
                {
                    walkingSpeedInKilometersPerHour++;
                    speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;
                    seconds = distanceToTarget / speedInMetersPerSecond;
                }
            }

            //log distance and time
            if (seconds > 60)
            {
                Logger.Write($"(NAVIGATION) Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.None, ConsoleColor.Red);
            }
            else
            {
                Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(walkingSpeedInKilometersPerHour, PokeRoadieSettings.Current.FlyingEnabled)} at {walkingSpeedInKilometersPerHour}kmh", LogLevel.Navigation);
            }
            var nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
            var nextWaypointDistance = speedInMetersPerSecond;
            var waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing, Convert.ToDouble(trk.Ele));

            //Initial walking

            var requestSendDateTime = DateTime.Now;
            var result =
                await
                    UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, waypoint.Altitude);

            do
            {
                var millisecondsUntilGetUpdatePlayerLocationResponse =
                    (DateTime.Now - requestSendDateTime).TotalMilliseconds;

                sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
                var currentDistanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
                //if (_client.Settings.DebugMode)
                    //Logger.Write($"Distance to target location: {currentDistanceToTarget:0.##} meters. Will take {currentDistanceToTarget / speedInMetersPerSecond:0.##} seconds!", LogLevel.Navigation);

                /*
                if (currentDistanceToTarget < 40)
                {
                    if (speedInMetersPerSecond > SpeedDownTo)
                    {
                        Logger.Write("We are within 40 meters of the target. Speeding down to 10 km/h to not pass the target.", LogLevel.Info);
                        speedInMetersPerSecond = SpeedDownTo;
                    }
                }
                */

                nextWaypointDistance = Math.Min(currentDistanceToTarget,
                    millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
                nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
                waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

                requestSendDateTime = DateTime.Now;
                result =
                    await
                        UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude,
                            waypoint.Altitude);

                if (functionExecutedWhileWalking != null)
                    await functionExecutedWhileWalking();// look for pokemon
                await Task.Delay(500);
            } while (LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation) >= 30);

            return result;
        }

        public static FortData[] pathByNearestNeighbour(FortData[] pokeStops)
        {
            for (var i = 1; i < pokeStops.Length - 1; i++)
            {
                var closest = i + 1;
                var cloestDist = LocationUtils.CalculateDistanceInMeters(pokeStops[i].Latitude, pokeStops[i].Longitude, pokeStops[closest].Latitude, pokeStops[closest].Longitude);
                for (var j = closest; j < pokeStops.Length; j++)
                {
                    var initialDist = cloestDist;
                    var newDist = LocationUtils.CalculateDistanceInMeters(pokeStops[i].Latitude, pokeStops[i].Longitude, pokeStops[j].Latitude, pokeStops[j].Longitude);
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
    }
}