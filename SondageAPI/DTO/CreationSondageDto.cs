namespace SondageAPI.DTO;

/// <summary>
/// Retourne au client apres l'emission d'une nouvelle participation (une
/// cle a usage unique) pour un sondage precis.
/// </summary>
public class CreationSondageDto
{
    public string Cle { get; set; } = string.Empty;
    public string SondageId { get; set; } = string.Empty;
    public DateTime DateEmission { get; set; }
}
