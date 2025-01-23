using System.ComponentModel.DataAnnotations;

namespace TwitchVor.Data.Models
{
    public class SegmentDb
    {
        [Key] public int Id { get; set; }

        /// <summary>
        /// В теории может повторяться, ведь при переподрубе он обнуляется
        /// </summary>
        public int MediaSegmentNumber { get; set; }

        /// <summary>
        /// Абсолютное время
        /// </summary>
        [Required]
        public DateTimeOffset ProgramDate { get; set; }

        /// <summary>
        /// В байтах
        /// </summary>
        [Required]
        public long Size { get; set; }

        /// <summary>
        /// В секундах
        /// </summary>
        [Required]
        public float Duration { get; set; }
    }
}