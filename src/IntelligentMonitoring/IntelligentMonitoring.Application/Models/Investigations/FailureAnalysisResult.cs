namespace IntelligentMonitoring.Application.Models.Investigations;

public sealed class FailureAnalysisResult
{
    public string Summary { get; set; } = string.Empty;

    public string FailureStage { get; set; } = string.Empty;

    public string ProbableRootCause { get; set; } = string.Empty;

    public string RootCauseConfidence { get; set; } = "Low";

    public string BusinessImpact { get; set; } = string.Empty;

    public List<string> SupportingEvidence { get; set; } = [];

    public List<string> RecommendedActions { get; set; } = [];

    public List<string> InformationGaps { get; set; } = [];

    public string AnalysisStatus { get; set; } = "Completed";

    public string ModelName { get; set; } = string.Empty;

    public DateTimeOffset AnalyzedAtUtc { get; set; }
}