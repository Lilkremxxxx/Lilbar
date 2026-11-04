const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('lilbar', {
  stats: () => ipcRenderer.invoke('stats'),
  apps: () => ipcRenderer.invoke('apps'),
  windows: () => ipcRenderer.invoke('windows'),
  launch: (p) => ipcRenderer.send('launch', p),
  activate: (h) => ipcRenderer.send('activate', h),
  resize: (w) => ipcRenderer.send('resize', w),
});
