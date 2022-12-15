using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Data.Models;

public class ChatMessageDb
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }
    [Required]
    public string Username { get; set; }
    public string? DisplayName { get; set; }

    [Required]
    public string Message { get; set; }

    public string? Color { get; set; }
    public string? Badges { get; set; }

    [Required]
    public DateTimeOffset PostTime { get; set; }

    public ChatMessageDb(int id, string userId, string username, string? displayName, string message, string? color, string? badges, DateTimeOffset postTime)
    {
        Id = id;
        UserId = userId;
        Username = username;
        DisplayName = displayName;
        Message = message;
        Color = color;
        Badges = badges;
        PostTime = postTime;
    }
}
