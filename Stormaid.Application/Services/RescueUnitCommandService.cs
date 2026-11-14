using Stormaid.Application.DTOs.Request;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Exceptions;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;

namespace Stormaid.Application.Services;

/// <summary>
/// Serviço responsável pelas operações de cadastro
/// das unidades institucionais de resgate.
/// </summary>
public sealed class RescueUnitCommandService
{
    private readonly IRescueUnitRepository _repository;

    public RescueUnitCommandService(IRescueUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<RescueUnitResponse> CreateAsync(
        CreateRescueUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unit = new RescueUnit(
            request.UnitCode,
            request.DisplayName,
            request.InstitutionType,
            request.Capacity,
            request.SupportsAccessibleTransport,
            DateTime.UtcNow
        );

        var codeExists = await _repository.ExistsByUnitCodeAsync(
            unit.UnitCode,
            cancellationToken
        );

        if (codeExists)
        {
            throw new RescueUnitCodeAlreadyExistsException(
                unit.UnitCode
            );
        }

        await _repository.AddAsync(
            unit,
            cancellationToken
        );

        return RescueUnitResponse.FromEntity(unit);
    }
}