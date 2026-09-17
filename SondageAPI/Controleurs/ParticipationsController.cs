using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SondageAPI.DTO;
using SondageAPI.Services;

namespace SondageAPI.Controleurs
{
    /// <summary>
    /// Cycle de vie d'une participation : emission d'une cle a usage unique,
    /// consultation de son statut, et soumission des reponses. Toutes les
    /// routes exigent la cle d'API systeme (en-tete X-API-KEY).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ParticipationsController : ControllerBase
    {
        private readonly ServiceParticipation _serviceParticipation;

        public ParticipationsController(ServiceParticipation serviceParticipation)
        {
            _serviceParticipation = serviceParticipation;
        }

        /// <summary>
        /// Emet une nouvelle participation (cle a usage unique) pour ce
        /// sondage. A appeler une fois par repondant avant qu'il ne remplisse
        /// le formulaire.
        /// </summary>
        [HttpPost("{sondageId}")]
        [ProducesResponseType(typeof(CreationSondageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErreurDto), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CreationSondageDto>> CreerParticipation(string sondageId)
        {
            var participant = await _serviceParticipation.CreerParticipationAsync(sondageId);
            if (participant is null)
            {
                return NotFound(new ErreurDto { Erreur = $"Sondage '{sondageId}' introuvable." });
            }

            var dto = new CreationSondageDto
            {
                Cle = participant.Cle,
                SondageId = participant.SondageId,
                DateEmission = participant.DateEmission
            };

            return CreatedAtAction(nameof(ObtenirStatutParticipation), new { cle = dto.Cle }, dto);
        }

        /// <summary>
        /// Consulte l'etat d'une participation (existe / deja utilisee /
        /// pour quel sondage). Pratique pour verifier vos scenarios dans
        /// Postman.
        /// </summary>
        [HttpGet("{cle}")]
        [ProducesResponseType(typeof(StatutParticipationDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<StatutParticipationDto>> ObtenirStatutParticipation(string cle)
        {
            var statut = await _serviceParticipation.ObtenirStatutAsync(cle);
            return Ok(new StatutParticipationDto
            {
                Cle = cle,
                Existe = statut.Existe,
                EstUtilisee = statut.EstUtilisee,
                SondageId = statut.SondageId
            });
        }

        /// <summary>
        /// Soumet les reponses d'une participation. La cle identifie a elle
        /// seule le sondage concerne : impossible de la faire servir pour
        /// un autre sondage que celui pour lequel elle a ete emise.
        /// </summary>
        [HttpPost("{cle}/soumission")]
        [ProducesResponseType(typeof(ConfirmationSoumissionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErreurDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErreurDto), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErreurDto), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SoumettreParticipation(string cle, [FromBody] SoumissionSondageDto dto)
        {
            var (resultat, participation, erreurs) = await _serviceParticipation.SoumettreAsync(cle, dto);

            return resultat switch
            {
                ResultatSoumission.Succes => Ok(new ConfirmationSoumissionDto
                {
                    Succes = true,
                    Message = "Sondage soumis avec succes.",
                    DateSoumission = participation!.DateSoumission
                }),
                ResultatSoumission.ReponsesInvalides => BadRequest(new ErreurDto { Erreur = string.Join(" ", erreurs) }),
                ResultatSoumission.DejaSoumise => Conflict(new ErreurDto { Erreur = "Cette participation a deja ete soumise." }),
                ResultatSoumission.CleInvalide => Unauthorized(new ErreurDto { Erreur = "Cle de participation invalide." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
