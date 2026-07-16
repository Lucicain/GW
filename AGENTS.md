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
- Organize each release under its date/version with short `新增与调整` and
  `修复` lists. Do not add a `未改动` section. Mention an unchanged behavior
  only when omitting it would make a changed mechanic materially misleading.
- Summarize existing playable systems by feature, not as a complete manual.
  Let players discover secondary behavior in play; do not fill the README with
  internal reasoning, exhaustive formulas, long FAQs, test history, or every
  edge case.
- Write from the player's point of view. Retain exact chances, ranges, damage,
  cooldowns, durations, multipliers, or other values only when they are needed
  to understand a changed mechanic or make a gameplay decision.
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
