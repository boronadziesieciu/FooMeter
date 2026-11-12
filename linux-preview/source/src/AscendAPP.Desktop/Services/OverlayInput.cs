using System.Runtime.InteropServices;
namespace AscendAPP.Desktop.Services;
internal readonly record struct OverlayKeys(bool Alt,bool ButtonsDown,bool ConflictingModifiers=false)
{public bool InteractionAlt=>Alt&&!ConflictingModifiers;}
internal static class OverlayInput
{
    private static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    internal static OverlayKeys ReadKeys()=>new(Down(0x12),Down(1)||Down(2)||Down(4),Down(0x11)||Down(0x5b)||Down(0x5c));
    internal static bool IsClickThrough(IntPtr hwnd)=>((IntPtr.Size==8?GetWindowLongPtr(hwnd,-20).ToInt64():GetWindowLong(hwnd,-20))&0x20L)!=0;
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    public static void SetClickThrough(IntPtr hwnd,bool enabled)
    {
        long prior=IntPtr.Size==8?GetWindowLongPtr(hwnd,-20).ToInt64():GetWindowLong(hwnd,-20);long next=enabled?prior|0x20L:prior&~0x20L;if(prior==next)return;
        if(IntPtr.Size==8)SetWindowLongPtr(hwnd,-20,new IntPtr(next));else SetWindowLong(hwnd,-20,(int)next);
    }
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll")]private static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")]private static extern int SetWindowLong(IntPtr h,int index,int value);
}
