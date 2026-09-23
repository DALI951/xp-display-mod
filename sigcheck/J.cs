
using System;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes;

public static class Jo : MonoBehaviour {
    static void P(){
        var go = new GameObject();
        go.AddComponent(typeof(Component)); // non-generic takes Il2CppSystem.Type
    }
}
