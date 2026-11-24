#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>

enum ButtonRole { Quiet, Primary, Minimize, Close, Link };

typedef struct {
    COLORREF canvas, surface, ink, muted, primary, hover, onPrimary;
    COLORREF selected, border, borderStrong, danger, dangerBg;
} Palette;

typedef struct {
    int titleHeight, buttonExclusion;
} FrameStyle;

typedef struct {
    enum ButtonRole role;
    int diameter, focusInset;
    BOOL hot;
} ButtonStyle;

static Palette colors;
static const WCHAR buttonProperty[] = L"Core30.Setup.Button";

void WINAPI Core30SetPalette(const Palette *palette)
{
    colors = *palette;
}

static void PreferCompositedCorners(HWND window)
{
    typedef HRESULT (WINAPI *SetAttribute)(HWND, DWORD, LPCVOID, DWORD);
    const DWORD cornerPreference = 33;
    const DWORD round = 2;
    HMODULE dwm = LoadLibraryExW(L"dwmapi.dll", NULL, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!dwm)
        return;
    SetAttribute setAttribute = (SetAttribute)GetProcAddress(dwm, "DwmSetWindowAttribute");
    if (setAttribute)
        setAttribute(window, cornerPreference, &round, sizeof(round));
    FreeLibrary(dwm);
}

static LRESULT CALLBACK FrameProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam,
                                  UINT_PTR id, DWORD_PTR data)
{
    FrameStyle *style = (FrameStyle *)data;
    if (message == WM_NCHITTEST) {
        RECT client;
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        if (IsWindowEnabled(window) && ScreenToClient(window, &point) && GetClientRect(window, &client)) {
            if (point.x >= 0 && point.x < client.right - style->buttonExclusion &&
                point.y >= 0 && point.y < style->titleHeight)
                return HTCAPTION;
        }
    }
    if (message == WM_NCLBUTTONDOWN && wParam == HTCAPTION)
        return DefWindowProcW(window, WM_SYSCOMMAND, SC_MOVE | HTCAPTION, lParam);
    if (message == WM_NCDESTROY) {
        RemoveWindowSubclass(window, FrameProc, id);
        HeapFree(GetProcessHeap(), 0, style);
    }
    return DefSubclassProc(window, message, wParam, lParam);
}

BOOL WINAPI Core30BindFrame(HWND window, int titleHeight, int exclusion, int diameter)
{
    FrameStyle *style = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(*style));
    if (!style)
        return FALSE;
    style->titleHeight = titleHeight;
    style->buttonExclusion = exclusion;
    if (!SetWindowSubclass(window, FrameProc, 1, (DWORD_PTR)style)) {
        HeapFree(GetProcessHeap(), 0, style);
        return FALSE;
    }
    if (diameter > 0)
        PreferCompositedCorners(window);
    return TRUE;
}

static LRESULT CALLBACK CaptionProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam,
                                    UINT_PTR id, DWORD_PTR data)
{
    (void)data;
    if (message == WM_NCHITTEST)
        return HTTRANSPARENT;
    if (message == WM_NCDESTROY)
        RemoveWindowSubclass(window, CaptionProc, id);
    return DefSubclassProc(window, message, wParam, lParam);
}

BOOL WINAPI Core30BindCaption(HWND window)
{
    return SetWindowSubclass(window, CaptionProc, 4, 0);
}

static void DrawButton(const DRAWITEMSTRUCT *item, const ButtonStyle *style)
{
    RECT textRect = item->rcItem;
    const BOOL pressed = (item->itemState & ODS_SELECTED) != 0;
    COLORREF face = colors.surface, edge = colors.borderStrong, ink = colors.ink;
    WCHAR label[256];
    GetWindowTextW(item->hwndItem, label, ARRAYSIZE(label));

    if (style->role == Primary) {
        face = pressed || style->hot ? colors.hover : colors.primary;
        edge = face;
        ink = colors.onPrimary;
    } else if (style->role == Link) {
        face = style->hot ? colors.selected : colors.canvas;
        edge = face;
        ink = colors.primary;
    } else if (style->role == Close || style->role == Minimize) {
        face = colors.canvas;
        label[0] = style->role == Close ? L'\x00d7' : L'\x2212';
        label[1] = L'\0';
        if (pressed || style->hot) {
            face = style->role == Close ? colors.dangerBg : colors.selected;
            if (style->role == Close)
                ink = colors.danger;
        }
        edge = face;
    } else if (pressed || style->hot) {
        face = colors.selected;
    }
    if (item->itemState & ODS_DISABLED) {
        face = style->role == Primary ? colors.selected : colors.canvas;
        ink = colors.muted;
        edge = colors.border;
    }

    const HDC dc = item->hDC;
    const int saved = SaveDC(dc);
    HBRUSH backdrop = CreateSolidBrush(colors.canvas);
    FillRect(dc, &textRect, backdrop);
    DeleteObject(backdrop);
    HBRUSH background = CreateSolidBrush(face);
    HPEN stroke = CreatePen(PS_SOLID, 1, edge);
    HGDIOBJ oldBrush = SelectObject(dc, background);
    HGDIOBJ oldPen = SelectObject(dc, stroke);
    RoundRect(dc, textRect.left, textRect.top, textRect.right, textRect.bottom,
              style->diameter, style->diameter);
    SelectObject(dc, oldPen);
    SelectObject(dc, oldBrush);
    DeleteObject(stroke);
    DeleteObject(background);

    HFONT font = (HFONT)SendMessageW(item->hwndItem, WM_GETFONT, 0, 0);
    if (font)
        SelectObject(dc, font);
    SetBkMode(dc, TRANSPARENT);
    SetTextColor(dc, ink);
    UINT format = DT_CENTER | DT_VCENTER | DT_SINGLELINE;
    if (item->itemState & ODS_NOACCEL)
        format |= DT_HIDEPREFIX;
    DrawTextW(dc, label, -1, &textRect, format);
    if ((item->itemState & (ODS_FOCUS | ODS_NOFOCUSRECT)) == ODS_FOCUS) {
        InflateRect(&textRect, -style->focusInset, -style->focusInset);
        DrawFocusRect(dc, &textRect);
    }
    RestoreDC(dc, saved);
}

