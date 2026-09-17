namespace SondageAPI.Modeles;

/// <summary>
/// Un choix de reponse pour une question (ex. Cle="a", Texte="0-25 ans").
/// </summary>
public class Option
{
    public string Cle { get; set; } = string.Empty;
    public string Texte { get; set; } = string.Empty;
}
