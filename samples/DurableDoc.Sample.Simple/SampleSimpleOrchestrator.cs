using System.Threading.Tasks;

namespace DurableDoc.Sample.Simple;

public static class SampleSimpleOrchestrator
{
    public static async Task RunBasicFulfillment(TaskOrchestrationContext context)
    {
        await context.CallActivityAsync("ValidateOrder");
        await context.CallActivityAsync("ReserveInventory");
        await context.CallActivityAsync("ChargePayment");
        await context.CallActivityAsync("SendReceipt");
    }
}
