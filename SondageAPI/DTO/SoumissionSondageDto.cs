using System.ComponentModel.DataAnnotations;

namespace SondageAPI.DTO;

/// <summary>
/// Corps de la requete POST /api/participations/{cle}/soumission. La cle de
/// participation est dans l'URL (elle identifie la ressource), mais doit
/// aussi etre repetee ici : le controleur valide que les deux correspondent
/// avant tout traitement, pour detecter une URL et un corps de requete
/// incoherents.
/// </summary>
public class SoumissionSondageDto
{
    [Required(ErrorMessage = "Cle est requise.")]
    public string Cle { get; set; } = string.Empty;

    [Required(ErrorMessage = "La liste des reponses est requise.")]
    [MinLength(1, ErrorMessage = "Le sondage doit contenir au moins une reponse.")]
    public List<ReponseDto> Reponses { get; set; } = new();
}
