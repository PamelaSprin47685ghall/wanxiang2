/** 原仓度量的浏览器投影。
 *  数值来自 libs/ThemeMetrics、libs/SpacingRamp、compose/DesignRhythm。
 *  组件不得再写自己的断点或字号。画布测量会让长文本撑破列，
 *  所以这些数只进样式变量和下面两个纯函数，排列交给文档网格。
 */

export const Metric = {
  fontMicro: 10.5,
  fontCaption: 11.5,
  fontSmall: 12.5,
  fontBody: 13.5,
  fontReading: 14.5,
  fontTitle: 16,
  fontHeading: 19,
  fontDisplay: 25,
  sidebarWidth: 284,
  readingWidth: 748,
  userMessageMaxWidth: 560,
  barHeight: 52,
  iconButton: 30,
  iconGlyph: 16,
  compactActionTarget: 44,
  rowMinHeight: 44,
  menuItemMinHeight: 34,
  textButtonMinHeight: 34,
  composerInputMinHeight: 30,
  composerMaxHeight: 240,
  composerModelMaxWidth: 240,
  composerModelCompactMaxWidth: 150,
  messageGap: 24,
  emptyStateMaxWidth: 480,
  menuMaxHeight: 380,
  startWorkspaceMinHeight: 600,
  compactBreakpoint: 720,
  wideLayoutBreakpoint: 1024,
  formSingleColumnBreakpoint: 440,
  paletteDialogWidth: 560,
  settingsDialogWidth: 520,
  /** 设置是一整屏，不是 520 的小编辑层。收进对话框时用紧凑断点当上限。 */
  settingsSurfaceWidth: 720,
  confirmDialogWidth: 420,
  dialogContentMaxHeight: 560,
  logoSidebar: 26,
  logoRadiusRatio: 0.2894736842105263,
  logoGlyphRatio: 0.42,
  scrollBottomThreshold: 48,
} as const;

/** 与 CSS `@media (max-width: 719px)` 同一条，不要另写 839。 */
export const COMPACT_MQ = "(max-width: 719px)";
/** 起步居中只在高度达到 StartWorkspaceMinHeight 时成立。 */
export const SHORT_WORKSPACE_MQ = "(max-height: 599px)";

export function isCompactWidth(width: number) {
  return width < Metric.compactBreakpoint;
}

/** LayoutPolicy.horizontalInset。四档都落在间距阶梯上。 */
export function horizontalInset(width: number) {
  if (width < Metric.formSingleColumnBreakpoint) return 16;
  if (width < Metric.compactBreakpoint) return 24;
  if (width < Metric.wideLayoutBreakpoint) return 32;
  return 48;
}

/** 算法 W：无消息、未在回复、窗口够高 → 起步；否则输入区回底部。 */
export function composerPlacement(height: number, messageCount: number, running: boolean) {
  if (messageCount === 0 && !running && height >= Metric.startWorkspaceMinHeight) return "start" as const;
  return "dock" as const;
}
