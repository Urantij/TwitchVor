namespace TwitchVor.Vvideo.Money;

public class TimeBasedPricer : IPricer
{
    readonly DateTimeOffset creationDate;
    readonly Bill perHourBill;

    public TimeBasedPricer(DateTimeOffset creationDate, Bill perHourBill)
    {
        this.creationDate = creationDate;
        this.perHourBill = perHourBill;
    }

    public Bill GetCost(DateTimeOffset currentDate)
    {
        int hours = (int)Math.Ceiling((currentDate - creationDate).TotalHours);

        decimal count = perHourBill.count * hours;

        return new Bill(perHourBill.currency, count);
    }
}