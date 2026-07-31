# Changelog

All notable changes to DM-X Video Player will be documented in this file.

## [1.6.0] - 2026-07-31

### ⭐ New Features

- Allow using DM-X Video Player as the default for chosen file extensions and launch DM-X VP by clicking on video files
- App is now released on github as portable or with installer
- Automatic updates using Velopack based on github releases

### 🐛 Bug Fixes

- Avoid bad artifact image on stop, force to go to ne next real frame

### 🧩 Misc

- Improve Timecode & tempo track panel (add TC before TimeCode, and modify order and add space for better readibility)
- Play/pause button act as load button if no video was loaded


## [1.5.0] - 2026-07-29

### ⭐ New Features

- About window: Allow to close "About" windows with shortkey "i"
- About window: Add link licence and changelog (on github)
- Timecode & tempo track panel: 
  - panel has now a magnetic snap on app horizontal center
  - opacity and font size can be changed in settings
- Review settings windows design to reduce windows size

### 🐛 Bug Fixes

- Allow to move in video based on horizontal mouse wheel (not only vertical one's)
- Fix effective beat computation (no special case for ternary rythm signature)

### 🧩 Misc

- Reorganize project files (Views / Controls / Services)

### 🔨 Dependency Upgrades

- update Avalonia to 12.1.1
- update FluentIcons.Avalonia.Fluent to 2.1.333

## [1.4.0] - 2026-07-28

### ⭐ New Features

- Use 'P' instead of 'S' for Parameters window. 'P' can also close the window if opened.
- Remove default video folder settings, save last one used instead.
- Add time signature and bar / beat display (read from .smt cubase tempo track)
- Allow to move in video based on arrow key (left/right) 

### 🐛 Bug Fixes

- Prevent rare exception on app close when a video was playing

### 🧩 Misc

- Rename app to to DM-X Video Player
- Simplify version management with Directory.Build.props

## [1.3.0] - 2026-07-24

### ⭐ New Features

- Allow to move timecode / BPM panel
- Adapt displayed components with application width 
- Set a minimum width and height for application
- Improve progression bar UI
- Refacteur windows/fullscreen management with controls overlay display (fix in windows mode, automatic hide in fullscreen)
- Create a nice icon for application
- Rename DM Video player to DM-X Video Player (Didier Martini, Xtream Video Player)
- Add about windows
- Manage localisation with english & french

### 🐛 Bug Fixes

- Allow to chose drive and not only folder for default video location
- Fix bug on multiple stop which prevent a correct play

## [1.2.0] - 2026-07-21

### ⭐ New Features

- Add a dedicated setting windows
- Move audio output selection to settings windows
- Move timecode display checkbox to settings windows
- Add BPM display checkbox to settings windows
- Add default vidéo folder to settings windows
- Allow to move in video based on mouse wheel 
- Add mouse wheel move step in settings

### 🐛 Bug Fixes

- Restore drag'n drop feature for quick vidéo playing

### 📝 Documentation

- Add CHANGELOG.md

## [1.1.0] - 2026-07-15

### ⭐ New Features

- Cubase tempo track management

### 🐛 Bug Fixes

- Fix very fast pause/play switch break BPM led
- Fix timecode interpolation for a smoother timecode display with frame
- Fix timecode size and label


### 🔨 Dependency Upgrades

- Update dependencies


## [1.0.0] - 2026-01-24

### 🎉 Initial Release

First public release of DM Video Player - A minimalist video player based on VLC dedicated to musicians and people with hearing impairments.

#### Key Features

- Classic video playback powered by LibVLC
- Dynamically add extra audio tracks (STEMS) to videos
- Multiple audio output routing capabilities
- Subtitle support with toggle functionality
- Timecode display with frame accuracy
- Keyboard shortcuts (0 to stop, space to play/pause)
- Mouse shortcuts (single click to play/pause, double click for fullscreen)


# Next version template

## Next version (only available as source, no binary released yet)

### ⭐ New Features

### 🐛 Bug Fixes

### 🧩 Misc

### 🔨 Dependency Upgrades