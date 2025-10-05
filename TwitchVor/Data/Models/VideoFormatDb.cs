using System.ComponentModel.DataAnnotations;

namespace TwitchVor.Data.Models;

public class VideoFormatDb
{
    [Key] public int Id { get; set; }

    [Required] public int Width { get; set; }
    [Required] public int Height { get; set; }

    // TODO а вот это я не уверен, но надеюсь, всё нормально будет
    [Required] public int Fps { get; set; }

    public VideoFormatDb()
    {
    }

    public VideoFormatDb(int width, int height, int fps)
    {
        Width = width;
        Height = height;
        Fps = fps;
    }

    public IList<SegmentDb> Segments { get; set; }
}