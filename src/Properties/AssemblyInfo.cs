using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[assembly: AssemblyTitle ("TestLite")]
[assembly: AssemblyDescription ("Lightweight TF replacement")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("")]
[assembly: AssemblyProduct ("KSPTestLite")]
[assembly: AssemblyCopyright ("Copyright © 2019")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]

[assembly: Guid("a1a38e23-00df-44d8-9caf-c389e268f579")]

[assembly: AssemblyVersion("0.2")]
[assembly: AssemblyFileVersion("0.2.1")]

// Use KSPAssembly to allow other DLLs to make this DLL a dependency in a
// non-hacky way in KSP.  Format is (AssemblyProduct, major, minor), and it
// does not appear to have a hard requirement to match the assembly version.
[assembly: KSPAssembly("TestLite", 0, 2)]
[assembly: KSPAssemblyDependency("RealFuels", 12, 6)]
