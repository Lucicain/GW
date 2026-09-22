# 正式发布检查清单

> 从 `maintenance-plan.md` 原样迁出的参考资料，正文未改。

## Formal release checklist

1. Build the diagnostics-enabled `Release` against Bannerlord `1.4.7` and keep
   it in the live test module.
2. Confirm the live module has no `Assets`, `AssetSources`, or
   `RuntimeDataCache` directory.
3. Confirm both runtime TPAC hashes above.
4. Confirm both player READMEs describe functions/results only, retain exactly
   the newest two formal versions, and match their live copies.
5. Build a diagnostics-disabled player DLL into a separate staging directory
   with live deployment disabled. Decompile it and confirm all diagnostic write
   methods are inert and no test log can be created.
6. Stage one top-level `GreyWarden` directory without `tools`, scripts, logs,
   developer notes, editor binaries, PDBs, or source assets.
7. Commit and push the release source and documentation to GitHub as part of
   the same formal release task.
8. Create the versioned ZIP and its `.sha256` file directly under the
   game's `Modules` directory, never under `Modules\GreyWarden` or `_Module`.
9. Inspect ZIP paths, verify the packaged DLL hash equals the separate player
   build, and confirm no diagnostic/test content exists; then create/update the
   GitHub release and upload the matching ZIP and checksum.
10. Run at least one battle that renders the black shield, exit the client, and
   check delayed Windows Application/WER/dump state.
