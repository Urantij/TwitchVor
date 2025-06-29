using System.ComponentModel.DataAnnotations;

namespace TwitchVor.Data.Models;

/// <summary>
/// Информация об инит части сегмента
/// </summary>
public class MapDb
{
    [Key] public int Id { get; set; }

    [Required] public long Size { get; set; }

    public MapDb(long size)
    {
        Size = size;
    }
}