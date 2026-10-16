# ZebraBleLabelPrinter

WPF (.NET 8) + MVVM 工程，配合 **Zebra 110XiIII Plus 600DPI**，通过 USB（ZDesigner 驱动）打印蓝牙 MAC 条码。

## 功能
- 启动后自动用 `BluetoothLEAdvertisementWatcher` **持续扫描** BLE 广播（Stopped 自动重启）
- 主页列表实时显示设备，**按 RSSI 信号强度降序排序**，RSSI 条 + dBm 数值
- 顶部"名称过滤"框：**不区分大小写** 包含匹配
- 每行一个 **打印条码** 按钮，点击后把该设备 MAC 用 **CODE128 条形码** 发到 Zebra
- 右上角下拉选择打印机，自动挑选名字含 "Zebra" 的；可点"刷新打印机列表"

## 打印链路
- 用 Windows 打印后台处理程序（Win32 `OpenPrinter` / `WritePrinter`）**直接写 ZPL 原始字节**，
  前提：Zebra 110XiIII Plus 通过 USB 接好，已安装 ZDesigner 驱动（即"USB 串口驱动"），
  系统打印机列表里能看到这台打印机。
- ZPL 标签尺寸按 **100 mm × 60 mm**（600 DPI ≈ 23.62 dots/mm）：
  `^PW2360` × `^LL1417`；CODE128 模块宽度 2 dots、高度 350 dots。
- 如果实际标签尺寸不同，改 `Services/ZebraPrinterService.cs::BuildMacBarcodeZpl` 里的
  `^PWxxxx` / `^LLxxxx` / 坐标即可。

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
