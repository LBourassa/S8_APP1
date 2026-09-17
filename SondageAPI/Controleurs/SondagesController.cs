using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SondageAPI.DTO;
using SondageAPI.Services;

namespace SondageAPI.Controleurs
{
    /// <summary>
    /// Catalogue des sondages disponibles. Toutes les routes exigent la cle
    /// d'API systeme (en-tete X-API-KEY), verifiee par ServiceAuthentification
    /// via [Authorize].
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class SondagesController : ControllerBase
    {
        private readonly ServiceSondage _serviceSondage;

        public SondagesController(ServiceSondage serviceSondage)
        {
            _serviceSondage = serviceSondage;
        }

        /// <summary>Liste des sondages disponibles.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SondageResumeDto>), StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<SondageResumeDto>> ObtenirSondages()
        {
            var sondages = _serviceSondage.ObtenirTous()
                .Select(s => new SondageResumeDto { Id = s.Id, Titre = s.Titre, NombreQuestions = s.Questions.Count });

            return Ok(sondages);
        }

        /// <summary>Detail d'un sondage (questions/options), pour savoir quoi soumettre.</summary>
        [HttpGet("{sondageId}")]
        [ProducesResponseType(typeof(SondageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErreurDto), StatusCodes.Status404NotFound)]
        public ActionResult<SondageDto> ObtenirSondage(string sondageId)
        {
            var sondage = _serviceSondage.ObtenirParId(sondageId);
            if (sondage is null)
            {
                return NotFound(new ErreurDto { Erreur = $"Sondage '{sondageId}' introuvable." });
            }

            return Ok(new SondageDto
            {
                Id = sondage.Id,
                Titre = sondage.Titre,
                Questions = sondage.Questions.Select(q => new QuestionDto
                {
                    Id = q.Id,
                    Texte = q.Texte,
                    Options = q.Options.Select(o => new OptionDto { Cle = o.Cle, Texte = o.Texte }).ToList()
                }).ToList()
            });
        }
    }
}
