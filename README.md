# ZebraBleLabelPrinter

WPF (.NET 8) + MVVM 工程，配合 **Zebra 110XiIII Plus 600DPI**，通过 USB（ZDesigner 驱动）打印蓝牙 MAC 条码。

## 功能
- 启动后自动用 `BluetoothLEAdvertisementWatcher` **持续扫描** BLE 广播（Stopped 自动重启）
- 主页列表实时显示设备，**按 RSSI 信号强度降序排序**，RSSI 条 + dBm 数值
- 顶部"名称过滤"框：**不区分大小写** 包含匹配
- 每行一个 **打印条码** 按钮，点击后把该设备 MAC 用 **CODE128 条形码** 发到 Zebra
- 右上角下拉选择打印机，自动挑选名字含 "Zebra" 的；可点"刷新打印机列表"

## 打印链路
- 两种打印方式，优先用 **COM 直连串口**（绕过 Windows 打印队列，最稳）：
  1. 下拉里选 `COMx（直连串口）` → 程序直接往串口写 ZPL 原始字节；
  2. 下拉里选打印机名 → 走 Windows 打印后台（Win32 `OpenPrinter`/`WritePrinter` 写 RAW），
     发送前自动清空该打印机的排队任务（处理"错误-正在打印-正在重新启动"的卡死状态）。
  前提：Zebra 110XiIII Plus 通过 USB 接好，已安装 ZDesigner 驱动（即"USB 串口驱动"），
  设备管理器端口里能看到对应 COM 口。
- ZPL 标签纸为 **双排：单列 30mm × 20mm，整排宽 81mm，列缝 2mm**（600 DPI ≈ 23.62 dots/mm）：
  `^PW1913`（整排 81mm）× `^LL472`（单列 20mm）；一排两列，**同一 MAC 打两份**（左右列各一张）。
- 条码用 **ZPL ^BC 原生命令**打印（几百字节，任何连接都快，不使用位图/^GFA）；
  每条 ZPL 带 `^LS0` 中和打印机内存储的水平偏移、`^LH0,0` 中和存储的标签原点。
- 排版参数（模块宽、列块起点、列内居中坐标、条码/文本位置）统一由 `Services/Code128Layout.cs` 计算：
  **模块宽固定 3 dots**；**所有 MAC 统一按 CODE128 Subset B（167 模块 → 501 dots）列内居中**，
  第 1 列块 x=0（条码列内 x=104），第 2 列块 x=756（条码 x=860）；
  条码高 300 dots（≈12.7mm），条码+文本整体在 20mm 列高内垂直居中（块顶 y=63）。
- ZPL 中 `^FD>:{MAC}^FS` 带 **">:" 显式 Start Code B**（官方建议，杜绝固件子集歧义），
  且 **不发送 ^CI27**（110XiIII 老固件字符集映射曾导致含字母数据编码异常，纯数字不受影响）。
- 每列条码下方 **打印 MAC 可读文本**（`^A0N,36,36`，列内全宽 709 居中，y=373）：
  扫码枪扫出的内容可直接与标签上的文字比对，用于验证 Zebra 编码是否正确。
- 若打印仍左右偏（整排整体左溢/右偏）：改 `Services/Code128Layout.cs` 里的 `HorizontalOffsetDots`
  （正数两列整体右移，负数左移，1mm≈23.6 dots）；标签尺寸不同则改 `RowW` / `ColW` / `ColH` / `ColGap`。

## 控制台调试工具（ConsoleTool）
构建：`dotnet build -c Release ConsoleTool\ZebraPrinterConsole.csproj`
```
ConsoleTool\bin\Release\net8.0-windows10.0.19041.0\ZebraPrinterConsole.exe
ZebraPrinterConsole <MAC>                    # 输出打印参数与完整 ZPL
ZebraPrinterConsole -print <MAC> -com COM3 [-offset N]          # 直连串口打印测试
ZebraPrinterConsole -print <MAC> -printer "名称" [-offset N]    # 打印队列打印测试
ZebraPrinterConsole -decode <图片路径>        # 用 ZXing 解码照片中的条码（验证打印质量/内容）
```
- `-offset N`：水平偏移(dots)，正数右移、负数左移；左侧溢出被截 → 用正数（如 60）右移补偿，
  打一张看一次，直到左右居中，再把该值填回 `Code128Layout.HorizontalOffsetDots`。

## 关键文件
```
ZebraBleLabelPrinter.csproj
App.xaml(.cs)
MainWindow.xaml(.cs)
Models/BleDevice.cs                — 设备模型，INPC，RSSI 信号条
ViewModels/ViewModelBase.cs
ViewModels/RelayCommand.cs
ViewModels/MainViewModel.cs        — 过滤/排序/打印命令
Services/BluetoothLeScanner.cs     — 持续 BLE 扫描，按 MAC 去重
Services/Code128Layout.cs          — 双排尺寸/列内居中排版计算
Services/ZebraPrinterService.cs    — 打印机枚举 + ZPL 生成 + 发送
Helpers/RawPrinterHelper.cs        — Win32 打印后台原始写入
```

## 构建与运行
```powershell
cd F:\项目\ZebraBleLabelPrinter
dotnet build -c Release
.\bin\Release\net8.0-windows10.0.19041.0\ZebraBleLabelPrinter.exe
```

## 注意
- 目标框架 `net8.0-windows10.0.19041.0`，需要 Windows 10 1809+；
  蓝牙扫描需要在 Windows 设置里打开蓝牙开关。
- 首次启动若打印机下拉为空：先在 Zebra 官网装 **ZDesigner Driver v5**，
  选 "USB" 端口，安装完成后点界面上的"刷新打印机列表"。
