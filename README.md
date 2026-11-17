# vibranceGUI-TW

vibranceGUI 的繁體中文自用版。可為 CS2 等 Windows 程式設定色彩飽和度，切換視窗時自動套用對應的數值。

## 暗色

- 繁體中文介面、暗色主題，使用 10.5 pt 微軟正黑體 UI。
- 移除主視窗的 Twitter 和 PayPal 區塊，標題只顯示 `vibranceGUI`。
- 按主視窗右上角的 **X** 會收進系統匣，並繼續在背景運作。左鍵點系統匣圖示可重新開啟，右鍵選 **結束** 可完全退出。

## 使用方式

1. 開啟 vibranceGUI，在 **桌面色彩飽和度** 設定平常使用的數值，例如 50%。
2. 在 **應用程式設定** 點 **新增**，從正在執行的程式中選擇 CS2，或點 **手動新增** 選擇 `cs2.exe`。
3. 雙擊清單中的 CS2，設定 **遊戲內色彩飽和度**（例如 90%），再按 **儲存**。

以 50%／90% 為例，CS2 在前景時會套用 90%。Alt+Tab 到同一個螢幕上、沒有自訂設定的一般視窗時，會回到 50%，切回 CS2 則再次套用 90%。如果勾選 **僅套用至主螢幕**，切到另一個螢幕時，遊戲所在的螢幕可能維持原本的飽和度。

## 編譯

1. 在 Windows 上用 Visual Studio 開啟 `vibrance.GUI.sln`。此專案使用 C#、Windows Forms 和 .NET Framework 4.0，需要支援該目標版本的建置環境與參考組件。
2. 還原 `vibrance.GUI/packages.config` 中的 NuGet 套件。
3. 選擇 **Release / x86** 並建置。NVIDIA 模組使用專案內附的 32 位元 `vibranceDLL.dll`。

顯卡功能沿用原版的 NVIDIA／AMD 驅動程式介面，是否可用取決於顯卡與驅動程式。

## 原專案

本專案基於 [juv/vibranceGUI](https://github.com/juv/vibranceGUI)，此 repo 用於保存自用的繁體中文與介面調整。
