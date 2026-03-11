using System.Threading;
using System.Threading.Tasks;

namespace DurableDoc.Sample.Advanced;

public static class SampleAdvancedOrchestrator
{
    public static async Task RunCustomerOnboarding(TaskOrchestrationContext context)
    {
        var application = await context.CallActivityAsync<CustomerApplication>("LoadApplication");
        await context.CallActivityAsync("ValidateCustomer");
        await context.CallActivityWithRetryAsync("ReserveCreditCheck");
        await context.CallSubOrchestratorAsync("CollectDocumentsSubOrchestrator");
        await context.CallSubOrchestratorAsync("ProvisionAccountSubOrchestrator");
        await context.WaitForExternalEvent<string>("WaitForCustomerApproval");
        await context.CreateTimer(DateTime.UtcNow.AddHours(12), CancellationToken.None);
        await context.CallActivityAsync("SendWelcomeEmail");

        _ = application;
    }

    public static async Task CollectDocumentsSubOrchestrator(TaskOrchestrationContext context)
    {
        await context.CallActivityAsync("CreateCase");
        await context.CallActivityAsync("UploadIdentityDocument");
        await context.CallActivityAsync("UploadProofOfAddress");
    }

    public static async Task ProvisionAccountSubOrchestrator(TaskOrchestrationContext context)
    {
        await context.CallActivityAsync("CreateCustomerRecord");
        await context.CallActivityAsync("ActivateCard");
        await context.CallSubOrchestratorAsync("ScheduleFollowUpSubOrchestrator");
    }

    public static async Task ScheduleFollowUpSubOrchestrator(TaskOrchestrationContext context)
    {
        await context.CallActivityAsync("ScheduleWelcomeCall");
        await context.CallActivityAsync("ScheduleFirstBillingCycle");
    }

    private sealed record CustomerApplication(string CustomerId, string ProductCode);
}
