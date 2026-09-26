using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoTimelineApp.Models
{
    public class Segment
    {
        public int Id { get; set; }

        public int VideoSessionId { get; set; }

        [ForeignKey("VideoSessionId")]
        public virtual VideoSession VideoSession { get; set; }

        [MaxLength(100)]
        public string Label { get; set; }

        /// <summary>Start time in seconds</summary>
        public double StartTime { get; set; }

        /// <summary>End time in seconds</summary>
        public double EndTime { get; set; }

        /// <summary>"normal" or "incident"</summary>
        [MaxLength(20)]
        public string Type { get; set; }

        /// <summary>Hex colour string, e.g. "#38bdf8"</summary>
        [MaxLength(20)]
        public string Color { get; set; }

        public DateTime CreatedAt { get; set; }

        [NotMapped]
        public double Duration { get { return EndTime - StartTime; } }

        public Segment()
        {
            Type      = "normal";
            Color     = "#38bdf8";
            Label     = string.Empty;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
