# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## System Architecture

This is a dual-language project that bridges Grasshopper (Rhino 3D's visual programming interface) with Claude Desktop via the Model Context Protocol (MCP). The system consists of three main components:

1. **Python MCP Bridge Server** (`grasshopper_mcp/bridge.py`): The main bridge that translates MCP requests to TCP commands for Grasshopper
2. **C# Grasshopper Component** (`GH_MCP/`): A .gha plugin that runs inside Grasshopper and provides a TCP server on port 8080 
3. **Component Knowledge Base** (`GH_MCP/GH_MCP/Resources/ComponentKnowledgeBase.json`): Contains metadata about Grasshopper components, their parameters, and connection rules

## Development Commands

### Python Bridge Server
```bash
# Install in development mode
pip install -e .

# Run the bridge server directly
python -m grasshopper_mcp.bridge

# Install from source
pip install git+https://github.com/msaringer-dialog/grasshopper-mcp.git
```

### C# Grasshopper Component
```bash
# Build the .gha component (requires Visual Studio or dotnet CLI)
dotnet build GH_MCP/GH_MCP/GH_MCP.csproj

# Build for multiple target frameworks
dotnet build --configuration Release
```

The C# project targets multiple frameworks:
- `net7.0-windows` and `net7.0` for Rhino 8
- `net48` for Rhino 7
- Output is a `.gha` file that goes in `%APPDATA%\Grasshopper\Libraries\`

## Communication Flow

1. Claude Desktop sends MCP requests → Python bridge server (localhost:8080)
2. Bridge translates to JSON commands → C# TCP server in Grasshopper
3. C# component executes Grasshopper operations via command handlers
4. Results flow back: Grasshopper → C# → Python → Claude Desktop

## Key Code Architecture

### Command Pattern (C#)
- `GrasshopperCommandRegistry.cs`: Central registry that maps command strings to handler functions
- Command handlers in `Commands/` folder:
  - `ComponentCommandHandler.cs`: Add/connect/query Grasshopper components
  - `ConnectionCommandHandler.cs`: Handle component connections with smart parameter routing
  - `DocumentCommandHandler.cs`: Document-level operations (save/load/clear)
  - `GeometryCommandHandler.cs`: Direct geometry creation
  - `IntentCommandHandler.cs`: High-level pattern recognition

### Bridge Intelligence (Python)
- Smart component name normalization (e.g., "slider" → "Number Slider") 
- Automatic parameter assignment for multi-input components (Addition, Subtraction)
- Component library integration with usage hints and compatibility rules
- Connection validation and guidance

### Threading Model
- C# operations must run on Grasshopper's UI thread via `RhinoApp.InvokeOnUiThread()`
- Python bridge uses async/await for TCP communication
- All component modifications trigger `doc.NewSolution(false)` to refresh the canvas

## Component Knowledge System

The system includes intelligent component handling:
- Fuzzy matching for component names (`FuzzyMatcher.cs`)
- Parameter type compatibility checking
- Automatic connection routing (e.g., first input goes to "A", second to "B" for math components)
- Component metadata in `ComponentKnowledgeBase.json` with usage examples and common issues

## Development Notes

- Python code has been translated from Chinese to English comments for accessibility
- C# code contains some Chinese comments that may need translation
- The bridge server provides comprehensive component information via MCP resources
- Error handling includes detailed logging for debugging connection issues
- All UI operations in C# must be marshaled to the main thread