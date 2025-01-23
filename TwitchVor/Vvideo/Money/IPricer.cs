namespace TwitchVor.Vvideo.Money;

public interface IPricer
{
    public Bill GetCost(DateTimeOffset currentTime);
}