// The library the writing itself lives in is reachable by name everywhere
// here, so nothing in this project has to say where a document, a ledger or
// an anchor came from. It is a global using rather than a handful of file
// level ones because the split between this project and that one is a
// division of work, not a difference of opinion: the window asks the
// writing, and never the other way round, and there is no name here to be
// careful about.
// And the writing's own vocabulary, reachable by name the same way: what a
// line break is, and the encoding a file is read with, are the text
// package's answers, and the services, the views and the walks all ask
// them rather than spelling a second opinion.
global using Shiranuix.CoreEditor;
global using Shiranuix.CoreText;
