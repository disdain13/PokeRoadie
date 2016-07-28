#region

using PokemonGo.RocketAPI.Logging;
using System;

#endregion

namespace PokemonGo.RocketAPI.Exceptions
{
    public class InvalidResponseException : Exception
    {
        public InvalidResponseException()
            :base()
        {
        }
        public InvalidResponseException(string message)
            :base(message)
        {
        }
    }
}