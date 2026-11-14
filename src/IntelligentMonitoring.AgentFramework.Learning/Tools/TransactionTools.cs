
using System.ComponentModel;
using System.Text.Json;

namespace IntelligentMonitoring.AgentFramework.Learning.Tools;

public static class TransactionTools
{
    [Description(
        "Retrieves transaction details including service name, " +
        "environment, transaction status, error code and error message.")]
    public static string GetTransactionDetails(
        [Description("The transaction ID to search for.")]
        string transactionId)
    {
        // Demo data for learning function calling.
        // Later, we will replace this with a real SQL repository.

        if (transactionId.Equals(
                "TXN-20003",
                StringComparison.OrdinalIgnoreCase))
        {
            var transaction = new
            {
                TransactionId = "TXN-20003",
                TraceId = "TRACE-20003",
                ServiceName = "warehouse-events-api",
                Environment = "QA",
                Status = "Failed",
                ErrorCode = "DOWNSTREAM_TIMEOUT",
                ErrorMessage =
                    "Downstream shipment API did not respond within the configured timeout.",
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            };

            return JsonSerializer.Serialize(transaction);
        }

        return JsonSerializer.Serialize(new
        {
            Found = false,
            TransactionId = transactionId,
            Message = "No transaction was found for the supplied transaction ID."
        });
    }
}
