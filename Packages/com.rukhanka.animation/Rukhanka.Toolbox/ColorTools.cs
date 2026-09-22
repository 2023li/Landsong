using Unity.Mathematics;
using UnityEngine;

////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Toolbox
{
public static class ColorTools
{
    public static int ToInt(this Color32 c)
    {
        var rv = c.r << 24 | c.g << 16 | c.b << 8 | c.a;
		return rv;
    }
    
////////////////////////////////////////////////////////////////////////////////////

    public static Color32 FromInt(int v)
    {
        var rv = new Color32((byte)(v >> 24 & 0xff), (byte)(v >> 16 & 0xff), (byte)(v >> 8 & 0xff), (byte)(v & 0xff));
		return rv;
    }
    
////////////////////////////////////////////////////////////////////////////////////

    public static string ToWebColor(Color c)
    {
        Color32 c32 = c;
        var rv = $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        return rv;
    }
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static uint ToUint(float4 cl)
    {
        uint rv = (uint)(cl.x * 255) << 24 | (uint)(cl.y * 255) << 16 | (uint)(cl.z * 255) << 8 | (uint)(cl.w * 255);
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static uint ToUint(Color cl)
    {
        uint rv = (uint)(cl.r * 255) << 24 | (uint)(cl.g * 255) << 16 | (uint)(cl.b * 255) << 8 | (uint)(cl.a * 255);
        return rv;
    }
}
}