static LRESULT CALLBACK ParentProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam,
                                   UINT_PTR id, DWORD_PTR data)
{
    (void)data;
    if (message == WM_DRAWITEM && lParam) {
        const DRAWITEMSTRUCT *item = (const DRAWITEMSTRUCT *)lParam;
        if (item->CtlType == ODT_BUTTON) {
            ButtonStyle *style = GetPropW(item->hwndItem, buttonProperty);
            if (style) {
                DrawButton(item, style);
                return TRUE;
            }
        }
    }
    if (message == WM_NCDESTROY)
        RemoveWindowSubclass(window, ParentProc, id);
    return DefSubclassProc(window, message, wParam, lParam);
}

static LRESULT CALLBACK ButtonProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam,
                                   UINT_PTR id, DWORD_PTR data)
{
    ButtonStyle *style = (ButtonStyle *)data;
    if (message == BM_SETSTYLE)
        wParam = (wParam & ~(WPARAM)BS_TYPEMASK) | BS_OWNERDRAW;
    if (message == WM_MOUSEMOVE && !style->hot) {
        TRACKMOUSEEVENT track = { sizeof(track), TME_LEAVE, window, 0 };
        style->hot = TrackMouseEvent(&track);
        InvalidateRect(window, NULL, FALSE);
    }
    if (message == WM_MOUSELEAVE) {
        style->hot = FALSE;
        InvalidateRect(window, NULL, FALSE);
    }
    if (message == WM_NCDESTROY) {
        RemovePropW(window, buttonProperty);
        RemoveWindowSubclass(window, ButtonProc, id);
        HeapFree(GetProcessHeap(), 0, style);
    }
    return DefSubclassProc(window, message, wParam, lParam);
}

BOOL WINAPI Core30BindButton(HWND window, int role, int diameter, int focusInset)
{
    ButtonStyle *style = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(*style));
    if (!style)
        return FALSE;
    style->role = (enum ButtonRole)role;
    style->diameter = diameter;
    style->focusInset = focusInset;
    if (!SetWindowSubclass(GetParent(window), ParentProc, 2, 0) ||
        !SetPropW(window, buttonProperty, style) ||
        !SetWindowSubclass(window, ButtonProc, 3, (DWORD_PTR)style)) {
        RemovePropW(window, buttonProperty);
        HeapFree(GetProcessHeap(), 0, style);
        return FALSE;
    }
    SetWindowLongW(window, GWL_STYLE, (GetWindowLongW(window, GWL_STYLE) & ~BS_TYPEMASK) | BS_OWNERDRAW);
    InvalidateRect(window, NULL, FALSE);
    return TRUE;
}

HANDLE WINAPI Core30StartSetupWorker(const WCHAR *exe, const WCHAR *args, DWORD *error)
{
    size_t length = lstrlenW(exe) + (size_t)lstrlenW(args) + 4;
    HANDLE heap = GetProcessHeap();
    WCHAR *command = HeapAlloc(heap, 0, length * sizeof(WCHAR));
    if (!command) {
        *error = ERROR_NOT_ENOUGH_MEMORY;
        return NULL;
    }
    lstrcpyW(command, L"\"");
    lstrcatW(command, exe);
    lstrcatW(command, L"\" ");
    lstrcatW(command, args);
    STARTUPINFOW startup = { 0 };
    PROCESS_INFORMATION child;
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESHOWWINDOW;
    startup.wShowWindow = SW_HIDE;
    BOOL started = CreateProcessW(exe, command, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &startup, &child);
    *error = started ? ERROR_SUCCESS : GetLastError();
    HeapFree(heap, 0, command);
    if (!started)
        return NULL;
    CloseHandle(child.hThread);
    return child.hProcess;
}

BOOL WINAPI Core30WaitSetupWorker(HANDLE process, DWORD *exitCode)
{
    // Only post-install uses this wait. The native installing page has no active
    // navigation actions, but its caption and painting must still work.
    BOOL quitting = FALSE;
    BOOL complete = FALSE;
    int quitCode = 0;
    for (;;) {
        DWORD result = MsgWaitForMultipleObjectsEx(1, &process, INFINITE, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
        if (result != WAIT_OBJECT_0 + 1) {
            complete = result == WAIT_OBJECT_0;
            break;
        }
        MSG message;
        while (PeekMessageW(&message, NULL, 0, 0, PM_REMOVE)) {
            if (message.message == WM_QUIT) {
                quitting = TRUE;
                quitCode = (int)message.wParam;
                continue;
            }
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
    }
    if (quitting)
        PostQuitMessage(quitCode);
    return complete && GetExitCodeProcess(process, exitCode);
}
