using System.Runtime.CompilerServices;

// Allow the EditMode test assembly to construct internal types (e.g. ServiceRegistrationBuilder)
// for white-box unit tests.
[assembly: InternalsVisibleTo("com.nonatomic.servicekit.tests.editmode")]
