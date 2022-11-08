using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Vvideo.Money;

public interface IPricer
{
    public Bill GetCost(DateTimeOffset currentTime);
}
