using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TwitchVor.Data.Models;

namespace TwitchVor.Data
{
    public class StreamDatabase
    {
        public const string UTFNoCase = "NOCASE2";

        readonly string path;

        public StreamDatabase(string path)
        {
            this.path = path;
        }

        public MyContext CreateContext()
        {
            return new MyContext(path);
        }

        public async Task InitAsync()
        {
            using var context = CreateContext();

            await context.Database.EnsureCreatedAsync();

            if (context.Database.GetDbConnection() is not SqliteConnection connection)
                throw new Exception("SqliteConnection null");

            connection.CreateCollation(UTFNoCase, (x, y) => string.Compare(x, y, ignoreCase: true));
        }

        public async Task DestroyAsync()
        {
            using var context = CreateContext();

            await context.Database.EnsureDeletedAsync();
        }

        public async Task<int> CountSegmentsAsync()
        {
            using var context = CreateContext();

            return await context.Segments.CountAsync();
        }

        public void AddVideoFormat(string format, DateTimeOffset date)
        {
            using var context = CreateContext();

            VideoFormatDb videoFormat = new()
            {
                Format = format,
                Date = date
            };

            context.VideoFormats.Add(videoFormat);

            context.SaveChanges();
        }

        public int AddSegment(int mediaSegmentNumber, DateTimeOffset programDate, long size, float duration)
        {
            using var context = CreateContext();

            SegmentDb segment = new()
            {
                MediaSegmentNumber = mediaSegmentNumber,
                ProgramDate = programDate,
                Size = size,
                Duration = duration
            };

            context.Segments.Add(segment);

            context.SaveChanges();

            return segment.Id;
        }

        public void AddSkip(DateTimeOffset startDate, DateTimeOffset endDate)
        {
            using var context = CreateContext();

            SkipDb segment = new()
            {
                StartDate = startDate,
                EndDate = endDate
            };

            context.Skips.Add(segment);

            context.SaveChanges();
        }

        public async Task AddSkipAsync(DateTimeOffset startDate, DateTimeOffset endDate)
        {
            using var context = CreateContext();

            SkipDb segment = new()
            {
                StartDate = startDate,
                EndDate = endDate
            };

            context.Skips.Add(segment);

            await context.SaveChangesAsync();
        }

        public async Task AddChatMessageAsync(string userId, string username, string? displayName, string message, string? color, string? badges, DateTimeOffset postTime)
        {
            using var context = CreateContext();

            ChatMessageDb chatMessage = new(default, userId, username, displayName, message, color, badges, postTime);

            context.ChatMessages.Add(chatMessage);

            await context.SaveChangesAsync();
        }

        public Task<VideoFormatDb[]> LoadAllVideoFormatsAsync()
        {
            using var context = CreateContext();

            return context.VideoFormats.OrderBy(s => s.Id).ToArrayAsync();
        }

        public SegmentDb[] LoadAllSegments()
        {
            using var context = CreateContext();

            return context.Segments.OrderBy(s => s.Id).ToArray();
        }

        public async Task<SegmentDb[]> LoadSegmentsAsync(int take, int skip)
        {
            using var context = CreateContext();

            return await context.Segments.OrderBy(s => s.Id)
                                         .Skip(skip)
                                         .Take(take)
                                         .ToArrayAsync();
        }

        public async Task<SkipDb[]> LoadSkipsAsync()
        {
            using var context = CreateContext();

            return await context.Skips.OrderBy(s => s.Id).ToArrayAsync();
        }

        /// <summary>
        /// Сумма размеров сегментов до указанного айди.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<long> CalculateOffsetAsync(int id)
        {
            using var context = CreateContext();

            return await context.Segments.Where(segment => segment.Id < id)
                                         .SumAsync(s => s.Size);
        }

        public async Task<long> CalculateSizeAsync(int startId, int endId)
        {
            using var context = CreateContext();

            return await context.Segments.Where(segment => segment.Id >= startId && segment.Id <= endId)
                                         .SumAsync(s => s.Size);
        }
    }
}