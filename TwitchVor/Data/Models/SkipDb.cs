using System.ComponentModel.DataAnnotations;

namespace TwitchVor.Data.Models;
// TODO. Была идея тут хранить не самое время, а сегменты.
// Но че то не знаю.

public class SkipDb
{
    [Key] public int Id { get; set; }

    /// <summary>
    /// Получается конец последнего видимого сегмента
    /// </summary>
    [Required]
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// Получается начало нового видимого сегмента.
    /// </summary>
    [Required]
    public DateTimeOffset EndDate { get; set; }
}