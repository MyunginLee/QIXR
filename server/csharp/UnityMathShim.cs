// Only the value types and logging used by the original math sources.
// This is not a Unity runtime or a substitute for an Editor/Quest build.
namespace UnityEngine
{
    public class MonoBehaviour { }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        public float magnitude => (float)System.Math.Sqrt(x*x+y*y+z*z);
    }
    public static class Debug
    {
        public static void Log(object value) => System.Console.WriteLine(value);
        public static void LogError(object value) => System.Console.Error.WriteLine(value);
    }
}
namespace UnityEditor
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class MenuItem:System.Attribute { public MenuItem(string path) { } }
}
