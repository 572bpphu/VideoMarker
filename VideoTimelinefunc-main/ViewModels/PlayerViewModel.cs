using VideoTimelineApp.Models;

namespace VideoTimelineApp.ViewModels
{
    public class PlayerViewModel
    {
        public VideoSession Session  { get; set; }
        /// <summary>Relative URL to the video file, e.g. "/uploads/videos/abc123.mp4"</summary>
        public string       VideoUrl { get; set; }
    }
}
