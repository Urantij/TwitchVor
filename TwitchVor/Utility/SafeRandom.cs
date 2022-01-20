using System;

namespace TwitchVor.Utility
{
    static class SafeRandom
    {
        static readonly Random random = new();

        public static double GetRoll()
        {
            lock (random)
            {
                return random.NextDouble();
            }
        }
    }
}