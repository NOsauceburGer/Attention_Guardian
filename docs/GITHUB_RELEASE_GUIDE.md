# Attention Guardian 首次 GitHub 发布指南

本指南记录规范的首次发布流程。Attention Guardian v0.1.0 已按该流程完成首次提交、
推送、Tag、安装包摘要核验和 GitHub Release；后续版本继续复用第 7、8 节。

## 1. 发布前确认身份与仓库可见性

1. 在 GitHub 创建一个空仓库，建议名称为 `AttentionGuardian`。
2. 选择 Public 才是公开开源；如果想先检查，可以先设为 Private，确认后再公开。
3. 不要让 GitHub 自动生成 README、LICENSE 或 `.gitignore`，因为本地已经存在。
4. Git 提交邮箱会公开显示。需要隐藏真实邮箱时，在 GitHub 的 Email 设置中启用
   `Keep my email addresses private`，并使用 GitHub 提供的 `noreply` 邮箱配置 Git。

## 2. 第一次提交前检查

在 Guardian 根目录执行：

```powershell
git status --short
git check-ignore -v artifacts
git diff --check
```

重点确认以下内容不会进入提交：

- `artifacts/`、`bin/`、`obj/`
- `*.db`、`*.db-wal`、`*.db-shm`
- `*.log`
- 本机临时文件、压缩备份、密钥和个人配置

然后先查看将要加入的文件，而不是直接提交所有未知内容：

```powershell
git add --dry-run .
git add .
git status
```

## 3. 创建首次提交并连接 GitHub

确认暂存列表正确后：

```powershell
git commit -m "Release Attention Guardian v0.1.0"
git remote add origin https://github.com/<你的账号>/AttentionGuardian.git
git push -u origin main
```

如果 GitHub 仓库默认分支不是 `main`，先在 GitHub 设置中统一默认分支，不要用强制
推送覆盖不确定的远程历史。

## 4. 在 GitHub 上做发布前检查

- README 首页是否能说明产品、安装方式和已知限制。
- LICENSE 是否显示为 GPL-3.0。
- SECURITY、SUPPORT、CONTRIBUTING 和行为准则是否可访问。
- 仓库中是否没有数据库、日志、压缩备份、个人路径和构建产物。
- Actions、Issues 和 Pull Requests 是否按需要开启。
- 建议为 `main` 增加分支保护：PR 合并、测试通过、禁止强制推送。

## 5. 创建 v0.1.0 Tag

Tag 必须指向已经验证并准备发布的那个提交：

```powershell
git tag -a v0.1.0 -m "Attention Guardian v0.1.0"
git push origin v0.1.0
```

不要在打 Tag 后继续修改同名安装包。任何变化都应产生新提交和新版本号，避免同一个
版本对应多个不同 SHA-256。

## 6. 创建 GitHub Release

1. 打开仓库的 Releases，选择 `Draft a new release`。
2. 选择已有 Tag `v0.1.0`，标题填写 `Attention Guardian v0.1.0`。
3. 从 `CHANGELOG.md` 提炼用户能理解的新增、修复和已知限制。
4. 上传：
   `artifacts/v0.1.0-public-clean/AttentionGuardian-0.1.0-win-x64-setup.exe`
5. 在正文明确写出：

```text
SHA-256:
AB8E7D6BBD5CCD894C48B576C970276794D894CC1AF056EBD6C2C575DE0140F7
```

6. 明确说明 v0.1.0 安装包尚无 Authenticode 代码签名，Windows 可能显示来源保护提示。
7. 先保存为 Draft，自己下载附件并重新计算 SHA-256；确认一致后再 Publish release。

## 7. 发布后验证

最好在一个从未运行过 Attention Guardian 的 Windows 用户或虚拟机中：

- 下载 GitHub Release 附件；
- 核对 SHA-256；
- 安装并启动；
- 确认没有预置事件；
- 检查应用图标、开始菜单、添加/管理、通知和卸载；
- 在 GitHub 创建一个测试 Issue，确认模板和支持文档正常。

## 8. 后续版本的规范节奏

1. 功能修改进入分支。
2. 自动化测试和人工验收通过。
3. 更新版本号与 CHANGELOG。
4. 合并到 `main`。
5. 从确定提交创建唯一 Tag。
6. 构建一次不可变安装包，记录 SHA-256。
7. 创建 Draft Release，复验下载件后公开。

不要把数据库、用户日志或测试数据放进 Git；不要覆盖已经公开的同版本附件；不要在
没有证书时声称安装包已签名。
