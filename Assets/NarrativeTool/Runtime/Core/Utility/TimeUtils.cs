using System;

namespace NarrativeTool.Core.Utility
{
    public static class TimeUtils
    {
        public static string RelativeTime(DateTime time)
        {
            if (time == DateTime.MinValue) return "Unknown";

            var span = DateTime.Now - time;
            if (span.TotalSeconds < 0) span = TimeSpan.Zero;

            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 2) return "1 minute ago";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
            if (span.TotalHours < 2) return "1 hour ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
            if (span.TotalDays < 2) return "Yesterday";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
            if (span.TotalDays < 14) return "1 week ago";
            if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)} weeks ago";
            if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} months ago";
            return $"{(int)(span.TotalDays / 365)} years ago";
        }
    }
}