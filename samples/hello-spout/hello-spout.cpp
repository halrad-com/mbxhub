// hello-spout - an MBXHub charm that receives Spout through mbxspout.dll and sends Spout
// by linking the SDK directly.
//
// ============================================================================================
// THE TWO HALVES ARE NOT THE SAME KIND OF THING. This is the whole point of the sample.
//
//   RECEIVING goes through mbxspout.dll's PUBLISHED C ABI - LoadLibrary, six GetProcAddress
//   lookups, assert the ABI major, then open/connect/present/state/close. That is the
//   supported surface. It is what a partner integrates against, it is versioned, and the
//   major-version assert is what makes a pin bump safe. See RunReceive().
//
//   SENDING does NOT. It links the vendored spoutDX sources straight into this exe. See
//   RunSend().
//
//   THAT IS NOW A PROPERTY OF THIS SAMPLE, NOT OF THE ABI. This file was written against the
//   receive-only spec, whose section 1 ruled sending out. The operator reversed that on
//   2026-09-06 and mbxspout became a send-and-receive library: mbxspout_sender_open,
//   mbxspout_sender_open_on, mbxspout_sender_send_texture, mbxspout_sender_send_pixels,
//   mbxspout_sender_resize, mbxspout_sender_name, mbxspout_sender_state and
//   mbxspout_sender_close are all published exports today. The current contract is
//   include/mbxspout.h in the mbxspout repo, and it is normative over anything said here.
//
//   RunSend is deliberately left linking spoutDX rather than rewritten onto those exports,
//   because the contrast is the lesson: one half goes through a versioned C ABI you pin by
//   hash, the other compiles a third-party SDK into your own binary. If you want to send
//   THROUGH the ABI, the sender_* exports above are the surface to use - this file does not
//   demonstrate them.
//
//   So: if you are writing a receiver, copy RunReceive and ship mbxspout.dll. If you are
//   writing a sender, RunSend shows you the raw SDK call sequence, and mbxspout.dll is not
//   involved in it.
// ============================================================================================
//
// Modes:
//   hello-spout --send [name] [w] [h] [fps] [seconds]   send a moving picture (links spoutDX)
//   hello-spout --recv [name] [seconds]                 receive it (through mbxspout.dll)
//   hello-spout --register                              register with MBXHub and print the exchange
//   hello-spout --launched                              what MBXHub runs: register, then receive
//
// Two instances - one --send, one --recv - is a complete demo with no third-party app in it.
//
// Options: --dll <path>  --host <ip>  --port <n>
//
// Zero new dependencies: d3d11, dxgi and winhttp are in-box. The Spout sources and
// mbxspout.h come from the SIBLING mbxspout checkout (see CMakeLists.txt - SPOUT_ROOT and
// MBXSPOUT_ROOT, both overridable), and mbxspout.dll is resolved at runtime.

#include <windows.h>
#include <winhttp.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <stdio.h>
#include <stdarg.h>
#include <string.h>
#include <math.h>
#include <chrono>
#include <string>
#include <vector>

#include "SpoutDX.h"        // the SEND half links this
#include "mbxspout.h"       // the RECEIVE half only needs the header; the DLL is loaded at runtime

#pragma comment(lib, "winhttp.lib")

// ---------------------------------------------------------------- shared plumbing

static double WallSeconds()
{
    static auto start = std::chrono::steady_clock::now();
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - start).count();
}

static void Log(const char* fmt, ...)
{
    va_list ap; va_start(ap, fmt);
    printf("[%7.2f] ", WallSeconds());
    vprintf(fmt, ap);
    printf("\n");
    fflush(stdout);
    va_end(ap);
}

static void Heading(const char* text)
{
    printf("\n== %s ==\n", text);
    fflush(stdout);
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    if (msg == WM_DESTROY) { PostQuitMessage(0); return 0; }
    if (msg == WM_KEYDOWN && wp == VK_ESCAPE) { DestroyWindow(hwnd); return 0; }
    return DefWindowProcA(hwnd, msg, wp, lp);
}

// Directory this executable lives in, with a trailing backslash.
static std::string ExeDir()
{
    char buf[MAX_PATH] = { 0 };
    GetModuleFileNameA(nullptr, buf, MAX_PATH);
    char* slash = strrchr(buf, '\\');
    if (slash) *(slash + 1) = 0;
    return std::string(buf);
}

