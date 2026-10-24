Haiyu Chart控件

此控件由Gpt编写，通过Win2D高性能渲染引擎渲染图表，替代LiveChart带来的渲染高内存占用问题。

## XAML + C# 分层

- `ChartSurface.xaml`：共享视觉层，声明 Win2D 画布模板、绘图区和提示框覆盖层。
- `ChartSurface.xaml.cs`：视觉层的代码后置，负责画布挂载/移除以及提示框跟随鼠标、边缘避让。
- `ChartControlBase.cs`：公共交互、动画和资源生命周期。卸载时释放 Win2D 资源，重新加载时从 XAML 模板创建新画布。
- `CartesianChart.cs` / `PieChart.cs`：数据映射、命中测试和 Win2D 绘制。密集数据继续使用 Win2D，避免为每个数据点创建 XAML 元素。
- `ChartModels.cs`：序列、坐标轴等公共数据模型。

修改提示框背景、边框、字体、内边距、圆角时，直接编辑 `ChartSurface.xaml`。提示框使用与应用 ToolTip 相同的主题资源，随浅色、深色和高对比度主题切换；覆盖层不参与鼠标命中测试。

提示框通过 `Canvas.Left/Top` 定位，不使用 `Margin`，避免上次位置进入下一次 `DesiredSize` 测量。默认显示在鼠标右下方，靠近边缘时翻转并限制在图表范围内。

现有 `CartesianChart` / `PieChart` 的属性、事件及页面用法保持不变。
