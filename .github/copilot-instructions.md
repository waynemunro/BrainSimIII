# BrainSim III Copilot Instruc### State Persistence**: Network files (.xml) save/restore UKS and module configurations
  - XML format stores serialized Things as `SThing` with index references (breaks circular links)
  - Network files in `BrainSimulator/UKSContent/` contain complete system state
  - `MainWindow.currentFileName` tracks active network file

## Key Development Patternsns

## Project Overview
BrainSim III is a **knowledge system for Common Sense AI** built around the Universal Knowledge Store (UKS) - a graph database of interconnected "Things" (nodes) and "Relationships" (edges). The system supports modular software agents written in both C# and Python that operate on this shared knowledge graph.

## Core Architecture

### Universal Knowledge Store (UKS)
- **Central component**: `UKS/UKS.cs` - static shared knowledge graph accessible to all modules
- **Thing.cs**: Nodes with labels, values, and relationship collections. Support implicit string conversion: `Thing dog = "dog"`
- **Relationship.cs**: Edges connecting source→target Things with a relationship type (also a Thing)
- **Inheritance system**: Relationships on parent Things automatically apply to children (e.g., "dogs have 4 legs" → Fido has 4 legs)
- **Clauses**: Higher-order structures relating multiple Relationships for conditional logic
- **Transient relationships**: TTL support with automatic cleanup via timer

### Module System (Agents)
- **C# Modules**: Inherit from `ModuleBase` in `BrainSimulator/Modules/`
  - Override `Fire()` for per-cycle execution
  - Override `Initialize()` for setup
  - Access UKS via `theUKS` property
  - Auto-saved public properties (use `[XmlIgnore]` to exclude)
- **Python Modules**: Follow template in `PythonProj/module_template.py`
  - Must implement `Init()`, `Fire()`, `GetHWND()`, `SetLabel()`, `Close()`
  - Integrate via Python.NET runtime
  - Name with `m*.py` pattern for auto-discovery

### Execution Model
- **Tick-based**: Central `DispatcherTimer` calls `Fire()` on all active modules sequentially
- **WPF Integration**: Single-threaded UI updates via Dispatcher
- **Deterministic**: Fixed module ordering ensures reproducible behavior
- **State Persistence**: Network files (.xml) save/restore UKS and module configurations

## Key Development Patterns

### UKS Operations
```csharp
// Create/retrieve Things
Thing dog = theUKS.GetOrAddThing("dog", "Animal");
Thing fido = theUKS.CreateInstanceOf(dog);

// Add relationships
fido.AddParent("dog");  // Implicit string conversion
fido.AddRelationship(theUKS.Labeled("has-attribute"), "4-legs");

// Query with inheritance
var attributes = fido.GetAttributes(); // Includes inherited from "dog"
```

### Module Development
```csharp
public class ModuleYourName : ModuleBase
{
    public override void Fire()
    {
        Init(); // Always call first
        // Your per-cycle logic here
        UpdateDialog(); // If you have a UI dialog
    }
    
    public override void Initialize()
    {
        // One-time setup
    }
}
```

### Python Module Integration
- Modules auto-discovered from current directory (`m*.py` pattern)
- Python path configured via environment variable or startup dialog
- Use `ViewBase` class for tkinter UI integration
- Access UKS through inter-process communication
- Must implement: `Init()`, `Fire()`, `GetHWND()`, `SetLabel()`, `Close()`
- Template in `PythonProj/module_template.py` shows required structure

## Project Structure
- **BrainSimulator/**: Main WPF application and C# modules
- **UKS/**: Core knowledge store library
- **PythonProj/**: Python module templates and utilities
- **BrainSimMAC/**: Console application for macOS
- **docs/**: Documentation and architectural decisions

## Build & Dependencies
- **.NET 8 Windows** target with WPF (x64 platform)
- **Python.NET** for Python integration
- **Pluralize.NET** for linguistic processing  
- **Windows Forms** for file dialogs
- **MSTest** for unit testing framework
- Use **Visual Studio** or **dotnet CLI** to build
- Run tests via `dotnet test` or Test Explorer in VS

## Critical Workflows

### Adding New Modules
1. C#: Create class inheriting `ModuleBase` in `BrainSimulator/Modules/`
2. Python: Copy `module_template.py`, rename with `m` prefix
3. Both: Module auto-appears in UI for activation

### UKS Development
- Always use `GetOrAddThing()` for safe Thing creation
- Leverage inheritance - set attributes on parent classes
- Use `ThingLabels.GetThing()` for safe label-based retrieval
- Consider transient relationships for temporary state

### Testing
- MSTest framework in `BrainSimIII.Tests/` project
- Tests create isolated UKS instances: `new UKS.UKS(clear: true)`
- Test patterns focus on UKS operations and module behavior
- Run via `dotnet test` or Visual Studio Test Explorer

### Debugging
- UKS visualized via `ModuleShowGraph` 
- Module dialogs show real-time state
- Network saves preserve exact state for reproduction
- Check `MainWindow.activeModules` for module lifecycle

## Architecture Considerations
- Current **tick model** prioritizes determinism and simplicity
- Future **actor model** migration planned for scalability (see `docs/ActorModelComparison.md`)
- Single-threaded design with shared mutable UKS
- File I/O and networking handled in specific modules

## Common Gotchas
- Module `Fire()` methods called synchronously - avoid blocking operations
- UKS operations not thread-safe without explicit locking
- Python modules require proper COM initialization for UI
- Label case-insensitive but initial case preserved
- Circular references in UKS handled by save/restore serialization system