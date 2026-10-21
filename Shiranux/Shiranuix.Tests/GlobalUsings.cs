// The library the writing itself lives in is reachable by name everywhere
// here, so nothing in this project has to say where a document, a ledger or
// an anchor came from. It is a global using rather than a handful of file
// level ones because the split between this project and that one is a
// division of work, not a difference of opinion: the tests are about the
// rules of the writing, and the window is nowhere near.
global using Shiranuix.CoreEditor;

// And the vocabulary those tests say lines and encodings in: a line break
// and an encoding are the text package's own, by the same reasoning as
// above — asked once, never spelled in an assertion.
global using Shiranuix.CoreText;

// And so are the doubles it is tested over — the disk that is not a disk
// and the store that remembers only what it is told moved out with the
// writing's tests, and both test projects lean on them from here.
global using Shiranuix.Testing;
