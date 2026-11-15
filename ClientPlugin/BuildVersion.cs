using System.Reflection;

[assembly: AssemblyVersion(ProtogenDocking.BuildVersion.Version)]
[assembly: AssemblyFileVersion(ProtogenDocking.BuildVersion.Version)]
[assembly: AssemblyInformationalVersion(ProtogenDocking.BuildVersion.Version)]

namespace ProtogenDocking;

// Shared by local builds and Pulsar's source compiler.
internal static class BuildVersion
{
    public const string Version = "1.1.0";
}
