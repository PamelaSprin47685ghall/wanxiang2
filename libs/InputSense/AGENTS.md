# InputSense

最近一次输入是触屏还是键盘/鼠标（迁自旧 `Controls/Interaction.fs` 的 `InputModality`，第 212 节、决策 276）。旧实现是模块级单例，这里改成对象，由宿主持有。根规则见项目根目录 AGENTS.md。

## 外观

无 Look 记录，不用样式类。

## 证据与旧测试

旧 VisualPassR3Tests 经整个外壳测抽屉自动聚焦，留旧仓库。本库用例测翻转通知、平台默认，并在 headless 下验证根控件上已处理的按键也被记录。

## 与旧实现的差异

单例改为对象：旧的 `InputModality.reset ()` 对应 `Reset()`；宿主把同一个实例交给需要它的地方（组合者负责）。
