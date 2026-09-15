using System.Runtime.CompilerServices;

// Reproducing the RFC 8291 vector needs a fixed salt and sender key, which the public encrypt
// surface must never accept from a caller.
[assembly: InternalsVisibleTo("Notifications.Tests")]
