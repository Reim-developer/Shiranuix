using Shiranuix.CoreFormat;
using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for <see cref="AlignmentRebaser"/>: a face follows the letters it
/// was set on, a paragraph erased has no face left to keep, and two
/// paragraphs folded into one keep the face of the one higher up.
/// </summary>
/// <remarks>
/// Each test hands over the four answers a save arrives with — the source
/// text, the faces standing on it, the moves the writer made, and the
/// writing those moves composed — and asks where the faces now stand.
/// </remarks>
public sealed class AlignmentRebaserTests {
    [Fact]
    public void No_Moves_Leaves_Every_Face_Where_It_Was() {
        var faces = new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center };

        var rebased = AlignmentRebaser.Rebase("mở đầu\ntiêu đề", faces, [], "mở đầu\ntiêu đề");

        Assert.Equal(faces, rebased);
    }

    [Fact]
    public void No_Faces_Stays_Empty_Through_Any_Move() {
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu", new Dictionary<int, ParagraphAlignment>(),
            [new TextReplacement(0, 0, "dòng mới\nvào đầu\n")],
            "dòng mới\nvào đầu\nmở đầu");

        Assert.Empty(rebased);
    }

    [Fact]
    public void A_Face_Follows_Its_Letters_Down_When_Lines_Are_Written_Above() {
        // The heading was the second paragraph; the writer added two lines
        // over it, so the heading's own letters now open the fourth.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center },
            [new TextReplacement(0, 0, "dòng một\ndòng hai\n")],
            "dòng một\ndòng hai\nmở đầu\ntiêu đề");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [3] = ParagraphAlignment.Center
        }, rebased);
    }

    [Fact]
    public void A_Face_Climes_When_The_Paragraph_Above_It_Is_Deleted() {
        // The paragraph over the heading is gone, so the heading's letters
        // slide up one paragraph and the face climbs with them.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\nvứt đi\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [2] = ParagraphAlignment.Center },
            [new TextReplacement(7, 6, string.Empty)],
            "mở đầu\ntiêu đề");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [1] = ParagraphAlignment.Center
        }, rebased);
    }

    [Fact]
    public void Typing_Inside_A_Paragraph_Keeps_Its_Face_On_The_Same_Paragraph() {
        // Editing the heading's own letters does not carry it to another
        // paragraph: no break was added or taken, so its number stands.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center },
            [new TextReplacement(7, 7, "phần đầu")],
            "mở đầu\nphần đầu");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [1] = ParagraphAlignment.Center
        }, rebased);
    }

    [Fact]
    public void A_Paragraph_Erased_Entirely_Takes_Its_Face_With_It() {
        // The heading was taken whole — its letters and the break ending
        // them — and put back as nothing. What slides into its number is the
        // paragraph after, which never wore the face.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\ntiêu đề\nkết",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center },
            [new TextReplacement(7, 8, string.Empty)],
            "mở đầu\nkết");

        Assert.Empty(rebased);
    }

    [Fact]
    public void A_Face_On_The_Last_Paragraph_Is_Dropped_When_It_Is_Deleted() {
        // The last paragraph has no break of its own to protect it: reaching
        // the end of the text is the same erasure as any break gone by.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center },
            [new TextReplacement(7, 7, string.Empty)],
            "mở đầu");

        Assert.Empty(rebased);
    }

    [Fact]
    public void A_Face_Written_Over_Lands_On_What_Took_Its_Place() {
        // The heading's letters were replaced by fresh letters rather than
        // erased, so the face goes to the paragraph those letters now open.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center },
            [new TextReplacement(7, 7, "phần mới")],
            "mở đầu\nphần mới");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [1] = ParagraphAlignment.Center
        }, rebased);
    }

    [Fact]
    public void The_Paragraph_Higher_Up_Keeps_Its_Face_When_Two_Fold_Into_One() {
        // Taking out the break between a centred heading and the paragraph
        // below folds them together. Both land on the same number, and the
        // one that came first — the higher paragraph — keeps the face.
        var rebased = AlignmentRebaser.Rebase(
            "tiêu đề\nthân bài",
            new Dictionary<int, ParagraphAlignment> {
                [0] = ParagraphAlignment.Center,
                [1] = ParagraphAlignment.Right
            },
            [new TextReplacement(7, 1, string.Empty)],
            "tiêu đềthân bài");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [0] = ParagraphAlignment.Center
        }, rebased);
    }

    [Fact]
    public void Pressing_Enter_At_The_Head_Of_A_Paragraph_Takes_The_Face_Down() {
        // Enter standing exactly on a heading's first letter opens a
        // paragraph above it. The heading's letters — and so its face — move
        // down rather than staying behind on a number nothing opens.
        var rebased = AlignmentRebaser.Rebase(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center },
            [new TextReplacement(7, 0, "\n")],
            "mở đầu\n\ntiêu đề");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [2] = ParagraphAlignment.Center
        }, rebased);
    }

    [Fact]
    public void A_Face_On_The_Last_Paragraph_Survives_Edits_Before_It() {
        // Two moves land above it — a line written over the front, a word
        // rewritten in the middle — and the closing line keeps its face at
        // the number its letters now open.
        var rebased = AlignmentRebaser.Rebase(
            "một\nhai\nba",
            new Dictionary<int, ParagraphAlignment> { [2] = ParagraphAlignment.Right },
            [
                new TextReplacement(0, 0, "số zero\n"),
                new TextReplacement(4, 3, "HAI")
            ],
            "số zero\nmột\nHAI\nba");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [3] = ParagraphAlignment.Right
        }, rebased);
    }

    [Fact]
    public void A_Face_Naming_A_Paragraph_The_Source_Never_Had_Is_Skipped() {
        // A stray the format let through: the source holds two paragraphs
        // and the record names the fifth. There are no letters to follow.
        var rebased = AlignmentRebaser.Rebase(
            "một\nhai",
            new Dictionary<int, ParagraphAlignment> { [4] = ParagraphAlignment.Center },
            [new TextReplacement(0, 0, "bốn dòng\nsẽ thêm\nvào đây\n")],
            "bốn dòng\nsẽ thêm\nvào đây\nmột\nhai");

        Assert.Empty(rebased);
    }

    [Fact]
    public void Every_Face_In_A_Chapter_Lands_On_Its_Own_Letters() {
        // The whole shape of a chapter: a centred title, a blank line, a
        // centred section head, a body, and a right-set closing line. Lines
        // go on over the front and one heading is rewritten — each face
        // arrives where its own letters now stand.
        var source = "Tiêu đề\n\nMở\n\nĐầu\ngiữa\ncuối\n\nKết";
        var faces = new Dictionary<int, ParagraphAlignment> {
            [0] = ParagraphAlignment.Center,
            [4] = ParagraphAlignment.Center,
            [8] = ParagraphAlignment.Right
        };

        var rebased = AlignmentRebaser.Rebase(
            source, faces,
            [
                new TextReplacement(0, 0, "Chương\n\n"),
                new TextReplacement(13, 3, "Đầu đã đổi")
            ],
            "Chương\n\nTiêu đề\n\nMở\n\nĐầu đã đổi\ngiữa\ncuối\n\nKết");

        Assert.Equal(new Dictionary<int, ParagraphAlignment> {
            [2] = ParagraphAlignment.Center,
            [6] = ParagraphAlignment.Center,
            [10] = ParagraphAlignment.Right
        }, rebased);
    }
}
