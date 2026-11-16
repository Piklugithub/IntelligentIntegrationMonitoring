
namespace IntelligentMonitoring.AgentFramework.Learning.Models;

public sealed class FailureAnalysisResult
{
    public string Summary { get; set; } = string.Empty;

    public string ProbableRootCause { get; set; } = string.Empty;

    public string BusinessImpact { get; set; } = string.Empty;

    public double RootCauseConfidence { get; set; }

    public List<string> RecommendedActions { get; set; } = [];
}
