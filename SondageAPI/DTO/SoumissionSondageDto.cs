using System.ComponentModel.DataAnnotations;

namespace SondageAPI.DTO;

/// <summary>
/// Corps de la requete POST /api/participations/{cle}/soumission. La cle de
/// participation est dans l'URL (elle identifie la ressource) : ce DTO ne
/// contient que les reponses.
/// </summary>
public class SoumissionSondageDto
{
    [Required(ErrorMessage = "La liste des reponses est requise.")]
    [MinLength(1, ErrorMessage = "Le sondage doit contenir au moins une reponse.")]
    public List<ReponseDto> Reponses { get; set; } = new();
}
