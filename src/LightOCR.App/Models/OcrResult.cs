using System.Text.Json.Serialization;

namespace LightOCR.App.Models;

public sealed record OcrLineResult(
    int Order,
    string Text,
    float Confidence,
    int[][] Polygon);

public sealed record OcrDocumentResult(
    string FullText,
    IReadOnlyList<OcrLineResult> Lines,
    TimeSpan Elapsed,
    int ImageWidth,
    int ImageHeight);
