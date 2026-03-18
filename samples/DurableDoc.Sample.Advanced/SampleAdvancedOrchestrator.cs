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

    /// <summary>
    /// Collects the core identity documents needed before the account can be opened.
    /// </summary>
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

    /// <summary>
    /// Schedules the first customer-facing follow-up actions after the account is provisioned.
    /// </summary>
    public static async Task ScheduleFollowUpSubOrchestrator(TaskOrchestrationContext context)
    {
        await context.CallActivityAsync("ScheduleWelcomeCall");
        await context.CallActivityAsync("ScheduleFirstBillingCycle");
    }

    /// <summary>
    /// Loads the submitted application so onboarding can use the customer and product details.
    /// </summary>
    [Function(nameof(LoadApplication))]
    public static CustomerApplication LoadApplication([ActivityTrigger] TaskActivityContext context)
        => new("sample-customer", "checking");

    /// <summary>
    /// Confirms the customer profile satisfies the onboarding policy checks.
    /// </summary>
    [Function(nameof(ValidateCustomer))]
    public static Task ValidateCustomer([ActivityTrigger] TaskActivityContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Opens the operations case that tracks document collection progress.
    /// </summary>
    [Function(nameof(CreateCase))]
    public static Task CreateCase([ActivityTrigger] TaskActivityContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Sends the final onboarding email after the workflow reaches its terminal state.
    /// </summary>
    [Function(nameof(SendWelcomeEmail))]
    public static Task SendWelcomeEmail([ActivityTrigger] TaskActivityContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Books the welcome call that closes the onboarding loop with the customer.
    /// </summary>
    [Function(nameof(ScheduleWelcomeCall))]
    public static Task ScheduleWelcomeCall([ActivityTrigger] TaskActivityContext context)
        => Task.CompletedTask;

    public sealed record CustomerApplication(string CustomerId, string ProductCode);
}
