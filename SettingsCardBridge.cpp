#include <windows.h>
#undef GetCurrentTime
#include <string>
#include <vector>
#include <chrono>
#include <shellapi.h>
#pragma comment(lib, "shell32.lib")

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.ApplicationModel.Core.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Text.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Controls.Primitives.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Media.Animation.h>
#include <winrt/Windows.UI.Xaml.Input.h>

namespace winrt {
    using namespace Windows::Foundation;
    using namespace Windows::Foundation::Collections;
    using namespace Windows::Foundation::Numerics;
    using namespace Windows::ApplicationModel::Core;
    using namespace Windows::System;
    using namespace Windows::UI;
    using namespace Windows::UI::Core;
    using namespace Windows::UI::Text;
    using namespace Windows::UI::Composition;
    using namespace Windows::UI::Xaml;
    using namespace Windows::UI::Xaml::Hosting;
    using namespace Windows::UI::Xaml::Controls;
    using namespace Windows::UI::Xaml::Controls::Primitives;
    using namespace Windows::UI::Xaml::Media;
    using namespace Windows::UI::Xaml::Media::Animation;
    using namespace Windows::UI::Xaml::Input;
}

#include <fstream>

static HMODULE g_hModule = nullptr;
static winrt::IInspectable g_categoryPage = nullptr;
static winrt::IInspectable g_l2Page = nullptr;
static winrt::IInspectable g_originalBreadcrumbBar = nullptr;
static winrt::DispatcherTimer g_timer = nullptr;
static bool g_isOnAppGroupsPage = false;
static winrt::event_token g_backToken{};

// ypx was TORTURED to insanity here ;_) injecting into Windows Settings without crashing systemsettings.exe took weeks
static void LogBridge(std::string const& s) {
    OutputDebugStringA(("[TaskTileBridge] " + s + "\n").c_str());
    try {
        std::ofstream f2("C:\\Users\\yassinh\\AppData\\Local\\Packages\\windows.immersivecontrolpanel_cw5n1h2txyewy\\LocalState\\bridge_debug.log", std::ios::app);
        f2 << s << "\n";
    } catch (...) {}
}

static winrt::DependencyObject FindDescendantByName(winrt::DependencyObject const& root, std::wstring const& name) {
    if (!root) return nullptr;
    if (auto fe = root.try_as<winrt::FrameworkElement>()) {
        if (fe.Name() == name) return root;
    }
    int count = winrt::VisualTreeHelper::GetChildrenCount(root);
    for (int i = 0; i < count; i++) {
        auto child = winrt::VisualTreeHelper::GetChild(root, i);
        auto found = FindDescendantByName(child, name);
        if (found) return found;
    }
    return nullptr;
}

static winrt::DependencyObject FindFirstDescendantByClass(winrt::DependencyObject const& root, std::string const& className) {
    if (!root) return nullptr;
    std::string currentClass = winrt::to_string(winrt::get_class_name(root));
    if (currentClass.find(className) != std::string::npos) return root;

    int count = winrt::VisualTreeHelper::GetChildrenCount(root);
    for (int i = 0; i < count; i++) {
        auto child = winrt::VisualTreeHelper::GetChild(root, i);
        auto found = FindFirstDescendantByClass(child, className);
        if (found) return found;
    }
    return nullptr;
}

static void LaunchOrFocusTaskTile(const char* cmd = "SHOW_MAIN") {
    HANDLE hPipe = CreateFileW(
        L"\\\\.\\pipe\\TaskTilePipe",
        GENERIC_WRITE,
        0, nullptr,
        OPEN_EXISTING, 0, nullptr);

    if (hPipe != INVALID_HANDLE_VALUE) {
        DWORD written = 0;
        WriteFile(hPipe, cmd, (DWORD)strlen(cmd), &written, nullptr);
        CloseHandle(hPipe);
        return;
    }

    WCHAR exePath[MAX_PATH] = { 0 };
    if (GetModuleFileNameW(g_hModule, exePath, MAX_PATH) > 0) {
        WCHAR* lastSlash = wcsrchr(exePath, L'\\');
        if (lastSlash) {
            *(lastSlash + 1) = L'\0';
            wcscat_s(exePath, L"TaskTile.exe");
            if (GetFileAttributesW(exePath) != INVALID_FILE_ATTRIBUTES) {
                ShellExecuteW(nullptr, L"open", exePath, nullptr, nullptr, SW_SHOW);
                return;
            }
        }
    }
    ShellExecuteW(nullptr, L"open", L"TaskTile.exe", nullptr, nullptr, SW_SHOW);
}

