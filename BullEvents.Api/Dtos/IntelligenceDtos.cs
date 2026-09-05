using BullEvents.Api.Ml;

namespace BullEvents.Api.Dtos;

public record ScoreSignalDto(string Label, string Detail, int Points);

public record LeadInsightsDto(
    int LeadId,
    int Score,
    string Band,
    string Engine,
    IReadOnlyList<ScoreSignalDto> Signals,
    IReadOnlyList<RecommendedAction> Actions,
    IReadOnlyList<DuplicateMatch> Duplicates
);

public record ModelStatusDto(
    string Engine,
    DateTime? TrainedAt,
    int TrainingRows,
    double? Accuracy,
    double? AreaUnderRocCurve,
    string? Message
);

public record ScoredLeadDto(
    int LeadId,
    string Name,
    string Stage,
    int Score,
    string Band
);