// Walk up `levels` directories from the exe.
static std::string AscendFromExe(int levels)
{
    char buf[MAX_PATH] = { 0 };
    GetModuleFileNameA(nullptr, buf, MAX_PATH);
    for (int i = 0; i <= levels; ++i) {          // the first strip removes the exe's own name
        char* slash = strrchr(buf, '\\');
        if (!slash) break;
        *slash = 0;
    }
    return std::string(buf);
}

// ---------------------------------------------------------------- the charm half
//
// The smallest honest registration: POST the manifest to /charms/register and print what
// comes back, verbatim. hello-charm (C#, in MBXHUB-Partners) is the sample that walks the
// FULL contract - approval, tickets, gated calls, events. This one registers and stops,
// because its subject is the video path, not the charm protocol.

static std::string ReadFileText(const std::string& path)
{
    FILE* f = nullptr;
    if (fopen_s(&f, path.c_str(), "rb") != 0 || !f) return std::string();
    std::string out;
    char buf[4096];
    size_t n;
    while ((n = fread(buf, 1, sizeof(buf), f)) > 0) out.append(buf, n);
    fclose(f);
    return out;
}

// JSON escape for a Windows path: backslashes and quotes. An unescaped path is the single
// most common way a hand-built JSON body fails to parse.
static std::string JsonEscape(const std::string& s)
{
    std::string out;
    for (char c : s) {
        if (c == '\\' || c == '"') { out += '\\'; out += c; }
        else out += c;
    }
    return out;
}

// manifest.json ships with "launch" holding a human note; the hub needs this exe's real
// path plus --launched. Substring surgery rather than a JSON library, deliberately - the
// sample takes no dependency it does not need, and the shape it edits is its own file.
static std::string LoadManifestWithLaunch(const std::string& manifestPath)
{
    std::string text = ReadFileText(manifestPath);
    if (text.empty()) return text;

    char exe[MAX_PATH] = { 0 };
    GetModuleFileNameA(nullptr, exe, MAX_PATH);
    const std::string launch = "\"" + JsonEscape(std::string(exe)) + " --launched\"";

    const std::string key = "\"launch\"";
    size_t k = text.find(key);
    if (k == std::string::npos) return text;
    size_t colon = text.find(':', k + key.size());
    if (colon == std::string::npos) return text;
    size_t open = text.find('"', colon);
    if (open == std::string::npos) return text;

    // find the closing quote of the existing value, honouring escapes
    size_t close = open + 1;
    while (close < text.size() && text[close] != '"') {
        if (text[close] == '\\') ++close;
        ++close;
    }
    if (close >= text.size()) return text;

    return text.substr(0, open) + launch + text.substr(close + 1);
}

