using Stormaid.Application.Interfaces;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure.Transactions;

public sealed class OracleTransactionExecutor : ITransactionExecutor
{
    private readonly StormaidDbContext _context;

    public OracleTransactionExecutor(StormaidDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var result = await operation(cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);

            throw;
        }
    }
}