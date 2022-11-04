using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Data.Models
{
    public class VideoFormatDb
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 720p60
        /// </summary>
        [Required]
        public string Format { get; set; }

        [Required]
        public DateTimeOffset Date { get; set; }
    }
}