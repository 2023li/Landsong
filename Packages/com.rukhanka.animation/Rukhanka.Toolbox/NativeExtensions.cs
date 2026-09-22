using System.Threading;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Toolbox
{
namespace AtomicHelpers
{
}
public static class NativeExtensions
{

	public static unsafe void SetBitThreadSafe(this UnsafeBitArray ba, int pos)
    {
		var idx = pos >> 6;
		var shift = pos & 0x3f;
		var value = 1ul << shift;
		InterlockedOr((long*)ba.Ptr, idx, (long)value);
    }

/////////////////////////////////////////////////////////////////////////////////

    static unsafe void InterlockedOr(long* qwords, int index, long value)
    {
        // TODO: Replace this with atomic OR once it is available
        long currentValue = Interlocked.Read(ref qwords[index]);
        for (;;)
        {
            // If the OR wouldn't change any bits, no need to issue the atomic
            if ((currentValue | value) == currentValue)
                return;

            long newValue = currentValue | value;
            long prevValue =
                Interlocked.CompareExchange(ref qwords[index], newValue, currentValue);

            // If the value was equal to the expected value, we know that our atomic went through
            if (prevValue == currentValue)
                return;

            currentValue = prevValue;
        }
    }
    
/////////////////////////////////////////////////////////////////////////////////

	public static unsafe void InterlockedMax(int* ptr, int cmpVal)
	{
		ref var r = ref UnsafeUtility.ArrayElementAsRef<int>(ptr, 0);
		int maxVal;
		int v;
		
		do
		{
			v = r;
			maxVal = math.max(v, cmpVal);
		} while (r != Interlocked.CompareExchange(ref r, maxVal, v));
	}

}
}
