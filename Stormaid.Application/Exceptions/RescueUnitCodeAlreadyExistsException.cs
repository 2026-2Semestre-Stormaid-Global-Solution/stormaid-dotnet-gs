namespace Stormaid.Application.Exceptions;

/// <summary>
/// Exceção lançada quando já existe uma unidade
/// de resgate com o código informado.
/// </summary>
public sealed class RescueUnitCodeAlreadyExistsException : Exception
{
    public RescueUnitCodeAlreadyExistsException(string unitCode)
        : base($"Já existe uma unidade de resgate com o código '{unitCode}'.")
    {
    }
}