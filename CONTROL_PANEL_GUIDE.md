# OpenSim Control Panel User Guide

## Overview

The OpenSim Control Panel is a cross-platform console application that provides an intuitive interface for managing OpenSim simulation instances. It replaces complex command-line operations with a user-friendly menu-driven interface.

## Features

### Current Features (Phase 1)
- **Simulation Status Dashboard**: View all configured simulations and their current status
- **Start/Stop/Restart Operations**: Manage simulation lifecycle with simple menu selections
- **Detailed Status Views**: Get comprehensive information about individual simulations
- **Setup Wizard**: Guided configuration for new simulations (framework implemented)
- **Cross-Platform Support**: Runs on Windows, Linux, and macOS

### Upcoming Features (Phase 2)
- **Configuration Manager**: Edit and validate OpenSim configuration files
- **Performance Monitor**: Real-time performance metrics and alerts
- **Log Viewer**: Integrated log streaming and analysis
- **Multi-Instance Management**: Bulk operations for multiple simulations

## Installation

The Control Panel is included with OpenSim and automatically built when you compile the project.

### Prerequisites
- .NET 8.0 Runtime
- OpenSim installation

### Location
After building OpenSim, the Control Panel executable will be located in the `bin/` directory:
- **Linux/macOS**: `./OpenSim.Tools.ControlPanel`
- **Windows**: `OpenSim.Tools.ControlPanel.exe`

## Usage

### Starting the Control Panel

```bash
# From the OpenSim bin directory
cd /path/to/opensim/bin
./OpenSim.Tools.ControlPanel
```

### Main Interface

The Control Panel presents a colorful, easy-to-read interface with:

1. **ASCII Art Header**: Shows the application name prominently
2. **Status Table**: Displays all simulations with their current status
3. **Main Menu**: Lists available actions

### Menu Options

#### 1. View Detailed Status
- Select any simulation to see comprehensive details
- Shows process information, uptime, configuration path
- Useful for troubleshooting and monitoring

#### 2. Start Simulation
- Lists all stopped simulations
- Select one to start with its existing configuration
- Shows progress indicator during startup

#### 3. Stop Simulation
- Lists all running simulations
- Includes confirmation prompt to prevent accidents
- Attempts graceful shutdown first, then force-stops if needed

#### 4. Restart Simulation
- Lists all running simulations
- Performs stop followed by start operation
- Useful for applying configuration changes

#### 5. Create New Simulation
- Guided setup wizard for new simulations
- Collects essential configuration parameters
- Validates settings before saving
- Advanced options available for experienced users

#### 6. Configuration Manager *(Coming Soon)*
- Edit existing simulation configurations
- Validate configuration files
- Create configuration templates

#### 7. Performance Monitor *(Coming Soon)*
- Real-time performance dashboard
- CPU, memory, and physics metrics
- Alert system for performance issues

#### 8. View Logs *(Coming Soon)*
- Real-time log streaming
- Filter and search capabilities
- Error highlighting

#### 9. Settings
- Configure Control Panel preferences
- Set OpenSim installation path
- Adjust refresh intervals

### Status Indicators

The simulation status table uses color coding:
- **Green**: Running normally
- **Yellow**: Starting or stopping
- **Red**: Error state
- **Gray**: Stopped

## Configuration Discovery

The Control Panel automatically discovers simulations by scanning:
- `Regions/` directory for region configuration files
- `config-include/` directory for include files
- Root directory for standalone configurations

## Troubleshooting

### Common Issues

**Control Panel won't start**
- Ensure .NET 8.0 runtime is installed
- Check that you're running from the correct directory
- Verify OpenSim.Framework.dll is present

**No simulations found**
- Check that configuration files exist in expected locations
- Ensure .ini files are properly formatted
- Verify file permissions allow reading

**Cannot start simulation**
- Check that the OpenSim executable is present
- Verify no port conflicts exist
- Check simulation configuration for errors

**Performance issues**
- Reduce refresh frequency in settings
- Close other resource-intensive applications
- Check system resources (CPU, memory)

### Log Files

The Control Panel logs important events and errors. Check the console output for:
- Startup messages
- Error conditions
- Status change notifications

## Tips for Best Results

1. **Run as Administrator/Root**: Some operations may require elevated privileges
2. **Close other OpenSim instances**: Avoid port conflicts
3. **Monitor system resources**: Large simulations require adequate CPU and memory
4. **Regular backups**: Always backup configurations before making changes
5. **Test configurations**: Use the validation features before starting simulations

## Advanced Usage

### Command Line Options *(Future)*
The Control Panel will support command-line arguments for automation:
```bash
./OpenSim.Tools.ControlPanel --start MySimulation
./OpenSim.Tools.ControlPanel --status --json
```

### Integration with Scripts *(Future)*
The Control Panel will provide APIs for integration with deployment scripts and monitoring systems.

## Support and Feedback

For issues, suggestions, or contributions:
1. Check the troubleshooting section above
2. Review OpenSim documentation and forums
3. Submit issues via the project's bug tracking system
4. Join the OpenSim community for support

## Version History

### v1.0.0 (Current)
- Initial release with basic simulation management
- Cross-platform console interface
- Start/stop/restart operations
- Status monitoring and detailed views
- Setup wizard framework

### Planned Versions
- **v1.1.0**: Configuration management and validation
- **v1.2.0**: Performance monitoring and log viewing
- **v1.3.0**: Web interface and API endpoints
- **v2.0.0**: Multi-grid management and cloud deployment