namespace SondageAPI.Modeles;

/// <summary>
/// L'enregistrement persiste d'une participation completee (un participant
/// ayant soumis ses reponses a un sondage).
/// </summary>
public class Participation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SondageId { get; set; } = string.Empty;
    public string ParticipantCle { get; set; } = string.Empty;
    public List<Reponse> Reponses { get; set; } = new();
    public DateTime DateSoumission { get; set; }
}
