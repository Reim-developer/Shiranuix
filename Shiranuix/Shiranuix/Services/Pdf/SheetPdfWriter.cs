using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Shiranuix.CoreFormat;
using Shiranuix.Models;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// Writes a document onto paper: an A4 page, a margin all round, and the
/// page's own typeface, size and way of setting each of its lines.
/// </summary>
/// <remarks>
/// <para>
/// Thin on purpose. Where the words go was decided by
/// <see cref="SheetLayout"/>, where a test can hold the decisions; this is
/// only the pen, asked to carry them out.
/// </para>
/// <para>
/// The typeface is carried inside the file rather than left to the
/// reader's machine: the encoding is Unicode, so the diacritics the page
/// was written with arrive whole. The size travels from screen to paper
/// the way a pixel maps to a point at a screen's own resolution - three
/// quarters of itself.
/// </para>
/// </remarks>
public static class SheetPdfWriter {
    /// <summary>The margin all round, about as much air as a written page keeps.</summary>
    private const double Margin = 57;

    /// <summary>Screen pixels to paper points: 96 pixels stand 72 points tall.</summary>
    private const double PointPerScreenPixel = 72.0 / 96.0;

    /// <summary>
    /// Tells the pen where typefaces are kept: the machine's own, so the
    /// paper is drawn in the typeface the sheet was written in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pen would otherwise ask for a private shelf the app does not
    /// keep - the library's own knowing is a handful of core faces, and
    /// the page's Georgia would arrive with nothing behind it. The house
    /// resolver reads the same shelves the sheet's own typeface list
    /// comes from, so what the paper draws and what the machine carries
    /// are one answer rather than two.
    /// </para>
    /// <para>
    /// The resolver is handed over once and only once. It is a global
    /// setting of the library: a second handover, arrived at by a second
    /// export, would not be written at all - the pen already answered to
    /// a name. A value already the same is asked for nothing rather than
    /// argued with, and asked for nothing is not the memory of whether
    /// the ask happened.
    /// </para>
    /// </remarks>
    static SheetPdfWriter() {
        GlobalFontSettings.FontResolver = MachineFontResolver.Instance;
    }

    /// <summary>
    /// Writes the document to the given path as a PDF.
    /// </summary>
    /// <param name="path">Where the file goes.</param>
    /// <param name="documentName">What the file says it is, without any ending.</param>
    /// <param name="text">The document as written.</param>
    /// <param name="fontFamily">The typeface the page is set in, as the machine carries it.</param>
    /// <param name="fontSize">The page's type size, in screen pixels.</param>
    /// <param name="alignments">How each line of the document is set, by
    /// its number in the writing — the same map the screen drew from, so
    /// the paper carries the heading's centring and the line flush right
    /// exactly as the page showed them. A line the map does not name is
    /// flush left, which is what a line nobody spoke for always is.</param>
    /// <param name="lineHeightFactor">The line spacing as a multiple of the size, as the page shows it.</param>
    /// <returns>
    /// The typeface the paper was actually drawn in: the page's own, or the
    /// spare face that carried the writing when the page's own could not.
    /// </returns>
    public static string Write(
        string path,
        string documentName,
        string text,
        string fontFamily,
        double fontSize,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments,
        double lineHeightFactor) {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(documentName);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fontFamily);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        var used = SheetFonts.UsedCodepoints(text);
        var face = SheetFonts.PickFace(fontFamily, used, FamilyBytes, MachineFontResolver.Instance.Spares);

        var font = new XFont(
            face,
            fontSize * PointPerScreenPixel,
            XFontStyleEx.Regular,
            XPdfFontOptions.UnicodeDefault);

        var lineHeight = fontSize * PointPerScreenPixel * lineHeightFactor;

        var document = new PdfDocument();
        var paper = document.AddPage();
        // An A4 page wherever the machine stands: a page's size is the
        // paper's own habit, not the machine's habits. Left to defaults, the
        // page takes the size the running machine's locale keeps — Letter
        // where it is American — and the paper it becomes is nobody's choice.
        paper.Size = PdfSharp.PageSize.A4;
        SheetPlan plan;

        // The first page is born to be measured against: measuring here is
        // asking the same pen that will draw, so the plan and the ink agree
        // about how wide words are. Each line is set for itself, off the
        // document's own map, so the paper stands the way the screen stood.
        using (var graphics = XGraphics.FromPdfPage(paper)) {
            plan = SheetLayout.Plan(
                text,
                line => graphics.MeasureString(line, font).Width,
                paper.Width.Point - 2 * Margin,
                paper.Height.Point - 2 * Margin,
                lineHeight,
                LineIsSet(alignments));

            Draw(graphics, plan.Pages[0], font, lineHeight);
        }

        for (var index = 1; index < plan.Pages.Count; ++index) {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;

            using (var graphics = XGraphics.FromPdfPage(page)) {
                Draw(graphics, plan.Pages[index], font, lineHeight);
            }
        }

        document.Info.Title = documentName;
        document.Save(path);
        return face;
    }

    /// <summary>The machine's file for a family, as the pen reads it.</summary>
    private static byte[]? FamilyBytes(string family) {
        var face = MachineFontResolver.Instance.FacePath(family);

        return face is null ? null : File.ReadAllBytes(face);
    }

    /// <summary>
    /// How the layout asks about a line: the map's own answer where it has
    /// one, and flush left where it does not.
    /// </summary>
    private static Func<int, ParagraphAlignment> LineIsSet(
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments) =>
        alignments is null
            ? static _ => ParagraphAlignment.Left
            : line => alignments.TryGetValue(line, out var set)
                ? set
                : ParagraphAlignment.Left;

    /// <summary>Sets a page's rows down its body, each in the place the plan named.</summary>
    private static void Draw(XGraphics graphics, SheetPage page, XFont font, double lineHeight) {
        var top = Margin;

        foreach (var line in page.Lines) {
            if (line.Text.Length > 0) {
                graphics.DrawString(
                    line.Text,
                    font,
                    XBrushes.Black,
                    new XPoint(Margin + line.Left, top),
                    XStringFormats.TopLeft);
            }

            top += lineHeight;
        }
    }
}
