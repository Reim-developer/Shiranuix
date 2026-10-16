using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The document's find: a query, its options, the runs that answer it,
/// the one that is on the page right now, and the words the find bar
/// speaks in turn. All of it happens off the user-interface thread —
/// a paragraph of thousand-line prose is not a thing to search while
/// the caret is waiting.
/// </summary>
public sealed partial class FindState : ObservableObject {
    /// <summary>Raised when the find matched a run and put it on the page.</summary>
    public event Action<int, int>? MatchRevealed;

    /// <summary>Raised when the find closed and the run should leave.</summary>
    public event Action? MatchHidden;

    private readonly Func<string> _text;
    private readonly Func<int> _caret;
    private readonly Action<Action> _dispatch;

    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand CloseCommand { get; }

    private CancellationTokenSource? _cts;

    private IReadOnlyList<FindMatch> _matches = Array.Empty<FindMatch>();

    /// <summary>
    /// Creates the state over the text it searches, the caret it starts
    /// from, and the door that shows a run when one is chosen.
    /// </summary>
    /// <param name="text">Reads the document's text when a search begins.</param>
    /// <param name="caret">Reads where the caret stands — the search
    /// offers the run after it first.</param>
    /// <param name="dispatch">Hands a finished search back to the thread
    /// that owns the window; the tests run it on the spot instead.</param>
    public FindState(Func<string> text, Func<int>? caret = null, Action<Action>? dispatch = null) {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _caret = caret ?? (() => 0);
        _dispatch = dispatch ?? (action => Dispatcher.UIThread.Post(action));
        NextCommand = new RelayCommand(Next);
        PreviousCommand = new RelayCommand(Previous);
        OpenCommand = new RelayCommand(Open);
        CloseCommand = new RelayCommand(Close);
    }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWord;

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private int _currentIndex = -1;

    [ObservableProperty]
    private int _matchCount = 0;

    [ObservableProperty]
    private string _status = string.Empty;

    partial void OnQueryChanged(string value) => Search();
    partial void OnMatchCaseChanged(bool value) => Search();
    partial void OnWholeWordChanged(bool value) => Search();
    partial void OnUseRegexChanged(bool value) => Search();

    partial void OnIsOpenChanged(bool value) {
        if (value) {
            Search();
        } else {
            Cancel();
            Clear();
            MatchHidden?.Invoke();
        }
    }

    /// <summary>Opens the find bar.</summary>
    public void Open() => IsOpen = true;

    /// <summary>Closes it and puts the match away.</summary>
    public void Close() => IsOpen = false;

    /// <summary>Moves to the next run, wrapping when the end is met.</summary>
    public void Next() {
        if (_matches.Count == 0) {
            return;
        }

        CurrentIndex = (CurrentIndex + 1) % _matches.Count;
        Reveal(CurrentIndex);
    }

    /// <summary>Moves to the previous run, wrapping when the start is met.</summary>
    public void Previous() {
        if (_matches.Count == 0) {
            return;
        }

        CurrentIndex = (CurrentIndex - 1 + _matches.Count) % _matches.Count;
        Reveal(CurrentIndex);
    }

    private void Reveal(int index) {
        if (index < 0 || index >= _matches.Count) return;
        var match = _matches[index];
        Status = $"{index + 1} of {_matches.Count}";
        MatchRevealed?.Invoke(match.Start, match.Length);
    }

    private void Search() {
        Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;
        Task.Run(() => {
            var text = _text();
            var caret = _caret();
            var hits = FindMatches.Find(text, Query, MatchCase, WholeWord, UseRegex, out var error);
            if (token.IsCancellationRequested) return;
            _dispatch(() => {
                if (token.IsCancellationRequested) return;
                Apply(hits, error, caret);
            });
        }, token);
    }

    private void Cancel() {
        try { _cts?.Cancel(); } catch { /* token may already be disposed */ }
    }

    private void Apply(IReadOnlyList<FindMatch> hits, FindQueryError error, int caret) {
        _matches = hits;
        MatchCount = hits.Count;

        if (hits.Count == 0) {
            CurrentIndex = -1;
            if (Query.Length > 0) {
                Status = error == FindQueryError.BadRegex
                    ? "Invalid regex"
                    : "No results";
            } else {
                Status = string.Empty;
            }
            return;
        }

        // The run the writer is likely looking for is the one after the
        // caret — the find opened where they were reading, not at the top
        // of the page. Past the last run, the first is the nearest.
        var after = IndexAfterCaret(caret);
        CurrentIndex = after;
        Reveal(after);
    }

    /// <summary>
    /// The index of the first run beginning at or after the caret, or the
    /// first run when the caret sits past all of them.
    /// </summary>
    private int IndexAfterCaret(int caret) {
        for (int i = 0; i < _matches.Count; i++) {
            if (_matches[i].Start >= caret) {
                return i;
            }
        }

        return 0;
    }

    private void Clear() {
        _matches = Array.Empty<FindMatch>();
        MatchCount = 0;
        CurrentIndex = -1;
        Status = string.Empty;
    }
}
