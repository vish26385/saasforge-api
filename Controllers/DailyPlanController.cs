using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Plan;
using SaaSForge.Api.Helpers;
using SaaSForge.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace SaaSForge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DailyPlanController : ControllerBase
    {

        private readonly FlowOSContext _context;
        public DailyPlanController(FlowOSContext context)
        {
            _context = context;
        }

        [HttpGet("{date}")]
        public async Task<IActionResult> GetPlan(string date)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirst("id")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            // ✅ DateOnly key (IST calendar date)
            if (!DateOnly.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var planDate))
            {
                return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd" });
            }

            var plan = await _context.DailyPlans
                .AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId &&
                    p.Date == planDate);

            if (plan == null) return NotFound();

            var dto = new PlanResponseDto
            {
                PlanId = plan.Id,
                Date = plan.Date.ToString("yyyy-MM-dd"),
                Focus = plan.Focus ?? "",
                Timeline = (plan.Items ?? new List<DailyPlanItem>())
                    .OrderBy(i => i.Start)
                    .Select(i => new PlanItemDto
                    {
                        ItemId = i.Id,
                        TaskId = i.TaskId,
                        Label = i.Label,
                        Start = i.Start,
                        End = i.End,
                        Confidence = i.Confidence,
                        NudgeAt = i.NudgeAt
                    })
                    .ToList(),
                CarryOver = new List<int>(),
                RawJson = null,
                PrettyJson = plan.PlanJsonClean
            };

            return Ok(dto);
        }

        [HttpPost("save")]
        public async Task<IActionResult> SavePlan([FromBody] DailyPlan model)
        {
            var userId = User.FindFirst("id")?.Value;
            if (userId == null) return Unauthorized();
            model.UserId = userId;
            _context.DailyPlans.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }
    }
}
