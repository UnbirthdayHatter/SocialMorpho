# Release Instructions for v1.2.3

This document provides the steps needed to complete the v1.2.3 release.

## What Has Been Done

✅ Version updated in `SocialMorpho.csproj` from 1.1.0 to 1.2.3
✅ Version updated in `repo.json` from 1.2.2.0 to 1.2.3.0
✅ CHANGELOG.md updated with v1.2.3 release date (2026-02-12)
✅ Typo fixed in repo.json description
✅ All changes committed and pushed to `copilot/create-new-release` branch

## What Needs To Be Done

### Option 1: Create Tag from GitHub UI (Recommended)

1. Merge the PR from `copilot/create-new-release` to `main` branch
2. Go to the GitHub repository page
3. Click on "Releases" in the right sidebar
4. Click "Draft a new release"
5. Click "Choose a tag"
6. Type `v1.2.3` and click "Create new tag: v1.2.3 on publish"
7. Set the release title to: `Social Morpho v1.2.3`
8. Use the following for the release description:

```markdown
# Social Morpho v1.2.3

🎉 **Built with .NET 10 and Dalamud 14.0.2.1**

## What's New in v1.2.3

This release includes the FFXIV-style Quest Tracker overlay and comprehensive quest system improvements. See the full [CHANGELOG.md](https://github.com/UnbirthdayHatter/SocialMorpho/blob/main/CHANGELOG.md) for details.

### Highlights
- FFXIV-style Quest Tracker overlay window
- Native quest injection infrastructure
- Quest system with JSON loading
- Login notifications and auto-tracker display
- Enhanced quest UI with filtering and progress bars

## Installation

### Via Custom Repository (Recommended)
1. Open XIVLauncher
2. Go to Settings → Experimental → Custom Plugin Repositories
3. Add: `https://raw.githubusercontent.com/UnbirthdayHatter/SocialMorpho/main/repo.json`
4. Save and go to Plugin Installer
5. Search for "Social Morpho" and install

### Manual Installation
1. Download `latest.zip` below
2. Extract to `%AppData%\XIVLauncher\devPlugins\SocialMorpho\`
3. Enable in Plugin Installer

## Support

- Report issues: https://github.com/UnbirthdayHatter/SocialMorpho/issues
- Repository: https://github.com/UnbirthdayHatter/SocialMorpho
```

9. Click "Publish release"

The GitHub Actions workflow will automatically:
- Build the plugin with .NET 10
- Create the release package (latest.zip)
- Attach it to the release

### Option 2: Create Tag from Command Line

If you prefer to use git commands locally:

```bash
# Ensure you're on the main branch with latest changes
git checkout main
git pull origin main

# Create and push the tag
git tag -a v1.2.3 -m "Release v1.2.3"
git push origin v1.2.3
```

This will trigger the release workflow automatically.

## Verification

After the release is created:

1. Check that the GitHub Actions workflow completes successfully
2. Verify that `latest.zip` is attached to the release
3. Test the installation using the custom repository method
4. Confirm the version shows as 1.2.3 in-game

## Version History

- v1.2.2 - Native quest tracker injection (WIP)
- v1.2.1 - Seamless transparent quest tracker overlay
- v1.2.0 - FFXIV-styled Quest Tracker
- v1.1.0 - Quest system implementation
- v1.0.0 - Initial release