static bool RegisterWithHub(const std::string& host, int port, const std::string& manifestPath)
{
    Heading("Register with MBXHub");

    const std::string body = LoadManifestWithLaunch(manifestPath);
    if (body.empty()) {
        Log("could not read manifest at '%s' - registration skipped", manifestPath.c_str());
        return false;
    }
    Log("POST http://%s:%d/charms/register", host.c_str(), port);
    printf("%s\n", body.c_str());
    fflush(stdout);

    bool ok = false;
    const std::wstring whost(host.begin(), host.end());

    HINTERNET session = WinHttpOpen(L"hello-spout/0.1", WINHTTP_ACCESS_TYPE_NO_PROXY,
                                    WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (session) {
        HINTERNET conn = WinHttpConnect(session, whost.c_str(), (INTERNET_PORT)port, 0);
        if (conn) {
            HINTERNET req = WinHttpOpenRequest(conn, L"POST", L"/charms/register", nullptr,
                                               WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, 0);
            if (req) {
                const wchar_t* hdr = L"Content-Type: application/json\r\n";
                if (WinHttpSendRequest(req, hdr, (DWORD)-1, (LPVOID)body.data(), (DWORD)body.size(),
                                       (DWORD)body.size(), 0) &&
                    WinHttpReceiveResponse(req, nullptr)) {

                    DWORD status = 0, len = sizeof(status);
                    WinHttpQueryHeaders(req, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                                        WINHTTP_HEADER_NAME_BY_INDEX, &status, &len, WINHTTP_NO_HEADER_INDEX);

                    std::string answer;
                    for (;;) {
                        DWORD avail = 0;
                        if (!WinHttpQueryDataAvailable(req, &avail) || avail == 0) break;
                        std::vector<char> chunk(avail);
                        DWORD read = 0;
                        if (!WinHttpReadData(req, chunk.data(), avail, &read) || read == 0) break;
                        answer.append(chunk.data(), read);
                    }

                    Log("HTTP %lu", status);
                    printf("%s\n", answer.c_str());
                    fflush(stdout);

                    if (status == 200) {
                        ok = true;
                        Log("registered. A person approves the charm in MBXHub; placement appears after that.");
                    } else if (status == 403) {
                        Log("403 - /charms/register answers only callers on the MusicBee machine. Run this there.");
                    } else {
                        Log("registration refused. ID_CLAIMED = pick your own id; UNKNOWN_SCOPE = see GET /charms/capabilities.");
                    }
                } else {
                    Log("no answer from %s:%d (WinHttp error %lu). Is MBXHub running?", host.c_str(), port, GetLastError());
                }
                WinHttpCloseHandle(req);
            }
            WinHttpCloseHandle(conn);
        }
        WinHttpCloseHandle(session);
    }
    return ok;
}

// ---------------------------------------------------------------- the SEND half
//
// Links spoutDX directly. mbxspout.dll is NOT involved in anything below this line.

// A picture that is obviously moving from across a room: diagonal bars that travel, a band
// whose colour cycles, and a square that walks the frame. CPU-drawn into BGRA, which needs
// no shader, no .fx file and no compile step - a sample should build with the toolchain it
// already has.
static void DrawFrame(std::vector<uint8_t>& px, unsigned w, unsigned h, long frame)
{
    const unsigned pitch = w * 4;
    const int      slide = (int)(frame * 3);

    for (unsigned y = 0; y < h; ++y) {
        uint8_t* row = px.data() + (size_t)y * pitch;
        for (unsigned x = 0; x < w; ++x) {
            uint8_t* p = row + (size_t)x * 4;
            const bool bar = (((int)x + (int)y + slide) / 40) % 2 == 0;
            uint8_t v = bar ? 40 : 65;
            p[0] = v; p[1] = v; p[2] = v; p[3] = 255;       // B G R A
        }
    }

    // a band across the middle whose colour cycles, so a frozen receiver is obvious
    const double t   = frame / 60.0;
    const uint8_t cb = (uint8_t)(127 + 127 * sin(t * 2.0));
    const uint8_t cg = (uint8_t)(127 + 127 * sin(t * 2.0 + 2.09));
    const uint8_t cr = (uint8_t)(127 + 127 * sin(t * 2.0 + 4.18));
    const unsigned bandTop = h / 2 - h / 12, bandBot = h / 2 + h / 12;
    for (unsigned y = bandTop; y < bandBot && y < h; ++y) {
        uint8_t* row = px.data() + (size_t)y * pitch;
        for (unsigned x = 0; x < w; ++x) {
            uint8_t* p = row + (size_t)x * 4;
            p[0] = cb; p[1] = cg; p[2] = cr; p[3] = 255;
        }
    }

    // a square that walks, so you can see motion even if the band is off-screen
    const unsigned side = h / 8;
    const unsigned sx = (unsigned)((frame * 4) % (long)(w > side ? w - side : 1));
    const unsigned sy = h / 8;
    for (unsigned y = sy; y < sy + side && y < h; ++y) {
        uint8_t* row = px.data() + (size_t)y * pitch;
        for (unsigned x = sx; x < sx + side && x < w; ++x) {
            uint8_t* p = row + (size_t)x * 4;
            p[0] = 245; p[1] = 245; p[2] = 245; p[3] = 255;
        }
    }
}

static int RunSend(const char* name, unsigned w, unsigned h, int fps, int seconds)
{
    Heading("Send - linking spoutDX directly (mbxspout.dll is NOT involved)");

    spoutDX sender;
    if (!sender.OpenDirectX11())      { Log("OpenDirectX11 failed"); return 2; }
    sender.SetSenderFormat(DXGI_FORMAT_B8G8R8A8_UNORM);
    if (!sender.SetSenderName(name))  { Log("SetSenderName('%s') failed", name); return 2; }

    ID3D11Device*        dev = sender.GetDX11Device();
    ID3D11DeviceContext* ctx = sender.GetDX11Context();

    D3D11_TEXTURE2D_DESC td = {};
    td.Width = w; td.Height = h; td.MipLevels = 1; td.ArraySize = 1;
    td.Format = DXGI_FORMAT_B8G8R8A8_UNORM; td.SampleDesc.Count = 1;
    td.Usage = D3D11_USAGE_DEFAULT;
    td.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    ID3D11Texture2D* tex = nullptr;
    if (FAILED(dev->CreateTexture2D(&td, nullptr, &tex))) { Log("CreateTexture2D %ux%u failed", w, h); return 2; }

    Log("sending '%s' at %ux%u, %d fps, for %d s (pid %lu)", name, w, h, fps, seconds, GetCurrentProcessId());
    Log("run the other half:  hello-spout --recv %s", name);

    std::vector<uint8_t> px((size_t)w * h * 4);
    long   frames = 0;
    double t0 = WallSeconds(), lastReport = t0;

    while (WallSeconds() - t0 < seconds) {
        DrawFrame(px, w, h, frames);
        ctx->UpdateSubresource(tex, 0, nullptr, px.data(), w * 4, 0);
        if (!sender.SendTexture(tex)) Log("SendTexture failed at frame %ld", frames);
        frames++;
        sender.HoldFps(fps);
        if (WallSeconds() - lastReport >= 2.0) {
            // Counted here, not read from sender.GetFps(). That returns a frame-count-derived
            // rate, and Spout's frame counting is off unless HKCU\Software\Leading Edge\Spout\
            // Framecount has been written by Spout's own settings app - the same measurement
            // that shapes the DLL's present path (spec section 3.1). Asked for 30 fps it
            // reports 60.0. A sample must not print a number it knows is wrong.
            const double elapsed = WallSeconds() - t0;
            Log("sent %ld frames (%.1f fps)", frames, elapsed > 0 ? frames / elapsed : 0.0);
            lastReport = WallSeconds();
        }
    }

    Log("done - %ld frames in %.1f s", frames, WallSeconds() - t0);
    tex->Release();
    sender.ReleaseSender();
    sender.CloseDirectX11();
    return 0;
}

// ---------------------------------------------------------------- the RECEIVE half
//
// Everything below goes through mbxspout.dll's published C ABI. This is the part to copy.

typedef uint32_t           (__cdecl *pfn_abi_version)(void);
typedef mbxspout_receiver* (__cdecl *pfn_open)(void*);
typedef int32_t            (__cdecl *pfn_connect)(mbxspout_receiver*, const char*);
typedef int32_t            (__cdecl *pfn_present)(mbxspout_receiver*, uint32_t*, uint32_t*, double*);
typedef int32_t            (__cdecl *pfn_state)(mbxspout_receiver*);
typedef void               (__cdecl *pfn_close)(mbxspout_receiver*);

struct MbxSpoutApi {
    HMODULE         dll = nullptr;
    pfn_abi_version abi_version = nullptr;
    pfn_open        open = nullptr;
    pfn_connect     connect = nullptr;
    pfn_present     present = nullptr;
    pfn_state       state = nullptr;
    pfn_close       close = nullptr;
};

static const char* StateText(int32_t s)
{
    switch (s) {
    case MBXSPOUT_STATE_LIVE:          return "live";
    case MBXSPOUT_STATE_NO_SENDER:     return "waiting for the sender";
    case MBXSPOUT_STATE_DEVICE_LOST:   return "video device lost - recovering";
    case MBXSPOUT_STATE_HANDLE_FAILED: return "sender found, but its texture will not open";
    default:                           return "unknown";
    }
}

// Load the DLL and resolve the six exports. This is the sequence a consumer performs once,
// at startup, and every failure in it is a legible message rather than a crash.
static bool LoadApi(MbxSpoutApi& api, const std::string& dllPath)
{
    api.dll = LoadLibraryA(dllPath.c_str());
    if (!api.dll) {
        Log("LoadLibrary('%s') failed (%lu)", dllPath.c_str(), GetLastError());
        return false;
    }
    api.abi_version = (pfn_abi_version)GetProcAddress(api.dll, "mbxspout_abi_version");
    api.open        = (pfn_open)       GetProcAddress(api.dll, "mbxspout_open");
    api.connect     = (pfn_connect)    GetProcAddress(api.dll, "mbxspout_connect");
    api.present     = (pfn_present)    GetProcAddress(api.dll, "mbxspout_present");
    api.state       = (pfn_state)      GetProcAddress(api.dll, "mbxspout_state");
    api.close       = (pfn_close)      GetProcAddress(api.dll, "mbxspout_close");
    if (!api.abi_version || !api.open || !api.connect || !api.present || !api.state || !api.close) {
        Log("that file loaded but is not mbxspout.dll - one or more exports did not resolve");
        FreeLibrary(api.dll); api.dll = nullptr;
        return false;
    }

    // Assert the MAJOR at load. This is the whole reason the ABI carries a version: a pin
    // bump that changed the contract fails HERE, once, legibly - not at the first present
    // with a corrupted frame or a wrong-sized struct.
    const uint32_t v = api.abi_version();
    Log("mbxspout.dll ABI 0x%08X (major %u, minor %u)", v, v >> 16, v & 0xFFFF);
    if ((v >> 16) != (uint32_t)MBXSPOUT_ABI_MAJOR) {
        Log("this build was compiled against ABI major %u and cannot use major %u. Refusing.",
            (unsigned)MBXSPOUT_ABI_MAJOR, v >> 16);
        FreeLibrary(api.dll); api.dll = nullptr;
        return false;
    }
    return true;
}

static int RunReceive(const char* name, int seconds, const std::string& dllPath)
{
    Heading("Receive - through mbxspout.dll's published C ABI");

    MbxSpoutApi api;
    if (!LoadApi(api, dllPath)) return 2;

    WNDCLASSA wc = {};
    wc.lpfnWndProc   = WndProc;
    wc.hInstance     = GetModuleHandleA(nullptr);
    wc.lpszClassName = "hello_spout";
    wc.hCursor       = LoadCursor(nullptr, IDC_ARROW);
    RegisterClassA(&wc);
    HWND hwnd = CreateWindowA("hello_spout", "hello-spout", WS_OVERLAPPEDWINDOW | WS_VISIBLE,
                              120, 120, 960, 540, nullptr, nullptr, wc.hInstance, nullptr);
    if (!hwnd) { Log("CreateWindow failed"); FreeLibrary(api.dll); return 2; }

    mbxspout_receiver* h = api.open(hwnd);
    if (!h) { Log("mbxspout_open failed - the DLL logged the reason to the debug output"); DestroyWindow(hwnd); FreeLibrary(api.dll); return 2; }
    if (!api.connect(h, name)) { Log("mbxspout_connect('%s') refused", name); api.close(h); DestroyWindow(hwnd); FreeLibrary(api.dll); return 2; }

    Log("watching for sender '%s'. Esc closes. (pid %lu)", name, GetCurrentProcessId());
    Log("no sender yet is a NORMAL state, not an error - start the other half and it connects on its own.");

    int32_t shown = -1;
    long    presented = 0;
    double  cpuSum = 0;
    double  t0 = WallSeconds(), lastReport = t0;
    bool    quit = false;

    while (!quit && (seconds <= 0 || WallSeconds() - t0 < seconds)) {
        MSG m;
        while (PeekMessageA(&m, nullptr, 0, 0, PM_REMOVE)) {
            if (m.message == WM_QUIT) quit = true;
            TranslateMessage(&m); DispatchMessageA(&m);
        }
        if (quit) break;

        uint32_t w = 0, ht = 0;
        double   cpuMs = 0;
        const int32_t rc = api.present(h, &w, &ht, &cpuMs);
        const int32_t st = api.state(h);

        if (rc == 1) { presented++; cpuSum += cpuMs; }
        else if (rc == 0) Sleep(1);          // nothing to present: do not spin

        // Degrade to something a person can see. The DLL owns the swapchain, so the title
        // bar is where a state that has no picture gets said out loud.
        if (st != shown) {
            char title[256];
            if (st == MBXSPOUT_STATE_LIVE) sprintf_s(title, "hello-spout - %s - %ux%u", StateText(st), w, ht);
            else                           sprintf_s(title, "hello-spout - %s '%s'", StateText(st), name);
            SetWindowTextA(hwnd, title);
            Log("state: %s", StateText(st));
            shown = st;
        }

        // Spec section 3.3: the DLL never re-creates a lost device. Recovery is the CALLER's,
        // because the caller owns the window and has to re-create the swapchain against it.
        // This is that half, and it is why the DLL does not do it quietly for you.
        if (st == MBXSPOUT_STATE_DEVICE_LOST) {
            Log("device lost - closing and re-opening, which is the caller's job, not the DLL's");
            api.close(h);
            h = api.open(hwnd);
            if (!h) { Log("re-open failed; giving up"); break; }
            api.connect(h, name);
            shown = -1;
            continue;
        }

        if (WallSeconds() - lastReport >= 2.0) {
            if (presented > 0)
                Log("%ld frames, %.3f ms CPU per frame", presented, cpuSum / presented);
            lastReport = WallSeconds();
        }
    }

    if (presented > 0) Log("done - %ld frames, %.3f ms CPU per frame", presented, cpuSum / presented);
    else               Log("done - nothing was ever presented (no sender named '%s' appeared)", name);

    api.close(h);
    DestroyWindow(hwnd);
    FreeLibrary(api.dll);
    return 0;
}

// ---------------------------------------------------------------- main

static void Usage()
{
    printf(
        "hello-spout - receives Spout through mbxspout.dll, sends Spout by linking spoutDX.\n"
        "\n"
        "  hello-spout --send [name] [w] [h] [fps] [seconds]   default: HelloSpout 1280 720 30 120\n"
        "  hello-spout --recv [name] [seconds]                 default: HelloSpout, runs until Esc\n"
        "  hello-spout --register                              register with MBXHub, print the exchange\n"
        "  hello-spout --launched                              what MBXHub runs: register, then receive\n"
        "\n"
        "  --dll <path>    mbxspout.dll to load (default: beside this exe, then the sibling\n"
        "                  mbxspout checkout's build\\Release)\n"
        "  --host <ip>     MBXHub host for --register (default 127.0.0.1)\n"
        "  --port <n>      MBXHub port for --register (default 8080)\n"
        "\n"
        "Receiving uses the published C ABI. Sending here links spoutDX directly - a choice of\n"
        "this sample, not a limit of the ABI: mbxspout publishes sender_* exports too. See the\n"
        "comment at the top of hello-spout.cpp.\n");
}

int main(int argc, char** argv)
{
    std::string dllPath, host = "127.0.0.1";
    int port = 8080;

    // pull the options out first, so the positional arguments stay simple
    std::vector<char*> pos;
    for (int i = 1; i < argc; ++i) {
        if (strcmp(argv[i], "--dll") == 0 && i + 1 < argc)       dllPath = argv[++i];
        else if (strcmp(argv[i], "--host") == 0 && i + 1 < argc) host    = argv[++i];
        else if (strcmp(argv[i], "--port") == 0 && i + 1 < argc) port    = atoi(argv[++i]);
        else pos.push_back(argv[i]);
    }

    if (dllPath.empty()) {
        // Beside the exe first - that is how it ships. Then the SIBLING mbxspout checkout's
        // build output, which is what makes the sample runnable straight after mbxspout's
        // build-spout.cmd.
        //
        // Ascend 5, not 4: this used to live in the mbxspout repo, where four levels up from
        // build\Release landed on that repo's root and its own build output was right there.
        // The sample moved to mbxhub in 7c9a085 and the arithmetic came with it, so the
        // fallback has been resolving to mbxhub\build\Release\mbxspout.dll - a path that has
        // never existed. Five levels reaches the directory holding BOTH checkouts, and the
        // sibling is named explicitly, matching MBXSPOUT_ROOT in CMakeLists.txt.
        const std::string beside = ExeDir() + "mbxspout.dll";
        if (GetFileAttributesA(beside.c_str()) != INVALID_FILE_ATTRIBUTES) dllPath = beside;
        else dllPath = AscendFromExe(5) + "\\mbxspout\\build\\Release\\mbxspout.dll";
    }

    const std::string mode = pos.empty() ? std::string() : std::string(pos[0]);
    auto arg = [&](size_t i, const char* dflt) -> const char* {
        return pos.size() > i ? pos[i] : dflt;
    };

    if (mode == "--send") {
        return RunSend(arg(1, "HelloSpout"),
                       (unsigned)atoi(arg(2, "1280")), (unsigned)atoi(arg(3, "720")),
                       atoi(arg(4, "30")), atoi(arg(5, "120")));
    }
    if (mode == "--recv") {
        return RunReceive(arg(1, "HelloSpout"), atoi(arg(2, "0")), dllPath);
    }
    if (mode == "--register") {
        return RegisterWithHub(host, port, ExeDir() + "manifest.json") ? 0 : 1;
    }
    if (mode == "--launched") {
        // MBXHub started us. Register so the charm has a placement, then show the picture.
        // Registration failing is not fatal: the video half is worth watching either way.
        RegisterWithHub(host, port, ExeDir() + "manifest.json");
        return RunReceive(arg(1, "HelloSpout"), 0, dllPath);
    }

    Usage();
    return 64;
}
