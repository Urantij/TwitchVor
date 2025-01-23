using System.ComponentModel.DataAnnotations;

namespace TwitchVor.Data.Models
{
    public class VideoFormatDb
    {
        [Key] public int Id { get; set; }

        /// <summary>
        /// 1280x720:60
        /// </summary>
        [Required]
        public string Format { get; set; }

        [Required] public DateTimeOffset Date { get; set; }
    }
}