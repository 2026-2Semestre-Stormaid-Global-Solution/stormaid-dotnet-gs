
using Stormaid.Application.DTOs.Integration;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;

namespace Stormaid.Application.Services;

/// <summary>
/// Processa eventos de ocorrências criadas
/// recebidos da API Java.
/// </summary>
public sealed class IncidentCreatedEventService
{
    private readonly IIncidentReferenceRepository _repository;

    public IncidentCreatedEventService(
        IIncidentReferenceRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Valida e persiste uma ocorrência recebida do Java,
    /// evitando o processamento sequencial de duplicidades.
    /// </summary>
    public async Task<IncidentReference> ProcessAsync(
        IncidentCreatedEvent message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.IncidentId is not > 0)
        {
            throw new ArgumentException(
                "IncidentId must be greater than zero.",
                nameof(message)
            );
        }

        if (message.RequesterUserId is not > 0)
        {
            throw new ArgumentException(
                "RequesterUserId must be greater than zero.",
                nameof(message)
            );
        }

        var existingIncident =
            await _repository.GetByJavaIncidentIdAsync(
                message.IncidentId.Value,
                cancellationToken
            );

        if (existingIncident is not null)
        {
            return existingIncident;
        }

        // 3 - Criar a entidade de domínio

        var incident = new IncidentReference(
            javaIncidentId: message.IncidentId.Value,
            requesterUserId: message.RequesterUserId.Value,
            recipientType: message.RecipientType,
            channelType: message.ChannelType,
            emergencyCategory: message.EmergencyCategory,
            latitude: message.Location?.Latitude,
            longitude: message.Location?.Longitude,
            addressText: message.Location?.AddressText,
            javaCreatedAt: message.CreatedAt?.UtcDateTime,
            utcNow: DateTime.UtcNow
        );


        await _repository.AddAsync(
            incident,
            cancellationToken
        );

        return incident;
    }
}
