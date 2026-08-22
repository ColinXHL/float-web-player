---
name: publish-akasha-navigator
description: Interactively decide whether AkashaNavigator should be released and recommend the next version, then publish it end to end after user confirmation. Use when the user asks to 发布、发版、判断要不要升版本、下一个版本号、制作安装包、更新 Alpha/稳定版、同步 GitHub/CNB，或修复“检查更新检测不到新版本”。 Covers change analysis, semantic versioning, release notes, validation, PR merge, GitHub Actions packaging, GitHub Release publication, Qiniu notice.json update, CDN verification, and updater visibility.
---

# Publish AkashaNavigator

Treat a release as incomplete until both the binaries and the online update
manifest are published.

## Make the version decision interactively

Always complete this section before editing a version, release notes, tags, or
external release state.

1. Work from the repository root.
2. Read:
   - `AkashaNavigator/AkashaNavigator.csproj`
   - `release_badges.md`
   - `.github/workflows/publish.yml`
   - `.github/workflows/update_notice.yml`
3. Run `gh auth status` and confirm the repository is
   `ColinXHL/akasha-navigator`.
4. Fetch `origin` and tags, then collect the decision context:

```powershell
& .agents/skills/publish-akasha-navigator/scripts/get-version-context.ps1
```

5. Inspect:
   - the project version
   - the latest published Release and tag
   - the online stable and Alpha manifest versions
   - commits and diff since the latest published tag
   - staged, unstaged, and untracked work
6. Decide whether a release is warranted:
   - Recommend **no release yet** for incomplete work, internal-only
     experiments, or changes with no user-facing/package impact.
   - Recommend a release for user-visible fixes or features, security and data
     integrity changes, packaging/updater changes, or fixes users are waiting
     for.
   - Any new public release must have a unique version. If the project version
     is already published, it must change.
   - If the project already contains an unpublished version that matches the
     change scope, keep it instead of incrementing again.
7. Recommend the version:

| Current release state | Change scope | Recommendation |
|---|---|---|
| Alpha train | ordinary fixes or compatible features | increment `alpha.N` |
| Alpha train | next planned feature line | start the next minor Alpha |
| Stable | backward-compatible bug fix | patch |
| Stable | backward-compatible feature | minor |
| Stable | breaking behavior/API/data change | major |
| Alpha is ready for general use | stabilization only | promote the same core version to stable |

Do not treat commit prefixes mechanically. Explain the user and compatibility
impact that drives the recommendation.

8. Present a short decision prompt in this shape:

```text
当前已发布：v<X>
本次改动：<one-line summary>

建议：<不发布 / v<Y>>（推荐）
理由：
- <reason>
- <reason>

备选：
1. <alternative and tradeoff>
2. <alternative and tradeoff>

你确认采用哪个方案？
```

Offer at most three mutually exclusive choices, put the recommendation first,
and stop for explicit user confirmation. Do not edit the version, release
notes, tags, PR state, or workflows while waiting.

If the user already supplied an exact version and explicitly requested its
publication, treat that as confirmation after checking that it is valid,
unused, and compatible with the changes. Surface conflicts instead of silently
choosing another version.

9. Preserve unrelated working-tree changes. Never use `git add -A` in a
   mixed worktree.
10. Never reuse or overwrite an existing tag or Release.

## Prepare the source

Start only after the user confirms the release decision and exact version.

1. Create a `codex/` release or fix branch before committing.
2. Implement the requested changes and add focused regression tests.
3. Update `<Version>` in `AkashaNavigator/AkashaNavigator.csproj`.
4. Replace `release_badges.md` with notes for exactly `## v<VERSION>`.
   Include correct installer and portable download links.
5. Run:

```powershell
& .agents/skills/publish-akasha-navigator/scripts/test-release-readiness.ps1 `
  -Version <VERSION>
