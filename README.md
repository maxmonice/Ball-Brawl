# ⚽ Ball Brawl

**Ball Brawl** is a 2D ball-based game developed using **C# and Windows Forms**. The project uses an **AI-assisted, agentic coding workflow** to build and iterate on gameplay mechanics, visual effects, sound effects, and game systems.

## 🎮 About the Game

Ball Brawl is a personal game development project built with C# and the .NET Windows Forms framework.

The game is developed through a combination of AI-assisted code generation, iterative prompting, and hands-on development. AI coding agents help implement and refine gameplay systems, while development focuses on turning ideas into functional mechanics and an engaging player experience.

## 🛠️ Built With

- C#
- .NET / Windows Forms
- Visual Studio Code
- AI Coding Agents

## ✨ Features

- Interactive 2D gameplay
- Ball movement and collision mechanics
- Character-specific abilities and combat systems
- Visual effects (VFX) and sound effects (SFX)
- Windows Forms graphical user interface
- Modular code structure for extensible gameplay systems

*Features are subject to change as development continues.*

## 🚀 Getting Started

### Prerequisites

- Windows
- .NET 10 SDK
- Visual Studio Code or Visual Studio

### Installation

1. Clone the repository:

   ```bash
   git clone <YOUR-REPOSITORY-URL>
   ```

2. Navigate to the project directory.

3. Restore the required dependencies:

   ```bash
   dotnet restore
   ```

4. Build and run the game:

   ```bash
   dotnet build BallBrawl.csproj
   dotnet run
   ```

5. Run the built-in gameplay self-test:

   ```bash
   dotnet run -- --selftest
   ```

   The self-test report appears in a Windows message box.

## Architecture

Gameplay state and presentation are separated. `GameEngine` coordinates the simulation, while `PhysicsSystem`, `CombatSystem`, `StatusEffectSystem`, and `UltimateSystem` own focused parts of the update. `GameEventManager` queues simulation events for the audio and visual-effects code to consume.

`BallCharacter` defines shared character state and lifecycle hooks. Each playable character has its own derived class, and `CharacterFactory` creates fresh instances. The eight charge-based characters share only their common charge-on-hit lifecycle through `ChargedBallCharacter`; each still supplies its passive and ultimate behavior. Roster definitions retain names, colors, health, size, and UI descriptions.

The WinForms UI keeps the existing menu, selection, match, and results flow. Rendering files read character and engine state without applying gameplay changes. The enum-based UI state remains a small, explicit state-management approach.

```text
Characters/  Character base class, factory, and eleven character implementations
Domain/      Roster definitions and shared balance values
Engine/      Simulation lifecycle and match physics orchestration
Events/      Gameplay event queue
Match/       Match creation and results snapshots
Systems/     Physics, combat, status effects, and ultimate/world updates
Rendering/   Pixel art, character art, visual effects, sword art, and impacts
Audio/       NAudio playback and gameplay sound mapping
UI/          Windows Forms game, menus, selection, and HUD
```

## 🤖 Development Workflow

Ball Brawl follows an **agentic vibe-coding workflow**, using AI coding agents to assist with:

- Generating and modifying game code
- Implementing gameplay mechanics and character abilities
- Creating and refining visual and audio effects
- Refactoring code and organizing project files
- Debugging issues and iterating on features through prompts

The development process emphasizes rapid prototyping, experimentation, and iterative improvement, with AI serving as a development collaborator.

## 📌 Project Status

**In Development**

Ball Brawl is an ongoing project. Gameplay mechanics, character abilities, effects, and other features may evolve as development progresses.

## 👨‍💻 Author

Developed as a personal game development project using C# and AI-assisted coding tools.

---

*Built with C#, AI-assisted development, and a passion for game creation.*
