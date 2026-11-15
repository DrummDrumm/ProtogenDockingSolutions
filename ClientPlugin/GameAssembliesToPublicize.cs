using System.Runtime.CompilerServices;

// The assemblies the picture-in-picture (DockPip.cs) reaches into. Keep this list in sync with the <Publicize>
// entries in ClientPlugin.csproj: local builds publicize them with Krafs.Publicizer, and Pulsar publicizes the
// assemblies named here when it compiles the plugin on the player's PC.
[assembly: IgnoresAccessChecksTo("VRage.Render")]
[assembly: IgnoresAccessChecksTo("VRage.Render11")]

// Krafs.Publicizer generates this attribute class in local builds (LOCAL_BUILD); Pulsar's build needs it here.
#if !LOCAL_BUILD
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
#endif
