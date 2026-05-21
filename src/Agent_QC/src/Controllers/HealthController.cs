using Microsoft.AspNetCore.Mvc;

namespace Agent_QC.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
