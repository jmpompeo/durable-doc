using System.Threading;
using System.Threading.Tasks;

namespace DurableDoc.Sample.Advanced;

// Lightweight stubs keep the sample project buildable without external Azure packages.
public sealed class TaskOrchestrationContext
{
    public Task CallActivityAsync(string name) => Task.CompletedTask;

    public Task<T> CallActivityAsync<T>(string name) => Task.FromResult(default(T)!);

    public Task CallActivityWithRetryAsync(string name) => Task.CompletedTask;

    public Task<T> CallActivityWithRetryAsync<T>(string name) => Task.FromResult(default(T)!);

    public Task CallSubOrchestratorAsync(string name) => Task.CompletedTask;

    public Task<T> CallSubOrchestratorAsync<T>(string name) => Task.FromResult(default(T)!);

    public Task<T> WaitForExternalEvent<T>(string name) => Task.FromResult(default(T)!);

    public Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) => Task.CompletedTask;
}
