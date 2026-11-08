using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace StellarDriveAutoHost {
 internal static class HeadlessCompatibility {
  static readonly MethodInfo Original=AccessTools.Method(typeof(Material),nameof(Material.HasProperty),new[]{typeof(string)});
  static readonly MethodInfo Replacement=AccessTools.Method(typeof(HeadlessCompatibility),nameof(AlbedoMarker));
  internal static void Install(HarmonyLib.Harmony h){
   int count=0;
   foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="Odyssey.Building"||a.GetName().Name=="Odyssey.Power"))
    foreach(var type in assembly.GetTypes().Where(t=>t.FullName.StartsWith("Odyssey.Content.",StringComparison.Ordinal)))
     foreach(var method in type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static)){
      if(method.GetMethodBody()==null)continue;
      var code=PatchProcessor.GetOriginalInstructions(method);
      // Only visual material selection based on the native _Albedo marker. Never change gameplay methods.
      if(!code.Any(i=>i.opcode==OpCodes.Ldstr&&(string)i.operand=="_Albedo")||!code.Any(i=>Equals(i.operand,Original)))continue;
      h.Patch(method,transpiler:new HarmonyMethod(typeof(HeadlessCompatibility),nameof(Transpile)));count++;AutoHost.Log("Headless visual compatibility: "+type.FullName+"."+method.Name);
     }
   AutoHost.Log("Headless material-selection patches: "+count+". Validation of registered parts still required.");
  }
  static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> code){foreach(var i in code){if(Equals(i.operand,Original)){i.opcode=OpCodes.Call;i.operand=Replacement;}yield return i;}}
  static bool AlbedoMarker(Material m,string property){if(m==null)return false;if(property=="_Albedo"&&SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return true;return m.HasProperty(property);}
 }
}
