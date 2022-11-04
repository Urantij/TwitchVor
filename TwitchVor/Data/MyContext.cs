using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TwitchVor.Data.Models;

namespace TwitchVor.Data
{
    public class MyContext : DbContext
    {
        readonly string path;

#nullable disable

        public DbSet<SegmentDb> Segments { get; set; }
        public DbSet<VideoFormatDb> VideoFormats { get; set; }
        public DbSet<SkipDb> Skips { get; set; }

#nullable restore

        public MyContext(string path)
        {
            this.path = path;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={path};");
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<DateTimeOffset>()
                                .HaveConversion<DateTimeOffsetToBinaryConverter>();

            configurationBuilder.Properties<DateTimeOffset?>()
                                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}