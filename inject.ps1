$source = @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class Injector {
    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtResumeProcess(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    public static uint Inject(int pid, string dllPath) {
        IntPtr hProc = OpenProcess(0x1FFFFF, false, pid);
        if (hProc == IntPtr.Zero) return 0;
        try {
            // Unsuspend/Resume process if Windows suspended it
            NtResumeProcess(hProc);

            byte[] bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
            IntPtr pMem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)bytes.Length, 0x1000 | 0x2000, 0x04);
            if (pMem == IntPtr.Zero) return 0;
            IntPtr written;
            if (!WriteProcessMemory(hProc, pMem, bytes, (uint)bytes.Length, out written)) return 0;
            IntPtr pLoadLibrary = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            IntPtr hThread = CreateRemoteThread(hProc, IntPtr.Zero, 0, pLoadLibrary, pMem, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero) return 0;
            WaitForSingleObject(hThread, 5000);
            uint exitCode = 0;
            GetExitCodeThread(hThread, out exitCode);
            CloseHandle(hThread);
            return exitCode;
        } finally {
            CloseHandle(hProc);
        }
    }
}
'@

if (-not ([System.Management.Automation.PSTypeName]'Injector').Type) {
    Add-Type -TypeDefinition $source -Language CSharp
}

$dll = $args[0]
$p = Get-Process -Name "SystemSettings" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    Write-Host "Injecting $dll into PID $($p.Id)..."
    $res = [Injector]::Inject($p.Id, $dll)
    Write-Host ("ExitCode / HMODULE: 0x{0:X}" -f $res)
} else {
    Write-Host "SystemSettings not found"
}
