using System.Threading.Tasks;

namespace DurableDoc.Sample.Simple;

public sealed class TaskOrchestrationContext
{
    public Task CallActivityAsync(string name) => Task.CompletedTask;
}
