using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Vvideo.Money;

public class Bill
{
    public readonly Currency currency;
    public readonly decimal count;

    public Bill(Currency currency, decimal count)
    {
        this.currency = currency;
        this.count = count;
    }

    public string Format()
    {
        System.Globalization.CultureInfo cultureInfo = currency switch
        {
            Currency.RUB => System.Globalization.CultureInfo.GetCultureInfo("ru-ru"),
            Currency.USD => System.Globalization.CultureInfo.GetCultureInfo("en-us"),
            _ => System.Globalization.CultureInfo.CurrentCulture
        };

        return count.ToString("C", cultureInfo);
    }
}