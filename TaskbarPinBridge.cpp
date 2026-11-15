#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <sddl.h>
#include <shlobj.h>
#include <strsafe.h>

#pragma comment(lib, "kernel32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")

// CLSID_PinManager: {A5C8D635-B4ED-452B-8109-9501781096D1}
static const GUID CLSID_PinManager = 
    { 0xA5C8D635, 0xB4ED, 0x452B, { 0x81, 0x09, 0x95, 0x01, 0x78, 0x10, 0x96, 0xD1 } };

// IID_IPinManagerInterop2: {87D9E034-56D0-4F8C-BE59-997B01754710}
static const GUID IID_IPinManagerInterop2 = 
    { 0x87D9E034, 0x56D0, 0x4F8C, { 0xBE, 0x59, 0x99, 0x7B, 0x01, 0x75, 0x47, 0x10 } };

// ypx: vtable hooking shell taskband interfaces - pure black magic, please appreciate the suffering ;_)
typedef HRESULT (__stdcall *PinMethod)(void* thisPtr, PIDLIST_ABSOLUTE pidl, int type);
typedef HRESULT (__stdcall *UnpinMethod)(void* thisPtr, PIDLIST_ABSOLUTE pidl, int type);
typedef ULONG (__stdcall *ReleaseMethod)(void* thisPtr);

static HRESULT DoPin(const WCHAR* pszPath)
{
    PIDLIST_ABSOLUTE pidl = NULL;
    HRESULT hr = SHParseDisplayName(pszPath, NULL, &pidl, 0, NULL);
    if (FAILED(hr) || !pidl)
    {
        return hr;
    }

    void* pPinManager = NULL;
    hr = CoCreateInstance(CLSID_PinManager, NULL, CLSCTX_INPROC_SERVER, IID_IPinManagerInterop2, &pPinManager);
    if (SUCCEEDED(hr) && pPinManager)
    {
        void** vtbl = *(void***)pPinManager;
        // Slot 4: Pin (offset 0x20 in x64)
        PinMethod pinFunc = (PinMethod)vtbl[4];
        hr = pinFunc(pPinManager, pidl, 0x19);

        // Slot 2: Release
        ReleaseMethod relFunc = (ReleaseMethod)vtbl[2];
        relFunc(pPinManager);
    }

    CoTaskMemFree(pidl);
    return hr;
}

static HRESULT DoUnpin(const WCHAR* pszPath)
{
    PIDLIST_ABSOLUTE pidl = NULL;
    HRESULT hr = SHParseDisplayName(pszPath, NULL, &pidl, 0, NULL);
    if (FAILED(hr) || !pidl)
    {
        return hr;
    }

    void* pPinManager = NULL;
    hr = CoCreateInstance(CLSID_PinManager, NULL, CLSCTX_INPROC_SERVER, IID_IPinManagerInterop2, &pPinManager);
    if (SUCCEEDED(hr) && pPinManager)
    {
        void** vtbl = *(void***)pPinManager;
        // Slot 9: Unpin (offset 0x48 in x64)
        UnpinMethod unpinFunc = (UnpinMethod)vtbl[9];
        hr = unpinFunc(pPinManager, pidl, 0x0B);

        // Slot 2: Release
        ReleaseMethod relFunc = (ReleaseMethod)vtbl[2];
        relFunc(pPinManager);
    }

    CoTaskMemFree(pidl);
    return hr;
}

static DWORD WINAPI PipeServerThread(LPVOID lpParam)
{
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    PSECURITY_DESCRIPTOR pSD = NULL;
    ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:(A;;GA;;;WD)", SDDL_REVISION_1, &pSD, NULL);
    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(sa);
    sa.lpSecurityDescriptor = pSD;
    sa.bInheritHandle = FALSE;

    while (TRUE)
    {
        HANDLE hPipe = CreateNamedPipeW(
            L"\\\\.\\pipe\\TaskTilePinBridge",
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            2048,
            2048,
            0,
            &sa
        );

        if (hPipe == INVALID_HANDLE_VALUE)
        {
            Sleep(1000);
            continue;
        }

        BOOL connected = ConnectNamedPipe(hPipe, NULL) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
        if (connected)
        {
            WCHAR buffer[1024] = { 0 };
            DWORD bytesRead = 0;
            if (ReadFile(hPipe, buffer, sizeof(buffer) - sizeof(WCHAR), &bytesRead, NULL) && bytesRead > 0)
            {
                buffer[bytesRead / sizeof(WCHAR)] = L'\0';

                // Strip \r or \n
                for (int i = 0; buffer[i]; i++)
                {
                    if (buffer[i] == L'\r' || buffer[i] == L'\n')
                    {
                        buffer[i] = L'\0';
                        break;
                    }
                }

                WCHAR reply[128] = L"ERR:UNKNOWN";
                if (wcscmp(buffer, L"PING") == 0)
                {
                    StringCchCopyW(reply, 128, L"PONG");
                }
                else if (wcsncmp(buffer, L"PIN|", 4) == 0)
                {
                    const WCHAR* path = buffer + 4;
                    HRESULT hr = DoPin(path);
                    if (SUCCEEDED(hr))
                    {
                        StringCchCopyW(reply, 128, L"OK");
                    }
                    else
                    {
                        StringCchPrintfW(reply, 128, L"ERR:0x%08X", hr);
                    }
                }
                else if (wcsncmp(buffer, L"UNPIN|", 6) == 0)
                {
                    const WCHAR* path = buffer + 6;
                    HRESULT hr = DoUnpin(path);
                    if (SUCCEEDED(hr))
                    {
                        StringCchCopyW(reply, 128, L"OK");
                    }
                    else
                    {
                        StringCchPrintfW(reply, 128, L"ERR:0x%08X", hr);
                    }
                }

                DWORD bytesWritten = 0;
                DWORD replyLen = (DWORD)wcslen(reply) * sizeof(WCHAR);
                WriteFile(hPipe, reply, replyLen, &bytesWritten, NULL);
                FlushFileBuffers(hPipe);
            }
            DisconnectNamedPipe(hPipe);
        }
        CloseHandle(hPipe);
    }

    if (pSD) LocalFree(pSD);
    CoUninitialize();
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    if (ul_reason_for_call == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);
        HANDLE hThread = CreateThread(NULL, 0, PipeServerThread, NULL, 0, NULL);
        if (hThread) CloseHandle(hThread);
    }
    return TRUE;
}
