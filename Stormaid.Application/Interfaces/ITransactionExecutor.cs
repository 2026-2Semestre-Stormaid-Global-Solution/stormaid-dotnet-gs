namespace Stormaid.Application.Interfaces;

public interface ITransactionExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    );
}