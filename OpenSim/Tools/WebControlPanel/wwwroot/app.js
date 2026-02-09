// OpenSim Web Control Panel JavaScript
class OpenSimControlPanel {
    constructor() {
        this.connection = null;
        this.currentLogSimulation = null;
        this.init();
    }

    async init() {
        await this.setupSignalR();
        await this.loadDashboard();
        this.setupEventHandlers();
        this.startTimeUpdate();
        
        // Refresh dashboard every 30 seconds
        setInterval(() => this.loadDashboard(), 30000);
    }

    async setupSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/simulationHub")
            .build();

        this.connection.on("SimulationStatusChanged", (name, status) => {
            this.showToast(`Simulation "${name}" status changed to ${status}`, 'info');
            this.loadDashboard();
        });

        this.connection.on("SimulationCreated", (name) => {
            this.showToast(`Simulation "${name}" created successfully`, 'success');
            this.loadDashboard();
        });

        try {
            await this.connection.start();
            console.log("SignalR Connected");
        } catch (err) {
            console.error("SignalR Connection Error: ", err);
        }
    }

    setupEventHandlers() {
        // Form submission for creating simulations
        document.getElementById('createSimForm').addEventListener('submit', (e) => {
            e.preventDefault();
            this.createSimulation();
        });
    }

    startTimeUpdate() {
        const updateTime = () => {
            const now = new Date();
            document.getElementById('current-time').textContent = now.toLocaleTimeString();
        };
        updateTime();
        setInterval(updateTime, 1000);
    }

    async loadDashboard() {
        try {
            const response = await fetch('/api/dashboard');
            const result = await response.json();
            
            if (result.success) {
                this.updateDashboardMetrics(result.data);
                this.updateSimulationsTable(result.data.simulations);
            } else {
                this.showToast('Failed to load dashboard data', 'error');
            }
        } catch (error) {
            console.error('Error loading dashboard:', error);
            this.showToast('Error loading dashboard data', 'error');
        }
    }

    updateDashboardMetrics(data) {
        const runningCount = data.simulations.filter(s => s.status === 'Running').length;
        
        document.getElementById('total-simulations').textContent = data.simulations.length;
        document.getElementById('running-simulations').textContent = runningCount;
        document.getElementById('active-users').textContent = data.performance.totalActiveUsers || 0;
        document.getElementById('cpu-usage').textContent = `${Math.round(data.systemInfo.cpuUsage || 0)}%`;
    }

    updateSimulationsTable(simulations) {
        const tbody = document.getElementById('simulations-table');
        tbody.innerHTML = '';

        if (simulations.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center">No simulations found</td></tr>';
            return;
        }

        simulations.forEach(sim => {
            const row = document.createElement('tr');
            const statusClass = this.getStatusClass(sim.status);
            const statusIcon = this.getStatusIcon(sim.status);
            const uptime = this.formatUptime(sim.uptime);
            
            row.innerHTML = `
                <td><strong>${sim.name}</strong></td>
                <td>
                    <span class="${statusClass}">
                        <i class="bi bi-${statusIcon}"></i>
                        ${sim.status}
                    </span>
                </td>
                <td>${uptime}</td>
                <td>${sim.configFile || 'N/A'}</td>
                <td>
                    <div class="btn-group btn-group-sm">
                        ${sim.status === 'Running' ? 
                            `<button class="btn btn-warning" onclick="controlPanel.stopSimulation('${sim.name}')">
                                <i class="bi bi-stop-fill"></i> Stop
                            </button>
                            <button class="btn btn-info" onclick="controlPanel.restartSimulation('${sim.name}')">
                                <i class="bi bi-arrow-clockwise"></i> Restart
                            </button>` :
                            `<button class="btn btn-success" onclick="controlPanel.startSimulation('${sim.name}')">
                                <i class="bi bi-play-fill"></i> Start
                            </button>`
                        }
                        <button class="btn btn-secondary" onclick="controlPanel.showLogs('${sim.name}')">
                            <i class="bi bi-file-text"></i> Logs
                        </button>
                    </div>
                </td>
            `;
            tbody.appendChild(row);
        });
    }

    getStatusClass(status) {
        switch (status) {
            case 'Running': return 'status-running';
            case 'Stopped': return 'status-stopped';
            case 'Starting': return 'status-starting';
            default: return '';
        }
    }

    getStatusIcon(status) {
        switch (status) {
            case 'Running': return 'play-circle-fill';
            case 'Stopped': return 'stop-circle-fill';
            case 'Starting': return 'hourglass-split';
            default: return 'question-circle';
        }
    }

    formatUptime(uptime) {
        if (!uptime || uptime === '00:00:00') return 'N/A';
        return uptime;
    }

    async startSimulation(name) {
        try {
            const response = await fetch(`/api/simulations/${name}/start`, { method: 'POST' });
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`Starting simulation "${name}"...`, 'success');
            } else {
                this.showToast(result.error || 'Failed to start simulation', 'error');
            }
        } catch (error) {
            this.showToast('Error starting simulation', 'error');
        }
    }

    async stopSimulation(name) {
        if (!confirm(`Are you sure you want to stop "${name}"?`)) return;
        
        try {
            const response = await fetch(`/api/simulations/${name}/stop`, { method: 'POST' });
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`Stopping simulation "${name}"...`, 'success');
            } else {
                this.showToast(result.error || 'Failed to stop simulation', 'error');
            }
        } catch (error) {
            this.showToast('Error stopping simulation', 'error');
        }
    }

    async restartSimulation(name) {
        if (!confirm(`Are you sure you want to restart "${name}"?`)) return;
        
        try {
            const response = await fetch(`/api/simulations/${name}/restart`, { method: 'POST' });
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`Restarting simulation "${name}"...`, 'success');
            } else {
                this.showToast(result.error || 'Failed to restart simulation', 'error');
            }
        } catch (error) {
            this.showToast('Error restarting simulation', 'error');
        }
    }

    async createSimulation() {
        const formData = {
            name: document.getElementById('simName').value,
            regionName: document.getElementById('regionName').value,
            httpPort: parseInt(document.getElementById('httpPort').value),
            internalPort: parseInt(document.getElementById('internalPort').value),
            externalHostName: document.getElementById('externalHost').value,
            maxAvatars: parseInt(document.getElementById('maxAvatars').value),
            maxObjects: parseInt(document.getElementById('maxObjects').value),
            locationX: parseInt(document.getElementById('locationX').value),
            locationY: parseInt(document.getElementById('locationY').value)
        };

        try {
            const response = await fetch('/api/simulations', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(formData)
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`Simulation "${formData.name}" created successfully`, 'success');
                bootstrap.Modal.getInstance(document.getElementById('createSimModal')).hide();
                document.getElementById('createSimForm').reset();
            } else {
                this.showToast(result.error || 'Failed to create simulation', 'error');
            }
        } catch (error) {
            this.showToast('Error creating simulation', 'error');
        }
    }

    async showLogs(simulationName) {
        this.currentLogSimulation = simulationName;
        const modal = new bootstrap.Modal(document.getElementById('logsModal'));
        modal.show();
        
        await this.loadLogs(simulationName);
    }

    async loadLogs(simulationName) {
        const logContent = document.getElementById('log-content');
        logContent.textContent = 'Loading logs...';
        
        try {
            const response = await fetch(`/api/simulations/${simulationName}/logs?lines=100`);
            const result = await response.json();
            
            if (result.success) {
                logContent.innerHTML = result.data.map(line => `<div>${this.escapeHtml(line)}</div>`).join('');
                logContent.scrollTop = logContent.scrollHeight;
            } else {
                logContent.textContent = result.error || 'Failed to load logs';
            }
        } catch (error) {
            logContent.textContent = 'Error loading logs';
        }
    }

    async refreshLogs() {
        if (this.currentLogSimulation) {
            await this.loadLogs(this.currentLogSimulation);
        }
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    showToast(message, type = 'info') {
        // Create toast container if it doesn't exist
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            container.style.zIndex = '1100';
            document.body.appendChild(container);
        }

        const toastId = 'toast-' + Date.now();
        const bgClass = type === 'error' ? 'bg-danger' : type === 'success' ? 'bg-success' : 'bg-info';
        
        const toastHtml = `
            <div id="${toastId}" class="toast ${bgClass} text-white" role="alert">
                <div class="toast-header ${bgClass} text-white border-0">
                    <strong class="me-auto">OpenSim Control Panel</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;
        
        container.insertAdjacentHTML('beforeend', toastHtml);
        
        const toast = new bootstrap.Toast(document.getElementById(toastId));
        toast.show();
        
        // Remove toast element after it's hidden
        document.getElementById(toastId).addEventListener('hidden.bs.toast', () => {
            document.getElementById(toastId).remove();
        });
    }

    async refreshDashboard() {
        await this.loadDashboard();
        this.showToast('Dashboard refreshed', 'success');
    }

    showSystemInfo() {
        // This could be expanded to show more detailed system information
        this.showToast('System info feature coming soon', 'info');
    }
}

// Global functions for button clicks
function refreshDashboard() {
    controlPanel.refreshDashboard();
}

function showSystemInfo() {
    controlPanel.showSystemInfo();
}

function createSimulation() {
    controlPanel.createSimulation();
}

function refreshLogs() {
    controlPanel.refreshLogs();
}

// Initialize the control panel when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.controlPanel = new OpenSimControlPanel();
});