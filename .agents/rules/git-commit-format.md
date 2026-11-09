---
trigger: always_on
---

# Commits and pushes

Commit or push when the user's request or standing instructions authorize that action. Reuse valid authorization. Otherwise leave the verified changes in the working tree and report their status. Create, update, or merge a PR only with authorization for that action.

Use one lowercase type, a colon and space, and a concrete Russian summary of the completed result. Prefer up to 72 characters and omit the final period. Separate an optional body with a blank line.

Types: `add` feature or content, `fix` bug, `tweak` small adjustment, `remove` deletion, `refactor` structure, `docs` documentation, `test` tests, `build` build or dependencies, `ci` automation, `chore` maintenance, `upstream` upstream sync, `revert` rollback. Use completed-result wording and correct agreement in Russian.

Keep imported commit messages and existing history. For merge commits, use `upstream` for upstream sync and `chore` for other merges.
