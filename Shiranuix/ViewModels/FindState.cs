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

    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand CloseCommand { get; }

    private CancellationTokenSource? _cts;

    private IReadOnlyList<FindMatch> _matches = Array.Empty<FindMatch>();

    /// <summary>
    /// Creates the state over the text it searches and the door that shows
    /// a run when one is chosen.
    /// </summary>
    public FindState(Func<string> text) {
        _text = text ?? throw new ArgumentNullException(nameof(text));
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
            var hits = FindMatches.Find(text, Query, MatchCase, WholeWord, UseRegex, out var error);
            if (token.IsCancellationRequested) return;
            Dispatcher.UIThread.Post(() => {
                if (token.IsCancellationRequested) return;
                Apply(hits, error);
            });
        }, token);
    }

    private void Cancel() {
        try { _cts?.Cancel(); } catch { /* token may already be disposed */ }
    }

    private void Apply(IReadOnlyList<FindMatch> hits, FindQueryError error) {
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

        CurrentIndex = 0;
        Reveal(0);
    }

    private void Clear() {
        _matches = Array.Empty<FindMatch>();
        MatchCount = 0;
        CurrentIndex = -1;
        Status = string.Empty;
    }
}
