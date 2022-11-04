using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Utility;

namespace TwitchVor.Utility
{
    public static class Greater
    {
        public static void Great()
        {
            ConsoleColor[] colors = new ConsoleColor[]
            {
                ConsoleColor.Red,
                ConsoleColor.DarkYellow,
                ConsoleColor.Yellow,
                ConsoleColor.Green,
                ConsoleColor.Blue,
                ConsoleColor.DarkBlue,
                ConsoleColor.Magenta,
            };

            foreach (var color in colors)
            {
                ColorLog.Log("Ты пидор.", null, color);
            }
        }
    }
}