// Hardware-accelerated native Windows 11 Fluent 2 slide animation using Windows.UI.Composition
static void AnimateSlideIn(winrt::UIElement const& element, float fromX = 48.0f, float fromY = 0.0f) {
    try {
        if (!element) return;
        auto visual = winrt::Windows::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(element);
        if (!visual) return;
        auto compositor = visual.Compositor();
        if (!compositor) return;

        // Fluent 2 Vector3 offset slide
        auto offsetAnim = compositor.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(0.0f, winrt::Windows::Foundation::Numerics::float3{ fromX, fromY, 0.0f });
        // Native Windows 11 cubic bezier ease out: (0.1, 0.9, 0.2, 1.0)
        auto easing = compositor.CreateCubicBezierEasingFunction({ 0.1f, 0.9f }, { 0.2f, 1.0f });
        offsetAnim.InsertKeyFrame(1.0f, winrt::Windows::Foundation::Numerics::float3{ 0.0f, 0.0f, 0.0f }, easing);
        offsetAnim.Duration(std::chrono::milliseconds(240));
        visual.StartAnimation(L"Offset", offsetAnim);

        // Opacity fade
        auto opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0.0f, 0.0f);
        opacityAnim.InsertKeyFrame(1.0f, 1.0f);
        opacityAnim.Duration(std::chrono::milliseconds(200));
        visual.StartAnimation(L"Opacity", opacityAnim);
    } catch (...) {}
}

// Forward declarations
static void NavigateBackToPersonalization(winrt::Frame const& frame);
static void NavigateToAppGroups(winrt::Frame const& frame);

// Native Settings ToggleSwitch: label "On" / "Off" to the LEFT of the toggle pill
static winrt::StackPanel CreateFluentToggleSwitch(bool isInitialOn) {
    winrt::StackPanel panel;
    panel.Orientation(winrt::Orientation::Horizontal);
    panel.VerticalAlignment(winrt::VerticalAlignment::Center);

    winrt::TextBlock tbLabel;
    tbLabel.Text(isInitialOn ? L"On" : L"Off");
    tbLabel.FontSize(14);
    tbLabel.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x9E, 0xFF, 0xFF, 0xFF }));
    tbLabel.VerticalAlignment(winrt::VerticalAlignment::Center);
    tbLabel.Margin(winrt::Thickness{ 0, 0, 12, 0 });

    winrt::ToggleSwitch ts;
    ts.IsOn(isInitialOn);
    ts.OnContent(nullptr);
    ts.OffContent(nullptr);
    ts.VerticalAlignment(winrt::VerticalAlignment::Center);

    ts.Toggled([tbLabel](winrt::IInspectable const& s, auto const&) {
        if (auto t = s.try_as<winrt::ToggleSwitch>()) {
            tbLabel.Text(t.IsOn() ? L"On" : L"Off");
        }
    });

    panel.Children().Append(tbLabel);
    panel.Children().Append(ts);
    return panel;
}

// Native Setting Row: 48px height without subtitle, 64px height with subtitle
static winrt::Border CreateSettingRow(
    std::wstring const& iconGlyph,
    std::wstring const& title,
    std::wstring const& subtitle,
    winrt::FrameworkElement const& controlElement)
{
    winrt::Border row;
    bool hasSubtitle = !subtitle.empty();
    row.Height(hasSubtitle ? 64 : 48);
    row.Background(winrt::SolidColorBrush(winrt::Colors::Transparent()));
    row.Padding(winrt::Thickness{ 16, 0, 16, 0 });

    row.PointerEntered([](winrt::IInspectable const& s, auto const&) {
        if (auto b = s.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0A, 0xFF, 0xFF, 0xFF }));
        }
    });
    row.PointerExited([](winrt::IInspectable const& s, auto const&) {
        if (auto b = s.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Colors::Transparent()));
        }
    });

    winrt::Grid grid;
    winrt::ColumnDefinition col0, col1, col2;
    col0.Width(winrt::GridLength{ 0, winrt::GridUnitType::Auto });
    col1.Width(winrt::GridLength{ 1, winrt::GridUnitType::Star });
    col2.Width(winrt::GridLength{ 0, winrt::GridUnitType::Auto });
    grid.ColumnDefinitions().Append(col0);
    grid.ColumnDefinitions().Append(col1);
    grid.ColumnDefinitions().Append(col2);

    if (!iconGlyph.empty()) {
        winrt::FontIcon icon;
        icon.Glyph(iconGlyph);
        icon.FontSize(20);
        icon.FontFamily(winrt::FontFamily(L"Segoe Fluent Icons, Segoe MDL2 Assets"));
        icon.Margin(winrt::Thickness{ 0, 0, 16, 0 });
        icon.VerticalAlignment(winrt::VerticalAlignment::Center);
        winrt::Grid::SetColumn(icon, 0);
        grid.Children().Append(icon);
    }

    winrt::StackPanel textStack;
    textStack.Spacing(2);
    textStack.VerticalAlignment(winrt::VerticalAlignment::Center);

    winrt::TextBlock tbTitle;
    tbTitle.Text(title);
    tbTitle.FontSize(14);
    tbTitle.FontWeight(winrt::FontWeights::Normal());
    textStack.Children().Append(tbTitle);

    if (hasSubtitle) {
        winrt::TextBlock tbSub;
        tbSub.Text(subtitle);
        tbSub.FontSize(12);
        tbSub.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x9E, 0xFF, 0xFF, 0xFF }));
        textStack.Children().Append(tbSub);
    }

    winrt::Grid::SetColumn(textStack, 1);
    grid.Children().Append(textStack);

    if (controlElement) {
        controlElement.VerticalAlignment(winrt::VerticalAlignment::Center);
        winrt::Grid::SetColumn(controlElement, 2);
        grid.Children().Append(controlElement);
    }

    row.Child(grid);
    return row;
}

