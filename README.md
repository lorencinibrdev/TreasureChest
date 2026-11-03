> [!NOTE]
> This README was initially generated with the assistance of AI and serves as a temporary overview. It will be refined and updated as the project evolves.


# TreasureChest 🏴‍☠️

**TreasureChest** is a native, modern, and lightweight GOG library manager and download tool designed specifically for the GNOME desktop environment. Written in C# and powered by GTK 4 and Libadwaita, it offers a seamless experience for browsing, managing, and downloading your DRM-free GOG games without the overhead of web wrappers or heavy launch clients.

---

## 🌟 Why TreasureChest?

Managing a DRM-free gaming library on Linux often involves navigating web browsers, handling loose shell installers, or running resource-heavy launcher wrappers. TreasureChest was created to provide a native, high-performance Linux client that respects system resources and integrates into the modern GNOME desktop.

Key design principles behind the project:

- **Native Desktop Integration:** Styled with GTK 4 and Libadwaita to align naturally with current GNOME HIG standards.
- **High-Performance Architecture:** Employs low-allocation C# patterns (such as `readonly record struct` data-bags and decoupled domain factory models) to minimize GC pressure and memory usage.
- **Robust Caching & Offline Capabilities:** Separates API DTOs from internal domain models. Raw GOG API responses are safely cached to disk, allowing instant startup times and offline library access.
- **Thread-Safe Service Infrastructure:** Built around robust singleton services (`LibraryService`, `GogServices`) featuring asynchronous disk locking and safe session handling.
- **Smart Data Handling:** Programmatically reorganizes complex GOG metadata—such as flattening non-playable "Soundtrack DLCs" into the base game's Extras list—to present a clean, intuitive UI.

---

## ✨ Features

- 🔐 **Secure Authentication:** GOG OAuth authorization flow with silent session restoration, storing refresh tokens securely in the system keyring (`LibSecret`).
- 📚 **Library Cache Management:** Local JSON storage with user-specific caching to prevent duplicate API fetches and support offline browsing.
- 🎮 **Comprehensive Game Details View:** Cleanly categorizes installers by platform (Linux, Windows, macOS) and language, alongside expansions and bonus extras.
- 🎨 **Media & Extras Organization:** Easy access to soundtracks (MP3/FLAC), artbooks, manuals, and official game add-ons.
- ⚡ **Low Overhead:** Built on native bindings for minimal RAM footprint and snappy user interactions.

---

## 🛠️ Prerequisites

To build and run TreasureChest from source, ensure your environment has the following installed:

- **.NET SDK** (version 8.0 or newer)
- **GTK 4** development libraries
- **Libadwaita** development libraries
- **libsecret** headers (for desktop keyring access)

### Package Dependencies (Example for Fedora/Arch/Debian)

- **Arch Linux:** `sudo pacman -S dotnet-sdk gtk4 libadwaita libsecret`
- **Fedora:** `sudo dnf install dotnet-sdk-8.0 gtk4-devel libadwaita-devel libsecret-devel`
- **Ubuntu / Debian:** `sudo apt install dotnet-sdk-8.0 libgtk-4-dev libadwaita-1-dev libsecret-1-dev`

---

## 🚀 Build Instructions

1. **Clone the repository:**
   ```bash
   git clone https://github.com/lorencinibrdev/TreasureChest.git
   cd TreasureChest
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the application:**
   ```bash
   dotnet build -c Release
   ```

4. **Run the application:**
   ```bash
   dotnet run --project TreasureChest.Source/TreasureChest.Source.csproj
   ```

---

## 📦 Installation

> **Status Notice:** TreasureChest is currently under active development. Native distribution packages (e.g., AUR, `.deb`, `.rpm`) and universal **Flatpak** builds are not yet available, but packaging for Flathub is a top priority on our roadmap.
>
> For now, please build and run the application directly from source following the build instructions above.

---

## 🗺️ Roadmap

- [x] GOG OAuth authentication flow & keyring integration (`LibSecret`)
- [x] Asynchronous, thread-safe library caching & domain model mapping
- [x] GTK 4 / Libadwaita Game Details view (Installers, DLCs, Extras)
- [ ] Automated game installation service integrated with `innoextract`
- [ ] Download manager with pause/resume and bandwidth limits
- [ ] Flatpak packaging for Flathub distribution

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.