using System.Runtime.Versioning;
using System.Text;
using Shiranuix.Models;
using Shiranuix.Services.Pdf;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for the reading engine over a paper PDF: the page count it reports
/// and what a page it renders. The papers are made by the exporter itself, so
/// the tests never carry a fixture.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PdfPagesTests {
    [Fact]
    public void A_Paper_Carries_The_Pages_Its_Pen_Wrote() {
        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-paper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var pdf = Path.Combine(home, "story.pdf");
        try {
            // Fifty lines is the window a page carried before; four times
            // over dwarfs one page, and the paper is more than one page.
            var whole = string.Join(
                "\n", Enumerable.Range(1, 200).Select(index => $"Dòng thứ {index}: một câu có dấu ế ộ ừ ỹ."));
            SheetPdfWriter.Write(
                pdf, "story", whole, "Georgia", 17, TextAlign.Left, TypographyState.LineHeightFactor);

            var paper = new PdfPages(pdf);

            // Whatever the exact break, two hundred lines cannot fit one page.
            Assert.True(paper.Count >= 2);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void A_Page_Asked_Of_The_Engine_Is_The_Pen_Works_Page() {
        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-paper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var pdf = Path.Combine(home, "story.pdf");
        try {
            SheetPdfWriter.Write(
                pdf, "story", "Một trang giấy hai câu.", "Georgia", 17, TextAlign.Left, TypographyState.LineHeightFactor);

            var paper = new PdfPages(pdf);
            using var page = paper.Draw(0, 800);

            // A page rendered at the width asked is a bitmap of real size.
            Assert.Equal(800, page.Width);
            Assert.True(page.Height > 0);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void A_File_That_Is_No_Paper_Is_Refused_As_Such() {
        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-paper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var txt = Path.Combine(home, "story.txt");
        try {
            File.WriteAllText(txt, "Chỉ là chữ, không phải giấy.", Encoding.UTF8);

            // PDFium reads papers; a text file says so itself.
            Assert.Throws<InvalidDataException>(() => new PdfPages(txt));
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void The_Paper_Tells_The_Size_Its_Pages_Are_Drawn_In() {
        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-paper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var pdf = Path.Combine(home, "story.pdf");
        try {
            var whole = string.Join(
                "\n", Enumerable.Range(1, 200).Select(index => $"Dòng thứ {index}: một câu có dấu ế ộ ừ ỹ."));
            SheetPdfWriter.Write(
                pdf, "story", whole, "Georgia", 17, TextAlign.Left, TypographyState.LineHeightFactor);

            var paper = new PdfPages(pdf);

            // An A4 page, told in points as the paper holds it — and told
            // again for the paper's last page, page after page the same.
            Assert.InRange(paper.PageSize(0).Height / paper.PageSize(0).Width, 1.40, 1.43);
            Assert.InRange(paper.PageSize(paper.Count - 1).Height / paper.PageSize(paper.Count - 1).Width, 1.40, 1.43);
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }
}
