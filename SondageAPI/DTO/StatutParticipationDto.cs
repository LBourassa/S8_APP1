namespace SondageAPI.DTO;

public class StatutParticipationDto
{
    public string Cle { get; set; } = string.Empty;
    public bool Existe { get; set; }
    public bool EstUtilisee { get; set; }
    public string? SondageId { get; set; }
}
