using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;

namespace Stormaid.Application.Services;

public sealed class IncidentReferenceQueryService
{
    private readonly IIncidentReferenceRepository _repository;

    public IncidentReferenceQueryService(
        IIncidentReferenceRepository repository)
    {
        _repository = repository;
    }

    public async Task<IncidentReferenceResponse?> GetByJavaIncidentIdAsync(
        long javaIncidentId,
        CancellationToken cancellationToken = default)
    {
        if (javaIncidentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(javaIncidentId)
            );
        }

        var incident = await _repository.GetByJavaIncidentIdAsync(
            javaIncidentId,
            cancellationToken
        );

        return incident is null
            ? null
            : IncidentReferenceResponse.FromEntity(incident);
    }
}