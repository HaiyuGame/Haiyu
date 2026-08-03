# Haiyu.Publish

## Architecture

- `MainWindow.xaml`: FluentWPF view; controls only use bindings and commands.
- `ViewModels/MainWindowViewModel.cs`: UI state and CommunityToolkit.Mvvm commands.
- `Services/ReleaseBuildService.cs`: release pipeline, filesystem work, and child processes.
- `Behaviors/AutoScrollTextBoxBehavior.cs`: log scrolling without view event handlers.
- `MainWindow.xaml.cs`: composition only; it creates and assigns the view model.

本工具把 EXE 渠道的手工发版流程合并为一次操作：

1. 修改 `WutheringWavesTool/App.xaml.cs` 中的 `AppVersion`，再使用 `win-x64` 配置发布 `Haiyu.csproj`。
2. 递归清理发布目录中的全部 `.pdb` 文件，再压缩到安装项目的 `Resources/program.zip`。
3. 修改 `Resources/Resource1.resx` 中的 `Version`。
4. 临时将安装文件和卸载文件资源都指向零字节的 `Simple.txt`，以普通 `Release` 方式构建轻量 WPF 卸载程序，并保存为 `Resources/uninstall.exe`（不执行 publish）。
5. 第二遍构建安装程序，将完整的程序包和卸载程序嵌入最终 EXE。
6. 输出 `artifacts/release/Haiyu-{version}-win-x64.exe`。

> 安装程序本身同时承担卸载入口，所以必须执行两遍构建。第一遍的两个二进制资源均为空 TXT，避免卸载程序嵌入主程序或递归嵌入旧卸载程序。

## 使用

在解决方案中启动 `Haiyu.Publish`，填写版本号，点击“生成 EXE 安装包”。下方日志会显示实际编译进度与错误。

构建成功后，Git 中需要提交的发版资源是：

- `src/Setup/Project.WPFSetup/Resources/Resource1.resx`
- `src/Setup/Project.WPFSetup/Resources/program.zip`（如果仓库跟踪该文件）
- `src/Setup/Project.WPFSetup/Resources/uninstall.exe`（如果仓库跟踪该文件）

`artifacts/` 是本机发布输出，已加入 `.gitignore`。
