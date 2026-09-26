using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VideoTimelineApp.Models
{
    public class VideoSession
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; }

        /// <summary>GUID-based filename stored on disk (for legacy local videos)</summary>
        [MaxLength(255)]
        public string StoredFileName { get; set; }

        /// <summary>Direct CDN video URL on Cloudinary</summary>
        [MaxLength(1000)]
        public string VideoUrl { get; set; }

        /// <summary>Public ID on Cloudinary used for deletions and management</summary>
        [MaxLength(255)]
        public string PublicId { get; set; }

        /// <summary>Duration in seconds – updated by client via PATCH after the HTML5 video loads</summary>
        public double Duration { get; set; }

        /// <summary>File size in bytes</summary>
        public long FileSize { get; set; }

        public DateTime UploadedAt { get; set; }

        public virtual ICollection<Segment> Segments { get; set; }

        public VideoSession()
        {
            Segments    = new HashSet<Segment>();
            UploadedAt  = DateTime.UtcNow;
        }
    }
}
