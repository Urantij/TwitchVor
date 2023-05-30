using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchVor.Vvideo.Dota;

class DotaMatchTimestamp : BaseTimestamp
{
    public readonly string heroName;
    public readonly int partyCount;
    public readonly bool? win;

    public DotaMatchTimestamp(string heroName, int partyCount, bool? win, DateTime timestamp) : base(timestamp)
    {
        this.heroName = heroName;
        this.partyCount = partyCount;
        this.win = win;
    }

    public override string MakeString()
    {
        StringBuilder sb = new();

        sb.Append(heroName);

        if (win != null)
        {
            sb.Append(win == true ? " Вин" : " Луз");
        }

        if (partyCount > 1)
        {
            sb.Append($" (Пати {partyCount})");
        }

        return sb.ToString();
    }
}