```

6. Require `dotnet test AkashaNavigator.sln --no-restore` and
   `git diff --check` to pass. Existing skipped tests are acceptable; new
   failures are not.

## Land the release commit

1. Inspect the final diff and stage only intended paths.
2. Commit with a concise conventional message.
3. Push the `codex/` branch to `origin`.
4. Open a PR against `main`, with:
   - root cause and user impact
   - exact validation results
   - version being published
5. For an explicit user request to publish, mark the PR ready and merge it
   after required checks pass. Prefer squash merge.
6. Fetch and fast-forward local `main`. Confirm the version commit is at
   `origin/main`.

Do not run the release workflow from an unmerged feature branch.

## Build and publish the binaries

Map channels as follows:

| Version | `publish.yml` channel | Release kind |
|---|---|---|
| contains a prerelease suffix | `dev` | prerelease |
| stable semantic version | `release` | stable |

Trigger:

```powershell
gh workflow run publish.yml --ref main `
  -f version=<VERSION> `
  -f channel=<dev-or-release> `
  -f create_release=true `
  -f push_to_cnb=true
```

Capture the run URL/ID and monitor it until terminal. Require all applicable
jobs to succeed:

- `validate`
- `build_dist`
- `build_installer`
- `Trigger CNB Build`
- `Create GitHub Release`

If a job fails, inspect its logs and stop. Do not publish partial artifacts.

The workflow creates a draft GitHub Release and synchronously publishes the same
two build artifacts to the stable or Alpha CNB repository. The `Trigger CNB Build`
job must complete its public SHA-256 readback before it succeeds. Verify both GitHub
assets exist:

- `AkashaNavigator.Install.<VERSION>.exe`
- `AkashaNavigator_v<VERSION>.7z`

Then publish the draft:

```powershell
# Prerelease
gh release edit v<VERSION> --draft=false --prerelease

# Stable
gh release edit v<VERSION> --draft=false --latest
```

Re-read the Release and confirm it is no longer a draft. A successful
`Trigger CNB Build` job means the CNB release completed and both public CNB assets
matched the GitHub Actions artifacts. If recovery is needed after GitHub publication,
run `publish_cnb.yml` with the same version and require its readback step to pass.

## Publish the updater manifest

This step is mandatory and separate from `publish.yml`. Omitting it makes the
application report that the old version is still current.

1. Read the current online manifest:

```powershell
Invoke-RestMethod 'https://update.fisheepx.cn/notice.json'
```

2. Preserve its `min_required_version` unless the user explicitly requests a
   forced-upgrade policy change.
3. Use channel `alpha` for prereleases and `stable` for stable versions.
4. Keep source `qiniu` unless the repository configuration changed.
5. Trigger:

```powershell
gh workflow run update_notice.yml --ref main `
  -f version=<VERSION> `
  -f channel=<alpha-or-stable> `
  -f min_required_version=<CURRENT_MINIMUM> `
  -f notes='<CONCISE_USER_FACING_NOTES>' `
  -f source=qiniu
```

6. Monitor the run and require these jobs to succeed:
   - `validate`
   - `build_notice`
   - `upload_notice`
7. Verify both a cache-busting request and the normal URL return the new
   channel version:

```powershell
$bust = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
Invoke-RestMethod "https://update.fisheepx.cn/notice.json?ts=$bust"
Invoke-RestMethod 'https://update.fisheepx.cn/notice.json'
```

Do not report the release complete before this verification passes.

## Final report

Provide:

- released version and whether it is prerelease/stable
- GitHub Release, PR, binary workflow, and notice workflow links
- installer and portable download links
- test totals
- updater-manifest version observed online
- precise CNB status: completed only if verified, otherwise triggered

If the client still cannot detect the release, inspect:

- installed executable `ProductVersion`
- `enablePrereleaseUpdate` in `User/Data/config.json`
- `User/Data/Update/notice-cache.json`
- `User/Data/Update/notice-state.json`
- the latest application log