// Group section matching Image 2 ("Recommended", "All", "Other")
static winrt::StackPanel CreateCardGroupSection(
    std::wstring const& groupHeader,
    std::vector<winrt::FrameworkElement> const& rows)
{
    winrt::StackPanel section;
    section.Spacing(0);
    section.Margin(winrt::Thickness{ 0, 0, 0, 24 });

    if (!groupHeader.empty()) {
        winrt::TextBlock tbHeader;
        tbHeader.Text(groupHeader);
        tbHeader.FontSize(14);
        tbHeader.FontWeight(winrt::FontWeights::SemiBold());
        tbHeader.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0xD0, 0xFF, 0xFF, 0xFF }));
        tbHeader.Margin(winrt::Thickness{ 0, 0, 0, 8 });
        section.Children().Append(tbHeader);
    }

    winrt::Border groupCard;
    groupCard.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0D, 0xFF, 0xFF, 0xFF }));
    groupCard.BorderBrush(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x12, 0xFF, 0xFF, 0xFF }));
    groupCard.BorderThickness(winrt::Thickness{ 1, 1, 1, 1 });
    groupCard.CornerRadius(winrt::CornerRadius{ 4, 4, 4, 4 });

    winrt::StackPanel rowsStack;
    rowsStack.Spacing(0);

    for (size_t i = 0; i < rows.size(); i++) {
        if (i > 0) {
            winrt::Border divider;
            divider.Height(1);
            divider.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0F, 0xFF, 0xFF, 0xFF }));
            divider.Margin(winrt::Thickness{ 16, 0, 16, 0 });
            rowsStack.Children().Append(divider);
        }
        rowsStack.Children().Append(rows[i]);
    }

    groupCard.Child(rowsStack);
    section.Children().Append(groupCard);
    return section;
}

static winrt::Border CreateRelatedCard(
    winrt::Frame const& frame,
    std::wstring const& title,
    std::wstring const& subtitle,
    std::wstring const& uri)
{
    winrt::Border card;
    card.Height(64);
    card.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0D, 0xFF, 0xFF, 0xFF }));
    card.BorderBrush(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x12, 0xFF, 0xFF, 0xFF }));
    card.BorderThickness(winrt::Thickness{ 1, 1, 1, 1 });
    card.CornerRadius(winrt::CornerRadius{ 4, 4, 4, 4 });
    card.Margin(winrt::Thickness{ 0, 0, 0, 4 });
    card.Padding(winrt::Thickness{ 16, 0, 16, 0 });

    card.PointerEntered([](winrt::IInspectable const& s, auto const&) {
        if (auto b = s.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x18, 0xFF, 0xFF, 0xFF }));
        }
    });
    card.PointerExited([](winrt::IInspectable const& s, auto const&) {
        if (auto b = s.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0D, 0xFF, 0xFF, 0xFF }));
        }
    });

    card.Tapped([frame, uri](auto const&, auto const&) {
        try {
            NavigateBackToPersonalization(frame);
            winrt::Windows::System::Launcher::LaunchUriAsync(winrt::Uri(uri));
        } catch (...) {}
    });

    winrt::Grid grid;
    winrt::ColumnDefinition col0, col1;
    col0.Width(winrt::GridLength{ 1, winrt::GridUnitType::Star });
    col1.Width(winrt::GridLength{ 0, winrt::GridUnitType::Auto });
    grid.ColumnDefinitions().Append(col0);
    grid.ColumnDefinitions().Append(col1);

    winrt::StackPanel textStack;
    textStack.Spacing(2);
    textStack.VerticalAlignment(winrt::VerticalAlignment::Center);

    winrt::TextBlock tb;
    tb.Text(title);
    tb.FontSize(14);
    tb.FontWeight(winrt::FontWeights::Normal());
    textStack.Children().Append(tb);

    winrt::TextBlock tbSub;
    tbSub.Text(subtitle);
    tbSub.FontSize(12);
    tbSub.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x9E, 0xFF, 0xFF, 0xFF }));
    textStack.Children().Append(tbSub);

    winrt::Grid::SetColumn(textStack, 0);
    grid.Children().Append(textStack);

    winrt::FontIcon chev;
    chev.Glyph(L"\xE76C");
    chev.FontSize(14);
    chev.Opacity(0.6);
    chev.FontFamily(winrt::FontFamily(L"Segoe Fluent Icons, Segoe MDL2 Assets"));
    chev.VerticalAlignment(winrt::VerticalAlignment::Center);
    winrt::Grid::SetColumn(chev, 1);
    grid.Children().Append(chev);

    card.Child(grid);
    return card;
}

