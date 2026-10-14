# DevicesPane

设置 → 设备分区的界面（迁自旧 `Views/SettingsDevices.fs` 的界面部分与 `QrCodeView`）。根规则见项目根目录 AGENTS.md。

- `DevicesPane(look)`；`View` 是整块；`Render state` 整块重建一帧（与旧实现每次 render 相同）。
- `DevicesPaneState`：`Invite`（`Unavailable 文字` / `Offer 被拒说明` / `Code(分组码, 逐字读法, 倒计时文字, 是否过期, 地址)`）、`List`（`Note 文字` / `Rows 已排序的行`）、`Editing`（正在重命名的设备 id）。
- `DevicesPaneAddress`：`Reachable(url, 二维码矩阵)` / `LoopbackOnly(首个局域网地址与矩阵, 其余地址)` / `NoAddress`。
- `QrPlate(text, matrix, moduleSize)`：白底黑模块、4 模块静区（与主题无关，扫码要对比度）；模块边长是活值。
- 事件：`AddClicked`、`RegenerateClicked`、`CloseInviteClicked`、`RefreshClicked`、`RenameClicked id`、`RenameSaved(id, 去空白的新名)`（空名也发，由组合者提示）、`RenameCancelled id`、`RevokeClicked id`。编辑时 `NameBox` 是名称输入框，打开编辑器后焦点自动进入它。
- 读取设备列表、有界等待（BoundedWait）、倒计时时钟、地址推断（DevicesModel.inviteTarget）、二维码矩阵（DevicesModel.qrMatrix）、排序与行文字都由组合者接。
- 自动化 id 与读屏名与旧实现逐字相同（`devices-add`、`devices-code`、`devices-countdown`、`devices-address`、`devices-qr`、`devices-regenerate`、`devices-close-invite`、`devices-list`、`devices-row-<id>`、`devices-rename-<id>`、`devices-revoke-<id>`、`devices-name-<id>`、`devices-name-save-<id>`、`devices-name-cancel-<id>`、`devices-refresh`、`devices-invite-unavailable`）。

## 外观

- `DevicesPaneLook`：公共字段（画笔、字号、间距、圆角、输入壳度量、等宽字体、`Glyph` 图标槽）加 `CodeTracking`（配对码字距）、`QrModuleSize`。全部是主题管理者的活实例。
- 用到的样式类（compose/Theme）：按钮 `surface button-* hover-*`（吊销为 `button-danger hover-danger`）、卡 `grouping-card`、分节标签 `section-label`、输入壳 `surface hover-raised focus-soft`（出错时组合者的 FieldCheck 挂 `invalid`，聚焦描边让位）。

## 证据与旧测试

- DevicesUiTests：`device list marks current desktop online and revoked and sorts revoked last`（呈现部分）、`revoke button passes the device to the action`、`rename edits inline trims and sends the new name`（呈现与事件部分；空名提示在组合者）、`only a desktop capable connection sees the add device button`、`invite card shows grouped code address qr and countdown`（呈现部分；时钟在组合者）、`loopback only host explains the listen setting and uses a lan address for the qr`、`rejected invite explains that only the desktop app can add devices`、`qr view measures with a quiet zone and draws on a white background`
- 组合者 SettingsScreen 覆盖：`device list read without a reply times out with the older host hint`、`a disconnect while loading explains the host may be too old`、`selecting the devices section requests the list once`

## 与旧实现的差异

- 「完成」不再由面板自己清掉邀请：发 `CloseInviteClicked`，组合者清掉后重绘。可见行为不变。
- 空名不由面板弹提示：`RenameSaved` 带空串，组合者弹提示并保持编辑。
