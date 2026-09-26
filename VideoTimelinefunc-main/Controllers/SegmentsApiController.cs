using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VideoTimelineApp.Data;
using VideoTimelineApp.Models;

namespace VideoTimelineApp.Controllers
{
    /// <summary>
    /// REST API for Segments.
    /// All routes are prefixed with /api/segments
    /// </summary>
    [RoutePrefix("api/segments")]
    public class SegmentsApiController : ApiController
    {
        private readonly AppDbContext _db = new AppDbContext();

        // ──────────────────────────────────────────────────────────────────
        // GET  api/segments/{videoId}
        // Returns all segments for a video, ordered by start time
        // ──────────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("{videoId:int}")]
        public async Task<IHttpActionResult> GetByVideo(int videoId)
        {
            var segments = await _db.Segments
                .Where(s => s.VideoSessionId == videoId)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.Id,
                    s.Label,
                    s.StartTime,
                    s.EndTime,
                    duration = s.EndTime - s.StartTime,
                    s.Type,
                    s.Color
                })
                .ToListAsync();

            return Ok(segments);
        }

        // ──────────────────────────────────────────────────────────────────
        // POST  api/segments
        // Creates a new segment (validates overlap on server side too)
        // ──────────────────────────────────────────────────────────────────
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Create([FromBody] CreateSegmentDto dto)
        {
            if (dto == null)
                return BadRequest("Payload không hợp lệ.");

            if (dto.EndTime <= dto.StartTime)
                return BadRequest("Điểm cuối phải lớn hơn điểm đầu.");

            string segType = dto.Type ?? "normal";

            // Server-side overlap check: chỉ cấm đè giữa các thao tác CÙNG LOẠI
            bool overlap = await _db.Segments.AnyAsync(s =>
                s.VideoSessionId == dto.VideoSessionId &&
                s.Type == segType &&
                s.StartTime < dto.EndTime &&
                s.EndTime   > dto.StartTime);

            if (overlap)
                return Content(HttpStatusCode.Conflict,
                    new { message = "Thao tác này đè lên một thao tác khác cùng loại đã có!" });

            var seg = new Segment
            {
                VideoSessionId = dto.VideoSessionId,
                Label          = dto.Label    ?? string.Empty,
                StartTime      = dto.StartTime,
                EndTime        = dto.EndTime,
                Type           = segType,
                Color          = dto.Color    ?? "#38bdf8",
                CreatedAt      = DateTime.UtcNow
            };

            _db.Segments.Add(seg);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                seg.Id,
                seg.Label,
                seg.StartTime,
                seg.EndTime,
                duration = seg.EndTime - seg.StartTime,
                seg.Type,
                seg.Color
            });
        }

        // ──────────────────────────────────────────────────────────────────
        // PUT  api/segments/{id}
        // Updates segment label, startTime, and/or endTime
        // ──────────────────────────────────────────────────────────────────
        [HttpPut]
        [Route("{id:int}")]
        public async Task<IHttpActionResult> UpdateSegment(int id, [FromBody] UpdateLabelDto dto)
        {
            var seg = await _db.Segments.FindAsync(id);
            if (seg == null) return NotFound();

            if (dto != null)
            {
                if (dto.Label != null) seg.Label = dto.Label;
                if (!string.IsNullOrWhiteSpace(dto.Color)) seg.Color = dto.Color.Trim();
                if (dto.StartTime.HasValue || dto.EndTime.HasValue)
                {
                    double newStart = dto.StartTime ?? seg.StartTime;
                    double newEnd = dto.EndTime ?? seg.EndTime;
                    if (newEnd <= newStart)
                        return BadRequest("Điểm cuối phải lớn hơn điểm đầu.");

                    bool overlap = await _db.Segments.AnyAsync(s =>
                        s.Id != id &&
                        s.VideoSessionId == seg.VideoSessionId &&
                        s.Type == seg.Type &&
                        s.StartTime < newEnd &&
                        s.EndTime   > newStart);

                    if (overlap)
                        return Content(HttpStatusCode.Conflict,
                            new { message = "Khoảng thời gian này đè lên một thao tác khác cùng loại!" });

                    seg.StartTime = newStart;
                    seg.EndTime = newEnd;
                }
            }
            await _db.SaveChangesAsync();

            return Ok(new
            {
                seg.Id,
                seg.Label,
                seg.StartTime,
                seg.EndTime,
                duration = seg.EndTime - seg.StartTime,
                seg.Type,
                seg.Color
            });
        }

        // ──────────────────────────────────────────────────────────────────
        // DELETE  api/segments/{id}
        // Removes a segment
        // ──────────────────────────────────────────────────────────────────
        [HttpDelete]
        [Route("{id:int}")]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var seg = await _db.Segments.FindAsync(id);
            if (seg == null) return NotFound();

            _db.Segments.Remove(seg);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xoá", id });
        }

        // ──────────────────────────────────────────────────────────────────
        // PATCH  api/segments/video/{videoId}/duration
        // Called by the client once the HTML5 video metadata is loaded
        // ──────────────────────────────────────────────────────────────────
        [HttpPut]
        [Route("video/{videoId:int}/duration")]
        public async Task<IHttpActionResult> UpdateDuration(int videoId, [FromBody] UpdateDurationDto dto)
        {
            var session = await _db.VideoSessions.FindAsync(videoId);
            if (session == null) return NotFound();

            if (dto != null && dto.Duration > 0)
                session.Duration = dto.Duration;

            await _db.SaveChangesAsync();
            return Ok(new { session.Id, session.Duration });
        }

        // ──────────────────────────────────────────────────────────────────
        // POST  api/segments/video/{videoId}/save-batch
        // Saves/replaces all segments for this video session in a single transaction
        // ──────────────────────────────────────────────────────────────────
        [HttpPost]
        [Route("video/{videoId:int}/save-batch")]
        public async Task<IHttpActionResult> SaveBatch(int videoId, [FromBody] BatchSaveSegmentsDto dto)
        {
            var session = await _db.VideoSessions.FindAsync(videoId);
            if (session == null) return NotFound();

            if (dto == null || dto.Segments == null)
                return BadRequest("Payload không hợp lệ.");

            // Validate endpoints
            foreach (var item in dto.Segments)
            {
                if (item.EndTime <= item.StartTime)
                    return BadRequest("Có thao tác với điểm cuối nhỏ hơn hoặc bằng điểm đầu.");
            }

            // Validate overlap among same-type segments
            var normalSegs = dto.Segments.Where(s => s.Type != "incident").OrderBy(s => s.StartTime).ToList();
            var incidentSegs = dto.Segments.Where(s => s.Type == "incident").OrderBy(s => s.StartTime).ToList();

            for (int i = 0; i < normalSegs.Count - 1; i++)
            {
                if (normalSegs[i].EndTime > normalSegs[i + 1].StartTime)
                    return Content(HttpStatusCode.Conflict, new { message = "Có các thao tác Đúng bị đè thời gian lên nhau." });
            }
            for (int i = 0; i < incidentSegs.Count - 1; i++)
            {
                if (incidentSegs[i].EndTime > incidentSegs[i + 1].StartTime)
                    return Content(HttpStatusCode.Conflict, new { message = "Có các thao tác Sai bị đè thời gian lên nhau." });
            }

            using (var tx = _db.Database.BeginTransaction())
            {
                try
                {
                    var existing = await _db.Segments.Where(s => s.VideoSessionId == videoId).ToListAsync();
                    _db.Segments.RemoveRange(existing);
                    await _db.SaveChangesAsync();

                    var toAdd = dto.Segments.Select(s => new Segment
                    {
                        VideoSessionId = videoId,
                        Label = s.Label ?? string.Empty,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Type = s.Type ?? "normal",
                        Color = s.Color ?? (s.Type == "incident" ? "#ef4444" : "#38bdf8"),
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    _db.Segments.AddRange(toAdd);
                    await _db.SaveChangesAsync();

                    tx.Commit();

                    var result = toAdd.OrderBy(s => s.StartTime).Select(s => new
                    {
                        s.Id,
                        s.Label,
                        s.StartTime,
                        s.EndTime,
                        duration = s.EndTime - s.StartTime,
                        s.Type,
                        s.Color
                    }).ToList();

                    return Ok(result);
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    return InternalServerError(ex);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────
    public class CreateSegmentDto
    {
        public int    VideoSessionId { get; set; }
        public string Label         { get; set; }
        public double StartTime     { get; set; }
        public double EndTime       { get; set; }
        public string Type          { get; set; }
        public string Color         { get; set; }
    }

    public class BatchSaveSegmentsDto
    {
        public System.Collections.Generic.List<CreateSegmentDto> Segments { get; set; }
    }

    public class UpdateLabelDto
    {
        public string Label { get; set; }
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }
        public string Color { get; set; }
    }

    public class UpdateDurationDto
    {
        public double Duration { get; set; }
    }
}