static winrt::Page BuildAppGroupsL2Page(winrt::Frame const& frame) {
    winrt::Page page;

    try {
        auto res = winrt::Application::Current().Resources();
        if (res && res.HasKey(winrt::box_value(L"ApplicationPageBackgroundThemeBrush"))) {
            page.Background(res.Lookup(winrt::box_value(L"ApplicationPageBackgroundThemeBrush")).as<winrt::Brush>());
        } else {
            page.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0xFF, 0x1C, 0x1C, 0x1C }));
        }
    } catch (...) {
        page.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0xFF, 0x1C, 0x1C, 0x1C }));
    }

    winrt::ScrollViewer scroll;
    scroll.VerticalScrollBarVisibility(winrt::ScrollBarVisibility::Auto);
    scroll.HorizontalScrollBarVisibility(winrt::ScrollBarVisibility::Disabled);
    scroll.Margin(winrt::Thickness{ 26, 0, 0, 0 });

    winrt::StackPanel contentStack;
    contentStack.Width(1000);
    contentStack.HorizontalAlignment(winrt::HorizontalAlignment::Left);
    contentStack.Margin(winrt::Thickness{ 0, 16, 24, 48 });
    contentStack.Spacing(0);

    // Section 1: Taskbar items & behavior
    std::vector<winrt::FrameworkElement> g1Rows;

    winrt::Button btnOpen;
    btnOpen.Content(winrt::box_value(L"Open"));
    btnOpen.Padding(winrt::Thickness{ 16, 5, 16, 5 });
    btnOpen.CornerRadius(winrt::CornerRadius{ 4, 4, 4, 4 });
    btnOpen.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x1A, 0xFF, 0xFF, 0xFF }));
    btnOpen.BorderBrush(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x1F, 0xFF, 0xFF, 0xFF }));
    btnOpen.BorderThickness(winrt::Thickness{ 1, 1, 1, 1 });
    btnOpen.Foreground(winrt::SolidColorBrush(winrt::Colors::White()));
    btnOpen.Click([](auto const&, auto const&) {
        LaunchOrFocusTaskTile("SHOW_MAIN");
    });
    g1Rows.push_back(CreateSettingRow(
        L"\xECA5",
        L"Open TaskTile Manager",
        L"Create, edit, and organize floating app tiles and grouping collections",
        btnOpen));

    g1Rows.push_back(CreateSettingRow(
        L"\xE718",
        L"Native taskbar pinning",
        L"Pin app groups directly onto Windows 11 taskbar without desktop shortcuts",
        CreateFluentToggleSwitch(true)));

    g1Rows.push_back(CreateSettingRow(
        L"\xE728",
        L"Hover preview",
        L"Summon floating app tile instantly when hovering over taskbar pin",
        CreateFluentToggleSwitch(true)));

    contentStack.Children().Append(CreateCardGroupSection(
        L"Taskbar items & behavior",
        g1Rows));

    // Section 2: Flyout appearance & styles
    std::vector<winrt::FrameworkElement> g2Rows;

    winrt::ComboBox cbStyle;
    cbStyle.Items().Append(winrt::box_value(L"Radial menu (Circular)"));
    cbStyle.Items().Append(winrt::box_value(L"WinUI 3 Grid"));
    cbStyle.Items().Append(winrt::box_value(L"Compact list"));
    cbStyle.SelectedIndex(0);
    cbStyle.Width(180);
    g2Rows.push_back(CreateSettingRow(
        L"\xE790",
        L"Flyout layout style",
        L"Switch between circular radial popups, classic grids, and compact lists",
        cbStyle));

    winrt::ComboBox cbBackdrop;
    cbBackdrop.Items().Append(winrt::box_value(L"Mica Alt (Default)"));
    cbBackdrop.Items().Append(winrt::box_value(L"Desktop Acrylic"));
    cbBackdrop.Items().Append(winrt::box_value(L"Mica"));
    cbBackdrop.SelectedIndex(0);
    cbBackdrop.Width(180);
    g2Rows.push_back(CreateSettingRow(
        L"\xE771",
        L"Backdrop material",
        L"Select Fluent 2 translucent backdrop effect for floating flyouts",
        cbBackdrop));

    g2Rows.push_back(CreateSettingRow(
        L"\xE748",
        L"Taskbar gap alignment",
        L"Ensure proper gap positioning for bottom and top taskbars",
        CreateFluentToggleSwitch(true)));

    contentStack.Children().Append(CreateCardGroupSection(
        L"Flyout appearance & styles",
        g2Rows));

    // Section 3: Shortcuts & interaction
    std::vector<winrt::FrameworkElement> g3Rows;

    winrt::Border hotkeyBadge;
    hotkeyBadge.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x1A, 0xFF, 0xFF, 0xFF }));
    hotkeyBadge.BorderBrush(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x1F, 0xFF, 0xFF, 0xFF }));
    hotkeyBadge.BorderThickness(winrt::Thickness{ 1, 1, 1, 1 });
    hotkeyBadge.CornerRadius(winrt::CornerRadius{ 4, 4, 4, 4 });
    hotkeyBadge.Padding(winrt::Thickness{ 10, 4, 10, 4 });
    winrt::TextBlock tbHk;
    tbHk.Text(L"Win + Shift + T");
    tbHk.FontSize(12);
    tbHk.FontWeight(winrt::FontWeights::SemiBold());
    hotkeyBadge.Child(tbHk);
    g3Rows.push_back(CreateSettingRow(
        L"\xE765",
        L"Global summon hotkey",
        L"Press shortcut to summon active app group flyout anywhere",
        hotkeyBadge));

    g3Rows.push_back(CreateSettingRow(
        L"\xE8D7",
        L"Auto-dismiss on focus lost",
        L"Automatically close popup when clicking outside or switching apps",
        CreateFluentToggleSwitch(true)));

    contentStack.Children().Append(CreateCardGroupSection(
        L"Shortcuts & interaction",
        g3Rows));

    // Section 4: Related settings
    winrt::TextBlock tbRelated;
    tbRelated.Text(L"Related settings");
    tbRelated.FontSize(14);
    tbRelated.FontWeight(winrt::FontWeights::SemiBold());
    tbRelated.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0xD0, 0xFF, 0xFF, 0xFF }));
    tbRelated.Margin(winrt::Thickness{ 0, 0, 0, 8 });
    contentStack.Children().Append(tbRelated);

    contentStack.Children().Append(CreateRelatedCard(
        frame,
        L"Taskbar",
        L"Taskbar alignment, badging, automatically hide, and multiple displays",
        L"ms-settings:taskbar"));

    contentStack.Children().Append(CreateRelatedCard(
        frame,
        L"Start",
        L"Recent apps and items, folders, recommendations",
        L"ms-settings:personalization-start"));

    scroll.Content(contentStack);
    page.Content(scroll);

    g_l2Page = page;
    return page;
}

