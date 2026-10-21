// The library these doubles stand in for is reachable by name everywhere
// here, for the same reason it is in the projects above: everything in
// this project exists to answer an interface that library names, and
// saying where each one came from would be arithmetic, not information.
global using Shiranuix.CoreEditor;

// And the text those doubles write: the newline they detect and the
// encoding they write with are the text package's own, asked of it
// rather than spelled again here.
global using Shiranuix.CoreText;
