using Microsoft.AspNetCore.Mvc;
using SondageAPI.Services;

namespace SondageAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SondageController : ControllerBase
    {
        private readonly SondageService _service;

        public SondageController(SondageService service)
        {
            _service = service;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitSurvey([FromBody] Submission submission)
        {
            // Vérifie que le ID est fourni
            if (string.IsNullOrWhiteSpace(submission.UserId))
            {
                return BadRequest("UserId est requis pour garantir l'authenticité.");
            }

            // Vérifie si l'utilisateur a déjà voté
            if (await _service.HasUserSubmittedAsync(submission.UserId))
            {
                return Conflict("Cet utilisateur a déjà soumis le sondage.");
            }

            // Sauvegarde le JSON
            await _service.SaveSubmissionAsync(submission);
            return Ok("Sondage soumis avec succès.");
        }
    }
}