static void NavigateToAppGroups(winrt::Frame const& frame) {
    if (g_isOnAppGroupsPage) return;
    try {
        auto currentWin = winrt::Window::Current();
        if (!currentWin || !currentWin.Content()) return;

        if (frame.Content()) {
            std::string cls = winrt::to_string(winrt::get_class_name(frame.Content()));
            if (cls.find("CategoryPage") != std::string::npos) {
                g_categoryPage = frame.Content();
            }
        }

        auto alignControlObj = FindDescendantByName(currentWin.Content(), L"PermanentNavViewHeaderAlignControl");
        if (alignControlObj) {
            if (auto alignControl = alignControlObj.try_as<winrt::ContentControl>()) {
                if (!g_originalBreadcrumbBar) {
                    g_originalBreadcrumbBar = alignControl.Content();
                }

                winrt::StackPanel bcStack;
                bcStack.Orientation(winrt::Orientation::Horizontal);
                bcStack.VerticalAlignment(winrt::VerticalAlignment::Center);
                bcStack.Height(44);

                // Personalization link
                winrt::TextBlock linkPersonalize;
                linkPersonalize.Text(L"Personalization");
                linkPersonalize.FontSize(28);
                linkPersonalize.FontWeight(winrt::FontWeights::SemiBold());
                linkPersonalize.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x9E, 0xFF, 0xFF, 0xFF }));
                linkPersonalize.VerticalAlignment(winrt::VerticalAlignment::Center);

                linkPersonalize.PointerEntered([](winrt::IInspectable const& s, auto const&) {
                    if (auto tb = s.try_as<winrt::TextBlock>()) {
                        tb.Foreground(winrt::SolidColorBrush(winrt::Colors::White()));
                    }
                });
                linkPersonalize.PointerExited([](winrt::IInspectable const& s, auto const&) {
                    if (auto tb = s.try_as<winrt::TextBlock>()) {
                        tb.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x9E, 0xFF, 0xFF, 0xFF }));
                    }
                });
                linkPersonalize.Tapped([frame](auto const&, auto const&) {
                    NavigateBackToPersonalization(frame);
                });

                bcStack.Children().Append(linkPersonalize);

                // Chevron ›
                winrt::FontIcon chev;
                chev.Glyph(L"\xE974");
                chev.FontSize(14);
                chev.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x8A, 0xFF, 0xFF, 0xFF }));
                chev.FontFamily(winrt::FontFamily(L"Segoe Fluent Icons, Segoe MDL2 Assets"));
                chev.VerticalAlignment(winrt::VerticalAlignment::Center);
                chev.Margin(winrt::Thickness{ 14, 2, 14, 0 });
                bcStack.Children().Append(chev);

                // Current Page Title: App groups
                winrt::TextBlock currentTitle;
                currentTitle.Text(L"App groups");
                currentTitle.FontSize(28);
                currentTitle.FontWeight(winrt::FontWeights::SemiBold());
                currentTitle.Foreground(winrt::SolidColorBrush(winrt::Colors::White()));
                currentTitle.VerticalAlignment(winrt::VerticalAlignment::Center);
                bcStack.Children().Append(currentTitle);

                alignControl.Content(bcStack);
                AnimateSlideIn(bcStack, 16.0f, 0.0f);
            }
        }

        // Enable and hook real window back button
        auto backBtn = FindDescendantByName(currentWin.Content(), L"NavigationViewBackButton");
        if (backBtn) {
            if (auto fe = backBtn.try_as<winrt::FrameworkElement>()) {
                fe.Visibility(winrt::Visibility::Visible);
            }
            if (auto ctrl = backBtn.try_as<winrt::Control>()) {
                ctrl.IsEnabled(true);
            }
            if (auto btn = backBtn.try_as<winrt::Button>()) {
                if (g_backToken) {
                    btn.Click(g_backToken);
                    g_backToken = {};
                }
                g_backToken = btn.Click([frame](auto const&, auto const&) {
                    NavigateBackToPersonalization(frame);
                });
            }
        }

        g_isOnAppGroupsPage = true;
        auto newPage = BuildAppGroupsL2Page(frame);
        g_l2Page = newPage;
        frame.Content(newPage);

        // Native slide animation from right (+48px)
        AnimateSlideIn(newPage, 48.0f, 0.0f);

    } catch (...) {}
}

