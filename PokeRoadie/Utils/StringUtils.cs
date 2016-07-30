#region " Imports "

using System;
using System.Collections.Generic;
using System.Linq;

using POGOProtos.Inventory.Item;

#endregion


namespace PokeRoadie
{
    public static class StringUtils
    {
        public static string GetSummedFriendlyNameOfItemAwardList(IEnumerable<ItemAward> items)
        {
            var enumerable = items as IList<ItemAward> ?? items.ToList();

            if (!enumerable.Any())
                return string.Empty;

            return
                enumerable.GroupBy(i => i.ItemId)
                          .Select(kvp => new { ItemName = kvp.Key.ToString(), Amount = kvp.Sum(x => x.ItemCount) })
                          .Select(y => $"{y.Amount} x {y.ItemName}")
                          .Aggregate((a, b) => $"{a}, {b}");
        }
        public static string GetSecondsDisplay(double seconds)
        {
            if (seconds > 60)
            {
                var minutes = seconds / 60;
                if (minutes > 60)
                {
                    var hours = minutes / 60;
                    if (hours > 24)
                    {
                        var days = hours / 24;
                        return $"{Math.Round(days, 2).ToString():0.##} days";
                    }
                    else
                    {
                        return $"{Math.Round(hours, 2).ToString():0.##} hours";
                    }
                }
                else
                {
                    return $"{Math.Round(minutes, 2).ToString():0.##} minutes";
                }

            }
            else
            {
                return $"{Math.Round(seconds, 2).ToString():0.##} seconds";
            }
        }

        public static string GetTravelActionString(double speed, bool flyingEnabled)
        {
            var action = "rocking";
            if (speed < 1)
                action = "standing";
            else if (speed < 5)
                action = "crawling";
            else if (speed < 10)
                action = "walking";
            else if (speed < 15)
                action = "jogging";
            else if (speed < 25)
                action = "running";
            else if (speed < 80)
                action = "driving";
            else
                action = flyingEnabled ? "flying" : "driving mad";
            return action;
        }
    }
}
