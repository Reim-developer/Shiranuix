// The library the writing itself lives in is reachable by name everywhere
// here, so nothing in this project has to say where a document, a ledger or
// an anchor came from. It is a global using rather than a handful of file
// level ones because the split between this project and that one is a
// division of work, not a difference of opinion: the window asks the
// writing, and never the other way round, and there is no name here to be
// careful about.
global using Shiranuix.CoreEditor;