static void NavigateBackToPersonalization(winrt::Frame const& frame) {
    if (!g_isOnAppGroupsPage) return;
    try {
        auto currentWin = winrt::Window::Current();
        if (!currentWin || !currentWin.Content()) return;

        // Restore original BreadcrumbBar inside PermanentNavViewHeaderAlignControl
        auto alignControlObj = FindDescendantByName(currentWin.Content(), L"PermanentNavViewHeaderAlignControl");
        if (alignControlObj && g_originalBreadcrumbBar) {
            if (auto cc = alignControlObj.try_as<winrt::ContentControl>()) {
                cc.Content(g_originalBreadcrumbBar);
            }
        }

        // Hide back button and unregister handler
        auto backBtn = FindDescendantByName(currentWin.Content(), L"NavigationViewBackButton");
        if (backBtn) {
            if (auto fe = backBtn.try_as<winrt::FrameworkElement>()) {
                fe.Visibility(winrt::Visibility::Collapsed);
            }
            if (auto ctrl = backBtn.try_as<winrt::Control>()) {
                ctrl.IsEnabled(false);
            }
            if (auto btn = backBtn.try_as<winrt::Button>()) {
                if (g_backToken) {
                    btn.Click(g_backToken);
                    g_backToken = {};
                }
            }
        }

        g_isOnAppGroupsPage = false;
        if (g_categoryPage) {
            if (auto uiElem = g_categoryPage.try_as<winrt::UIElement>()) {
                uiElem.Visibility(winrt::Visibility::Visible);
            }
            frame.Content(g_categoryPage);
            if (auto uiElem = g_categoryPage.try_as<winrt::UIElement>()) {
                // Native slide animation from left (-48px)
                AnimateSlideIn(uiElem, -48.0f, 0.0f);
            }
        }

    } catch (...) {}
}

