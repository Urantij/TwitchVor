using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pastel;
using TwitchVor.Utility;

namespace TwitchVor.Utility
{
    public static class Greater
    {
        public static void Great(ILoggerFactory loggerFactory)
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(Greater));

            string[] colors = new string[]
            {
                "#FF0000",
                "#FFA500",
                "#fdfd96",
                "#00FF00",
                "#87ceeb",
                "#0000FF",
                "#FF00FF",
            };

            foreach (var color in colors)
            {
                string value = "пидор".Pastel(color);

                logger.LogInformation("Я думаю, ты {value}.", value);
            }
        }
    }
}