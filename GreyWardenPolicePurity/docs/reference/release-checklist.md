# 玩家包交付检查清单

当前游戏版本、live布局和正式发布基线以[构建与部署](../state/build-and-deploy.md)为准。测试版只出本地ZIP并推送源码，不创建Release或tag；测试版不带监控。（依据：用户裁定2026-10-15、2026-10-16）

1. 读取 `.claude/rules/release-notes.md`，两份玩家说明相对上一正式玩家包描述变化，并同步到live。
2. 隔离重建玩家DLL：`dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild -p:DeployToLiveModule=false -p:GwpDiagnosticsEnabled=false`。
3. 对该DLL运行 `Verify-GameCompat.ps1`；检查诊断标记的ASCII、UTF-16LE均不存在。
4. 用 `New-PlayerPackage.py` 创建只有一个GreyWarden顶层目录的ZIP；包括最新 `AssetPackages/pack0.tpac`，排除美术源、编辑器、工具、日志与PDB。
5. 解压核对逐项哈希，确认玩家DLL等于隔离构建；在 `build-check/package-<version>/` 保留manifest及DLL证据。校验成功后，游戏Modules父目录只保留最新ZIP与校验文件。
6. live重建开发监控DLL，Client/Editor一致；保留Assets、AssetSources、RuntimeDataCache，不保留AssetPackages和DsAssetPackages。运行 `Verify-LiveModule.ps1`。
7. 完成适用离线回归、StateFreshness与开发记录；稳定点存档并按用户要求推送源码。
8. 只有用户明确要求线上发布时才创建tag/Release并上传，随后重新下载校验。此次不执行。（依据：用户裁定2026-10-15）

游戏内验收仅由用户进行，agent不启动或操控游戏。（依据：用户裁定2026-09-25）
