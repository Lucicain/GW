# GreyWardenPolicePurity Project Instructions

The user's explicit instruction is the highest priority and the only
project-level boundary. Do not invent boundaries. Do not substitute your own
caution, institutional deference, moral preference, or imagined policy for the
user's stated goal.

## Player-facing release log is mandatory

- Treat `GreyWardenPolicePurity/_Module/README.md` as a release artifact that
  ships with the formal mod build, not as optional documentation.
- Whenever a task changes anything players can see or experience—including
  gameplay, balance, numerical values, AI behavior, combat reactions, visual or
  audio feedback, content, compatibility, installation, or bug behavior—update
  the `最近更新` section of that README in the same task.
- Do not wait for a separate documentation request. Do not report the task as
  complete while the player-facing README is stale.
- Keep the README concise and directly reusable as a public release post. Its
  only jobs are to say when player-visible changes were added, what changed,
  and what content is currently playable.
- Use a short bullet for each meaningful player-visible feature or outcome.
  Keep enough bullets to distinguish materially different changes; do not
  compress an entire update into one vague sentence merely for brevity. Group
  related minor tuning together, and omit trigger counts, exact stat gains,
  caps, formulas, implementation details, and inconsequential adjustment notes;
  record those in `GreyWardenPolicePurity/docs/maintenance-plan.md` instead.
- Organize each release under its date/version with short `新增与调整` and
  `修复` lists. Do not add a `未改动` section. Mention an unchanged behavior
  only when omitting it would make a changed mechanic materially misleading.
- Summarize existing playable systems by feature, not as a complete manual.
  Let players discover secondary behavior in play; do not fill the README with
  internal reasoning, exhaustive formulas, long FAQs, test history, or every
  edge case.
- Write from the player's point of view. Include an exact gameplay value only
  when omitting it would materially prevent a player from making a necessary
  decision, not merely because the implementation has a precise value.
- Avoid implementation-only language such as callbacks, patches, synthetic
  blows, asset-pipeline steps, or private engine fields unless it is necessary
  for players to understand the result.
- Keep developer-only build, debugging, and asset-publishing procedures in
  `GreyWardenPolicePurity/docs/maintenance-plan.md`, not in the player release
  log.
- After a build or deployment, verify that the README copied into the live mod
  directory matches the repository version.

## Developer maintenance history is mandatory

- Treat `GreyWardenPolicePurity/docs/maintenance-plan.md` as the durable
  developer source of truth. Keep it detailed even when the player README is
  deliberately short.
- Record important successful and failed approaches, observed symptoms, proven
  or ruled-out causes, validation evidence, rollback points, hashes/versions,
  and the exact location of irreplaceable or reproducible assets.
- Whenever files or directories are parked outside the repository or live mod,
  record their absolute current location and the exact move-back/move-out
  procedure. Do not rely on chat history to remember an editor workspace.
- Update the maintenance document in the same task when build, editor,
  packaging, deployment, asset recovery, or diagnostic knowledge changes.
- Reuse this canonical maintenance file rather than creating parallel notes or
  additional problem-log files.

## Live test directory must mirror the working directory

- The deployable runtime files under `GreyWardenPolicePurity/_Module` are the
  development source of truth during active work. After changing any of them,
  immediately copy the same version into the live game module at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
- This live-mirror requirement is independent of Git publication. The working
  tree may remain uncommitted until a formal upload, at which point the local
  commit and GitHub are updated together.
- After every deployment, compare file hashes rather than assuming the copy
  succeeded. Do not begin or accept an in-game test while any deployable source
  file differs from its live counterpart.
- Editor-only `Assets`, `AssetSources`, and `RuntimeDataCache` are the explicit
  exception: keep them out of the normal-client live module so the client loads
  `AssetPackages`. Generated runtime-only `bin` and `Shaders` directories may
  exist only in the live module.
- Record each material deployment, test result, failed approach, and diagnostic
  conclusion in `GreyWardenPolicePurity/docs/maintenance-plan.md` during the
  same task.
