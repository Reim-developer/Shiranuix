namespace Shiranuix.Models;

/// <summary>
/// The shape an export to PDF is drawn in: the typeface and the point size
/// the paper carries, as the export dialog answered.
/// </summary>
/// <remarks>
/// A record rather than a row of two values: the dialog hands one over, the
/// writer reads it, and the store remembers it — one type, fewer places the
/// halves can fall out of step.
/// </remarks>
public sealed record PdfExportOptions(string FontFamily, double FontSize);
