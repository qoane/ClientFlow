using System;

namespace ClientFlow.Application.DTOs;

public sealed record SurveyVersionSummaryDto(int Version, DateTimeOffset CreatedUtc, bool IsPublished);