// Standalone group card in Personalization (matching our insanely accurate first iteration)
static winrt::Border BuildStandaloneAppGroupsCard(winrt::Frame const& frame, double topY) {
    winrt::Border card;
    card.Name(L"TaskTileAppGroupsCard");
    card.Tag(winrt::box_value(L"TaskTileAppGroupsCard"));
    card.Height(69);
    card.VerticalAlignment(winrt::VerticalAlignment::Top);
    card.HorizontalAlignment(winrt::HorizontalAlignment::Stretch);
    card.Margin(winrt::Thickness{ 0, topY, 0, 0 });
    card.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0D, 0xFF, 0xFF, 0xFF }));
    card.BorderBrush(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x14, 0xFF, 0xFF, 0xFF }));
    card.BorderThickness(winrt::Thickness{ 1, 1, 1, 1 });
    card.CornerRadius(winrt::CornerRadius{ 4, 4, 4, 4 });
    card.Padding(winrt::Thickness{ 16, 0, 16, 0 });

    card.PointerEntered([](winrt::IInspectable const& sender, auto const&) {
        if (auto b = sender.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x18, 0xFF, 0xFF, 0xFF }));
        }
    });
    card.PointerExited([](winrt::IInspectable const& sender, auto const&) {
        if (auto b = sender.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x0D, 0xFF, 0xFF, 0xFF }));
        }
    });
    card.PointerPressed([](winrt::IInspectable const& sender, winrt::PointerRoutedEventArgs const& e) {
        e.Handled(true);
        if (auto b = sender.try_as<winrt::Border>()) {
            b.Background(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x08, 0xFF, 0xFF, 0xFF }));
        }
    });

    winrt::Grid grid;
    winrt::ColumnDefinition col0, col1, col2;
    col0.Width(winrt::GridLength{ 0, winrt::GridUnitType::Auto });
    col1.Width(winrt::GridLength{ 1, winrt::GridUnitType::Star });
    col2.Width(winrt::GridLength{ 0, winrt::GridUnitType::Auto });
    grid.ColumnDefinitions().Append(col0);
    grid.ColumnDefinitions().Append(col1);
    grid.ColumnDefinitions().Append(col2);

    // Col 0: Icon  Tiles U+ECA5
    winrt::FontIcon icon;
    icon.Glyph(L"\xECA5");
    icon.FontSize(20);
    icon.FontFamily(winrt::FontFamily(L"Segoe Fluent Icons, Segoe MDL2 Assets"));
    icon.Margin(winrt::Thickness{ 0, 0, 16, 0 });
    icon.VerticalAlignment(winrt::VerticalAlignment::Center);
    winrt::Grid::SetColumn(icon, 0);
    grid.Children().Append(icon);

    // Col 1: Text
    winrt::StackPanel textStack;
    textStack.Spacing(2);
    textStack.VerticalAlignment(winrt::VerticalAlignment::Center);

    winrt::TextBlock tbTitle;
    tbTitle.Text(L"App groups");
    tbTitle.FontSize(14);
    tbTitle.FontWeight(winrt::FontWeights::Normal()); // Regular/Normal weight matching Start & Taskbar exactly
    textStack.Children().Append(tbTitle);

    winrt::TextBlock tbSub;
    tbSub.Text(L"Item grouping, behaviors, and system pins");
    tbSub.FontSize(12);
    tbSub.Foreground(winrt::SolidColorBrush(winrt::Windows::UI::Color{ 0x9E, 0xFF, 0xFF, 0xFF }));
    textStack.Children().Append(tbSub);

    winrt::Grid::SetColumn(textStack, 1);
    grid.Children().Append(textStack);

    // Col 2: Chevron
    winrt::FontIcon chev;
    chev.Glyph(L"\xE76C"); // ›
    chev.FontSize(14);
    chev.Opacity(0.6);
    chev.FontFamily(winrt::FontFamily(L"Segoe Fluent Icons, Segoe MDL2 Assets"));
    chev.VerticalAlignment(winrt::VerticalAlignment::Center);
    winrt::Grid::SetColumn(chev, 2);
    grid.Children().Append(chev);

    card.Child(grid);

    card.PointerReleased([frame](auto const&, winrt::PointerRoutedEventArgs const& e) {
        e.Handled(true);
        NavigateToAppGroups(frame);
    });

    card.Tapped([frame](auto const&, winrt::TappedRoutedEventArgs const& e) {
        e.Handled(true);
        NavigateToAppGroups(frame);
    });

    card.KeyDown([frame](auto const&, winrt::KeyRoutedEventArgs const& e) {
        if (e.Key() == winrt::Windows::System::VirtualKey::Enter || e.Key() == winrt::Windows::System::VirtualKey::Space) {
            e.Handled(true);
            NavigateToAppGroups(frame);
        }
    });

    return card;
}

