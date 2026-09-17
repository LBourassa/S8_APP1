namespace SondageAPI.Modeles;

/// <summary>
/// La reponse d'un participant a une question precise, au sein d'une Participation.
/// </summary>
public class Reponse
{
    public string QuestionId { get; set; } = string.Empty;
    public string Valeur { get; set; } = string.Empty;
}
