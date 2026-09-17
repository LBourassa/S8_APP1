using System.ComponentModel.DataAnnotations;

namespace SondageAPI.DTO;

/// <summary>Une reponse individuelle a une question du sondage.</summary>
public class ReponseDto
{
    [Required(ErrorMessage = "QuestionId est requis.")]
    public string QuestionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Valeur est requise.")]
    public string Valeur { get; set; } = string.Empty;
}
