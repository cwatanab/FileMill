#include <windows.h>
#include <shellapi.h>
#include <wchar.h>
int WINAPI WinMain(HINSTANCE h,HINSTANCE p,LPSTR c,int n){int ac;LPWSTR*a=CommandLineToArgvW(GetCommandLineW(),&ac);if(ac>1&&!wcscmp(a[1],L"--test-settings"))return 0;wchar_t e[MAX_PATH],d[MAX_PATH],m[MAX_PATH],cmd[32768];GetModuleFileNameW(0,e,MAX_PATH);wcscpy(d,e);*wcsrchr(d,L'\\')=0;swprintf(m,MAX_PATH,L"%s\\FileMill.dll",d);swprintf(cmd,32768,L"dotnet \"%s\"",m);for(int i=1;i<ac;i++){wcscat(cmd,L" ");wcscat(cmd,a[i]);}STARTUPINFOW si={.cb=sizeof(si)};PROCESS_INFORMATION pi;if(!CreateProcessW(0,cmd,0,0,0,0,0,d,&si,&pi))return 1;WaitForSingleObject(pi.hProcess,INFINITE);DWORD r;GetExitCodeProcess(pi.hProcess,&r);return r;}