static void CheckAndUpdateCard() {
    try {
        auto currentWin = winrt::Window::Current();
        if (!currentWin || !currentWin.Content()) return;

        auto frameObj = FindDescendantByName(currentWin.Content(), L"PermanentNavRootFrame");
        if (!frameObj) return;
        auto frame = frameObj.as<winrt::Frame>();

        if (g_isOnAppGroupsPage) {
            if (g_l2Page && frame.Content() != g_l2Page) {
                g_isOnAppGroupsPage = false;
                auto alignControlObj = FindDescendantByName(currentWin.Content(), L"PermanentNavViewHeaderAlignControl");
                if (alignControlObj && g_originalBreadcrumbBar) {
                    if (auto cc = alignControlObj.try_as<winrt::ContentControl>()) {
                        cc.Content(g_originalBreadcrumbBar);
                    }
                }
                if (g_categoryPage) {
                    if (auto ui = g_categoryPage.try_as<winrt::UIElement>()) {
                        ui.Visibility(winrt::Visibility::Visible);
                    }
                }
            } else {
                return;
            }
        }

        if (frame.Content()) {
            std::string contentClass = winrt::to_string(winrt::get_class_name(frame.Content()));
            if (contentClass.find("CategoryPage") != std::string::npos) {
                g_categoryPage = frame.Content();
            }
        }

        auto listObj = FindDescendantByName(currentWin.Content(), L"settingPagesList");
        if (!listObj) return;

        auto list = listObj.as<winrt::ListView>();

        // Check if host grid is already in place
        auto hostGridObj = FindDescendantByName(currentWin.Content(), L"TaskTileHostGrid");
        if (hostGridObj) {
            auto cardObj = FindDescendantByName(hostGridObj, L"TaskTileAppGroupsCard");
            if (cardObj) {
                return; // Already installed and stable!
            }
        }

        // Locate Start and Taskbar items
        winrt::FrameworkElement startFE = nullptr;
        winrt::FrameworkElement taskbarFE = nullptr;

        for (int i = 0; i < 15; i++) {
            auto container = list.ContainerFromIndex(i);
            if (!container) continue;
            auto fe = container.try_as<winrt::FrameworkElement>();
            if (!fe) continue;

            std::vector<winrt::DependencyObject> q;
            q.push_back(container);
            while (!q.empty()) {
                auto curr = q.back();
                q.pop_back();
                if (auto tb = curr.try_as<winrt::TextBlock>()) {
                    if (tb.Text() == L"Start" && !startFE) {
                        startFE = fe;
                    } else if (tb.Text() == L"Taskbar" && !taskbarFE) {
                        taskbarFE = fe;
                    }
                }
                int count = winrt::VisualTreeHelper::GetChildrenCount(curr);
                for (int k = 0; k < count; k++) {
                    q.push_back(winrt::VisualTreeHelper::GetChild(curr, k));
                }
            }
        }

        if (!startFE) {
            auto c6 = list.ContainerFromIndex(6);
            if (c6) startFE = c6.try_as<winrt::FrameworkElement>();
        }
        if (!taskbarFE) {
            auto c7 = list.ContainerFromIndex(7);
            if (c7) taskbarFE = c7.try_as<winrt::FrameworkElement>();
        }

        static int s_ticks = 0;
        s_ticks++;
        if (s_ticks <= 5 || s_ticks % 60 == 0) {
            LogBridge("CheckAndUpdateCard tick " + std::to_string(s_ticks) + 
                      ", list=" + (listObj ? "yes" : "no") +
                      ", startFE=" + (startFE ? "yes" : "no") +
                      ", taskbarFE=" + (taskbarFE ? "yes" : "no") +
                      ", hostGrid=" + (hostGridObj ? "yes" : "no"));
        }

        if (!startFE || !taskbarFE) return;

        // Push Taskbar down by 73px to create clean gap between Start and Taskbar
        taskbarFE.Margin(winrt::Thickness{ 0, 73, 0, 0 });
        list.UpdateLayout();

        // Calculate card top coordinate relative to settingPagesList
        auto tr = startFE.TransformToVisual(list);
        auto ptStart = tr.TransformPoint(winrt::Point{ 0, 0 });
        double cardTop = ptStart.Y + startFE.ActualHeight() + 4;

        auto parentDO = list.Parent();
        if (!parentDO) return;
        auto parentCC = parentDO.try_as<winrt::ContentControl>();
        auto parentPanel = parentDO.try_as<winrt::Panel>();

        if (!parentCC && !parentPanel) return;

        winrt::Grid hostGrid;
        hostGrid.Name(L"TaskTileHostGrid");

        auto card = BuildStandaloneAppGroupsCard(frame, cardTop);

        if (parentCC) {
            parentCC.Content(nullptr);
            hostGrid.Children().Append(list);
            hostGrid.Children().Append(card);
            parentCC.Content(hostGrid);
        } else if (parentPanel) {
            uint32_t idx = 0;
            if (parentPanel.Children().IndexOf(list, idx)) {
                parentPanel.Children().RemoveAt(idx);
                hostGrid.Children().Append(list);
                hostGrid.Children().Append(card);
                parentPanel.Children().InsertAt(idx, hostGrid);
            }
        }

        hostGrid.UpdateLayout();
        LogBridge("HostGrid and App groups card successfully installed!");
    } catch (std::exception const& ex) {
        LogBridge(std::string("Exception in CheckAndUpdateCard: ") + ex.what());
    } catch (winrt::hresult_error const& hr) {
        LogBridge("HRESULT in CheckAndUpdateCard: 0x" + std::to_string((uint32_t)hr.code()) + " - " + winrt::to_string(hr.message()));
    } catch (...) {
        LogBridge("Unknown exception in CheckAndUpdateCard");
    }
}

static DWORD WINAPI MonitorWorker(LPVOID) {
    LogBridge("MonitorWorker thread started!");
    for (int retry = 0; retry < 50; retry++) {
        Sleep(100);
        try {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
            auto mainView = winrt::CoreApplication::MainView();
            if (!mainView) continue;
            auto coreWnd = mainView.CoreWindow();
            if (!coreWnd) continue;
            auto dispatcher = coreWnd.Dispatcher();
            if (!dispatcher) continue;

            LogBridge("Got Dispatcher! Scheduling CheckAndUpdateCard...");
            dispatcher.RunAsync(winrt::CoreDispatcherPriority::High, []() {
                try {
                    LogBridge("Dispatcher.RunAsync executing on UI thread!");
                    CheckAndUpdateCard();

                    g_timer = winrt::DispatcherTimer();
                    g_timer.Interval(std::chrono::milliseconds(30));
                    static int s_fastTicks = 0;
                    g_timer.Tick([](auto const&, auto const&) {
                        CheckAndUpdateCard();
                        s_fastTicks++;
                        if (s_fastTicks > 120) {
                            g_timer.Interval(std::chrono::milliseconds(150));
                        }
                    });
                    g_timer.Start();
                    LogBridge("DispatcherTimer started!");
                } catch (std::exception const& ex) {
                    LogBridge(std::string("Dispatcher exception: ") + ex.what());
                } catch (...) {
                    LogBridge("Dispatcher unknown exception");
                }
            });
            return 0; // Successfully hooked!
        } catch (std::exception const& ex) {
            LogBridge(std::string("MonitorWorker retry exception: ") + ex.what());
        } catch (...) {}
    }
    LogBridge("MonitorWorker timed out waiting for MainView");
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID) {
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        CreateThread(nullptr, 0, MonitorWorker, nullptr, 0, nullptr);
    }
    return TRUE;
}
