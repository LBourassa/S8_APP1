namespace SondageAPI.DTO;

public class SondageResumeDto
{
    public string Id { get; set; } = string.Empty;
    public string Titre { get; set; } = string.Empty;
    public int NombreQuestions { get; set; }
}
