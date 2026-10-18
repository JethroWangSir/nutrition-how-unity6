# 營養 How 解

大學畢業專題的 Unity 2D 遊戲。這個 repository 專用於 macOS 整理、問題修復與 Unity 6.3 LTS 升級；舊帳號上的原版 repository 不會在此改動。

## 目前版本

- Unity Editor: `6000.3.26f1`（Unity 6.3 LTS）
- App 顯示名稱: `營養 How 解`
- Bundle Identifier: `com.jethrowangsir.nutritionhow`
- 專案內容: `Assets/`, `Packages/`, `ProjectSettings/`
- Git LFS: 大型圖片與影片素材
- 目標平台: Apple Silicon Mac（`arm64`）
- 已驗證開場、選單、四個關卡流程及全部 23 個建置場景
- 執行效能、記憶體與程式穩定性第三階段已完成
- macOS 顯示、視窗與輸入相容性第四階段已完成
- 1024×768、1280×800、1920×1080 共 69 張場景視覺回歸通過
- 第五階段「遊戲體驗與內容品質」的工程項目已完成；本輪進度可跨啟動保存
- 第六階段「商業級玩法與細節精修」進行中；已完成平台跳躍容錯、受傷回饋、分玩法鏡頭安定區、成功／失敗回饋分級、本機遊玩指標、獨立音效音量、降低閃爍，以及滑鼠游標與衝刺 HUD 精修，PlayMode 25/25 通過

## 已完成修復

- 修正 Input System 在場景切換時的 `IndexOutOfRangeException`
- 修正第三關 Boss 區缺少 Animator 時的 `NullReferenceException`
- 專案已完成 Unity 6.3 LTS 轉換

## 開啟專案

1. 安裝 Unity Hub 與 Unity `6000.3.26f1`。
2. 在 Unity Hub 選擇 **Add project from disk**。
3. 選擇這個 repository 的根目錄（同時包含 `Assets`、`Packages`、`ProjectSettings` 的資料夾）。
4. 首次開啟時讓 Unity 重建 `Library/`；該資料夾不會納入 Git。

## 建置 Apple Silicon 版本

在 Unity 選擇 **Build > Nutrition HOW > macOS Apple Silicon**。

命令列建置可使用：

```sh
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /path/to/Nutrition-How \
  -executeMethod NutritionHowBuild.BuildMacArm64 \
  -quit
```

預設輸出為 `/tmp/營養 How 解-macOS-arm64.app`；可用
`NUTRITION_HOW_BUILD_PATH` 指定其他輸出位置。建置會使用無損的 LZ4HC
資料壓縮，縮小成品容量但不降低圖片品質。

## 製作 DMG

```sh
./scripts/package_macos_dmg.sh
```

輸出位於 `Builds/macOS/營養-How-解-macOS-arm64.dmg`。腳本會確認 App
只包含 `arm64`、驗證簽章結構並驗證 DMG。這個流程不包含 Apple Developer
ID 簽署與 Apple 公證；公開散布且需要避免 Gatekeeper 警告時，仍需有效的
Apple Developer 帳號與簽署憑證。

建置容量分析請參考 [`Docs/BUILD_SIZE_AUDIT.md`](Docs/BUILD_SIZE_AUDIT.md)。

## Apple Silicon 效能基準

使用 Development Build 自動量測代表場景：

```sh
./scripts/benchmark_macos.sh
```

腳本會建立 arm64 Development App，以 1280×800 視窗執行兩輪場景取樣，並在
`/tmp/nutrition-how-benchmark/` 輸出 JSON 與 logs。基準結果、判讀與限制請參考
[`Docs/PERFORMANCE_BASELINE.md`](Docs/PERFORMANCE_BASELINE.md)。

在 Unity 選擇 **Tools > Nutrition HOW > Audit Scene Dependencies**，可重新輸出場景素材依賴稽核，協助追查大型場景的載入成本。

## macOS 顯示相容性

遊玩關卡會維持中央 4:3 構圖；選單、教學與影片依原始素材維持 16:9，
不同視窗比例以置中黑邊補足，不會拉伸或顯示額外關卡區域。顯示策略、
輸入調整與多解析度驗證方式請參考
[`Docs/MAC_DISPLAY_COMPATIBILITY.md`](Docs/MAC_DISPLAY_COMPATIBILITY.md)。

第五階段的問題分級、處理順序與驗證紀錄請參考
[`Docs/GAMEPLAY_EXPERIENCE_AUDIT.md`](Docs/GAMEPLAY_EXPERIENCE_AUDIT.md)。

第六階段的市場體驗差距、優先順序與分批驗證請參考
[`Docs/COMMERCIAL_GAMEPLAY_POLISH.md`](Docs/COMMERCIAL_GAMEPLAY_POLISH.md)。
