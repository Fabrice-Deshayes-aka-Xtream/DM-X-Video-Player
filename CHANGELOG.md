# Changelog

All notable changes to DMX Video Player will be documented in this file.

## WIP

### ⭐ New Features

- use 'P' instead of 'S' for Parameters window. 'P' can also close the window if opened.
- remove default video folder settings, save last one used instead.

### 🐛 Bug Fixes

## [1.3.0] - 2026-07-24

### ⭐ New Features

- allow to move timecode / BPM panel
- adapt displayed components with application width 
- set a minimum width and height for application
- improve progression bar UI
- refacteur windows/fullscreen management with controls overlay display (fix in windows mode, automatic hide in fullscreen)
- create a nice icon for application
- rename DM Video player to DMX Video Player (Didier Martini, Xtream Video Player)
- add about windows
- manage localisation with english & french

### 🐛 Bug Fixes

- allow to chose drive and not only folder for default video location
- fix bug on multiple stop which prevent a correct play

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