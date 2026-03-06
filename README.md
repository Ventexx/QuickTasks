# QuickTasks

Minimalist Windows task manager. Global shortcut and simple-to-use functionality.

![Cover Image](ScreenshotCover.png)

## Table of Contents

* [Overview](#overview)
* [Features](#features)
* [Getting Started](#getting-started)

  * [Installation](#installation)
  * [Uninstall](#uninstall)
* [Usage](#usage)
* [Contribution Guidelines](#contribution-guidelines)
* [Contact](#contact)
* [License](#license)

---

# Overview

QuickTasks is a minimal Windows task manager built with .NET 8 WPF that lets you manage your to-do list from a lightweight, always-accessible overlay.

The application is designed to stay out of your way — hiding when not in use and summoned instantly via a global hotkey.
It runs entirely locally with no external services or dependencies beyond the .NET runtime, and stores your tasks as simple JSON files in your AppData folder.

---

# Features

* **Global Hotkey Access** — Open and close QuickTasks from anywhere on your system with a fully customizable hotkey *(default: Ctrl+Alt+,)*.
* **Task Groups** — Organize tasks into collapsible groups. Drag tasks onto group headers to assign them, double-click a group to rename it inline, and star groups to pin them to the top.
* **Star & Prioritize** — Star individual tasks to highlight them with a ❗ marker and sort them above the rest within their group.
* **Mark as Done with Undo** — Click a task's checkbox to complete it. A 5-second undo window appears before the task is permanently moved to your finished log.
* **Open & Done Counters** — The status bar shows a live count of open and completed tasks. Click either counter to open the corresponding file directly in your text editor.
* **Multiple Color Themes** — Choose from 9 built-in themes including Dark, Light, Dracula, Gruvbox, and all four Catppuccin variants.
* **Persistent Window Position** — QuickTasks remembers where you left it on screen between sessions.

---

# Getting Started

## Installation

### Option 1: Use the Prebuilt Release

1. **Download the prebuilt package** from the [Releases](../../releases) page.

- Choose the StableBuild `.zip` file for the **latest stable package**.
- Extract the contents of the `.zip` to any folder of your choice.
- Follow the instructions in the `README.md` included within the package for any additional setup notes.

2. **After running the `.exe` file** once, configuration files will initialize under:

```
%AppData%\QuickTasks\
```


---

### Option 2: Build from Source

If you prefer building QuickTasks yourself:

1. **Clone the repository**

```
git clone https://github.com/Ventexx/QuickTasks.git
cd QuickTasks
```

2. **Build the project**

```
dotnet build -c Release
```

3. **Run the application**

```
dotnet run
```

4. **Locate the compiled executable**

The compiled files will be located in:

```
QuickTasks/bin/Release/net8.0-windows/
```

Run the application using:

```
QuickTasks.exe
```

The executable depends on the files inside this directory and should not be moved separately.

---

## Uninstall

To remove QuickTasks:

* Delete the application folder.

If configuration or user data is created in the future, it may be stored under:

```
C:\Users\<USERNAME>\AppData\Roaming\QuickTasks\
```

---

# Usage

Press the global hotkey to open QuickTasks. (Default: Ctrl+Alt+,)

Click the + button or press Ctrl+N to add a new task.

Click a task's circle checkbox to mark it as done.

Double-click any task to edit it inline.

Right-click a task for Rename, Set group, Star, or Delete.

Right-click a group header for Star group or Rename group.

You can change the global hotkey and switch between color themes in the Settings menu.

---

# Contribution Guidelines

Contributions are welcome.

Please follow the **Conventional Commits** specification when submitting changes:

https://www.conventionalcommits.org/

---

# Contact

**Maintainer:** Ventexx
Email: [enquiry.kimventex@outlook.com](mailto:enquiry.kimventex@outlook.com)

---

# License

QuickTasks © 2026 by Ventexx is licensed under **CC BY-NC 4.0**.

To view a copy of this license, visit:
https://creativecommons.org/licenses/by-nc/4.0/
