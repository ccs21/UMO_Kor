#if UNITY_EDITOR
using System;
using UnityEngine;

public static class UMODlcVersionTests
{
    public static void Run()
    {
        Assert(DlcPackage.IsVersionCompatible("1.1.17-ko-beta.9", "1.1.17"), "Korean beta suffix");
        Assert(DlcPackage.IsVersionCompatible("1.1.18", "1.1.17"), "newer patch");
        Assert(!DlcPackage.IsVersionCompatible("1.1.16-ko-beta.7", "1.1.17"), "older patch");
        Assert(!DlcPackage.IsVersionCompatible("invalid", "1.1.17"), "invalid current version");
        Assert(!DlcPackage.IsVersionCompatible("1.1.17", "invalid"), "invalid minimum version");
        Debug.Log("UMO DLC version tests passed.");
    }

    private static void Assert(bool condition, string name)
    {
        if(!condition)
            throw new Exception("UMO DLC version test failed: " + name);
    }
}
#endif
