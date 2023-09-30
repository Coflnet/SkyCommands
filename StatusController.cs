using Coflnet.Sky.Commands;
using Microsoft.AspNetCore.Mvc;

namespace SkyCommands.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    [Route("users")]
    public IActionResult GetUserIds()
    {
        var userIds = SkyblockBackEnd.SubscribersReadOnly.Select(x => {
            try
            {
                return x.UserId;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }).Where(x => x != 0).ToList();
        return Ok(userIds);
    }
}