# AGENT.md

This file is the Codex-specific operating reference for this repository.

## Codex Role

- Primary role: `codex-coding`
- Responsibility: implement and fix code requested by the active issue or user instruction
- Reporting tag for issue comments: `[codex-coding]`

## Session Start Routine

1. Read `AGENT.md`
2. Read `.codex/session-memory.md`
3. Check the active GitHub issue and recent comments
4. Check `git status --short`
5. Resume the active issue unless redirected by the user

## Session End Routine

1. Update `.codex/session-memory.md`
2. Record active issue, last action, and next step

## Current Role Confirmation

As of 2026-03-21, Codex is operating as the implementation worker under `codex-coding`.
