using System.Security.Cryptography;
using SondageAPI.DTO;
using SondageAPI.Modeles;
using SondageAPI.Repositories;

namespace SondageAPI.Services;

public enum ResultatSoumission
{
    Succes,
    CleInvalide,
    DejaSoumise,
    ReponsesInvalides
}

/// <summary>
/// Orchestre le cycle de vie d'une participation : emission d'une cle,
/// consultation de son statut, et soumission des reponses. Regroupe ce qui
/// etait auparavant deux services distincts (cles + soumission) car les
/// deux operations portent sur le meme concept metier -- la participation
/// d'une personne a un sondage.
/// </summary>
public class ServiceParticipation
{
    private readonly ParticipantRepository _participantRepository;
    private readonly ParticipationRepository _participationRepository;
    private readonly ServiceSondage _serviceSondage;

    public ServiceParticipation(
        ParticipantRepository participantRepository,
        ParticipationRepository participationRepository,
        ServiceSondage serviceSondage)
    {
        _participantRepository = participantRepository;
        _participationRepository = participationRepository;
        _serviceSondage = serviceSondage;
    }

    // "virtual" : uniquement pour permettre a Moq de les simuler dans les
    // tests de ParticipationsController, sans reintroduire d'interface. Le
    // comportement reel de chaque methode n'est pas affecte.
    public virtual async Task<Participant?> CreerParticipationAsync(string sondageId)
    {
        if (_serviceSondage.ObtenirParId(sondageId) is null)
        {
            return null;
        }

        var participant = new Participant
        {
            Cle = GenererCleSecurisee(),
            SondageId = sondageId,
            DateEmission = DateTime.UtcNow,
            EstUtilisee = false
        };

        await _participantRepository.AddAsync(participant).ConfigureAwait(false);
        return participant;
    }

    public virtual async Task<(bool Existe, bool EstUtilisee, string? SondageId)> ObtenirStatutAsync(string cle)
    {
        var participant = await _participantRepository.FindAsync(cle).ConfigureAwait(false);
        return participant is null ? (false, false, null) : (true, participant.EstUtilisee, participant.SondageId);
    }

    public virtual async Task<(ResultatSoumission Resultat, Participation? Participation, IReadOnlyList<string> Erreurs)> SoumettreAsync(
        string cle,
        SoumissionSondageDto dto)
    {
        var participant = await _participantRepository.FindAsync(cle).ConfigureAwait(false);
        if (participant is null)
        {
            return (ResultatSoumission.CleInvalide, null, Array.Empty<string>());
        }

        var sondage = _serviceSondage.ObtenirParId(participant.SondageId);
        if (sondage is null)
        {
            // Filet de securite : le sondage a disparu apres l'emission de la cle.
            return (ResultatSoumission.CleInvalide, null, Array.Empty<string>());
        }

        // On valide le CONTENU des reponses AVANT de consommer la cle, pour
        // qu'une soumission mal formee ne brule pas la seule tentative du
        // participant.
        var erreurs = _serviceSondage.ValiderReponses(sondage, dto.Reponses);
        if (erreurs.Count > 0)
        {
            return (ResultatSoumission.ReponsesInvalides, null, erreurs);
        }

        // Consommation atomique de la cle (existence + pas deja utilisee +
        // marquage comme utilisee, sous un seul verrou) : elimine la fenetre
        // de course entre "verifier" et "enregistrer" (TOCTOU).
        var resultatConsommation = await _participantRepository.TryConsumeAsync(cle).ConfigureAwait(false);
        if (resultatConsommation == ResultatConsommationCle.DejaUtilisee)
        {
            return (ResultatSoumission.DejaSoumise, null, Array.Empty<string>());
        }

        if (resultatConsommation != ResultatConsommationCle.Succes)
        {
            return (ResultatSoumission.CleInvalide, null, Array.Empty<string>());
        }

        var participation = new Participation
        {
            SondageId = participant.SondageId,
            ParticipantCle = cle,
            // GroupBy plutot que ToDictionary direct : evite un crash si le
            // client envoie deux fois le meme QuestionId (on garde la derniere valeur).
            Reponses = dto.Reponses
                .GroupBy(r => r.QuestionId)
                .Select(g => new Reponse { QuestionId = g.Key, Valeur = g.Last().Valeur })
                .ToList(),
            DateSoumission = DateTime.UtcNow
        };

        await _participationRepository.AddAsync(participation).ConfigureAwait(false);

        return (ResultatSoumission.Succes, participation, Array.Empty<string>());
    }

    /// <summary>
    /// Genere une cle aleatoire cryptographiquement sure (128 bits) encodee
    /// en Base64Url, pour qu'elle ne puisse pas etre devinee ou enumeree.
    /// </summary>
    private static string GenererCleSecurisee()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
