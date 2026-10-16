using Unity.Burst;
using UnityEditor;

// Echo does not use Jobs/Burst. The Burst compiler server can crash after a
// domain reload or a killed batch build and spam "Unexpected error in Burst compilation".
[InitializeOnLoad]
public static class DisableBurst
{
    static DisableBurst()
    {
        BurstCompiler.Options.EnableBurstCompilation = false;
        BurstCompiler.Options.EnableBurstSafetyChecks = false;
    }
}
