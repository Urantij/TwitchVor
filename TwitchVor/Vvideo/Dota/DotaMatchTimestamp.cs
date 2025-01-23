using System.Text;

namespace TwitchVor.Vvideo.Dota;

class DotaMatchTimestamp : BaseTimestamp
{
    public readonly string heroName;
    public readonly int partyCount;
    public readonly bool? win;

    private readonly bool spoilResults;

    public DotaMatchTimestamp(string heroName, int partyCount, bool? win, DateTime timestamp, bool spoilResults) :
        base(timestamp)
    {
        this.heroName = heroName;
        this.partyCount = partyCount;
        this.win = win;
        this.spoilResults = spoilResults;
    }

    public override string MakeString()
    {
        StringBuilder sb = new();

        sb.Append(heroName);

        if (spoilResults && win != null)
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