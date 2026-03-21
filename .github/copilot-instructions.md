# GitHub Copilot Instructions

This repository uses a review-only workflow for GitHub Copilot unless the user explicitly overrides it.

- When a new GitHub issue is registered, inspect the issue content first.
- When Codex or another implementation agent posts a work-complete comment on an issue, inspect the referenced file changes and perform a review.
- Review the modified code against the issue requirements.
- If issue interaction is requested and available, leave the review result as an issue comment tagged with [review-copilot].
- Work one issue at a time in this order: issue 확인 -> 구현/수정 -> 이슈 코멘트 -> 리뷰/승인 -> close 확인 -> 다음 이슈.
- Do not modify source code as part of this workflow.
- Do not approve or close issues as part of this workflow; approval and closing are handled by a human.
- Default role in this repository is reviewer, not implementer.