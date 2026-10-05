// Güvenli Bordro - Windows masaüstü uygulaması (Electron)
// Yönetim panelini kendi penceresinde açar; WhatsApp linklerini WhatsApp uygulamasına,
// toplu gönderim isteklerini (guvenlibordro://) yanındaki gönderici programa iletir.
const { app, BrowserWindow, shell, Menu, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

const DEFAULT_URL = 'https://guvenlihipermarketbordo.com/yonetim-k7x4q9m2/';
const PROTOCOL = 'guvenlibordro';
const EXTERNAL_SCHEMES = ['whatsapp:', 'mailto:', 'tel:'];

let win = null;

function configPath() {
    return path.join(app.getPath('userData'), 'ayar.json');
}

function panelUrl() {
    try {
        const cfg = JSON.parse(fs.readFileSync(configPath(), 'utf8'));
        if (/^https?:\/\//.test(cfg.url || '')) return cfg.url;
    } catch (e) { /* ayar dosyası yoksa varsayılan adres */ }
    return DEFAULT_URL;
}

function sameSite(url) {
    try {
        const a = new URL(url), b = new URL(panelUrl());
        return a.hostname.replace(/^www\./, '') === b.hostname.replace(/^www\./, '');
    } catch (e) {
        return false;
    }
}

function runSender(url) {
    const helper = path.join(process.resourcesPath, 'helper', 'GuvenliBordroGonderici.exe');
    if (!fs.existsSync(helper)) {
        dialog.showErrorBox('Güvenli Bordro', 'Gönderici program bulunamadı. Uygulamayı yeniden kurun.');
        return;
    }
    spawn(helper, [url], {
        detached: true,
        stdio: 'ignore',
        env: Object.assign({}, process.env, { GB_PANEL_URL: panelUrl() }),
    }).unref();
}

/** Panel dışına giden linkleri yönlendirir. true dönerse link uygulama içinde açılmaz. */
function handleExternal(url) {
    if (url.toLowerCase().startsWith(PROTOCOL + ':')) {
        runSender(url);
        return true;
    }
    if (EXTERNAL_SCHEMES.some(s => url.toLowerCase().startsWith(s))) {
        shell.openExternal(url);
        return true;
    }
    if (/^https?:/i.test(url) && !sameSite(url)) {
        shell.openExternal(url);
        return true;
    }
    return false;
}

function protocolArg(argv) {
    return (argv || []).find(a => typeof a === 'string' && a.toLowerCase().startsWith(PROTOCOL + ':'));
}

function createWindow() {
    win = new BrowserWindow({
        width: 1366,
        height: 860,
        minWidth: 900,
        minHeight: 600,
        title: 'Güvenli Bordro',
        icon: path.join(__dirname, 'build', 'icon.ico'),
        autoHideMenuBar: true,
        backgroundColor: '#f1f5f4',
        webPreferences: {
            contextIsolation: true,
            sandbox: true,
            nodeIntegration: false,
            plugins: true, // PDF görüntüleyici
        },
    });

    win.webContents.setWindowOpenHandler(({ url }) => {
        if (handleExternal(url)) return { action: 'deny' };
        // Panelden açılan PDF vb. için ayrı pencere
        return {
            action: 'allow',
            overrideBrowserWindowOptions: { autoHideMenuBar: true, icon: path.join(__dirname, 'build', 'icon.ico') },
        };
    });
    win.webContents.on('will-navigate', (e, url) => {
        if (handleExternal(url)) e.preventDefault();
    });
    win.webContents.on('did-fail-load', (e, code, desc, url, isMainFrame) => {
        if (!isMainFrame || code === -3) return; // -3: kullanıcı iptali / yönlendirme
        win.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(
            '<body style="font-family:Segoe UI,sans-serif;text-align:center;padding-top:80px;background:#f1f5f4">' +
            '<h2>Panele bağlanılamadı</h2><p>İnternet bağlantınızı kontrol edin.</p><p style="color:#64748b">' + desc + '</p>' +
            '<p><a href="' + panelUrl() + '">Tekrar dene</a></p></body>'));
    });

    win.loadURL(panelUrl());
}

function buildMenu() {
    const template = [
        {
            label: 'Uygulama',
            submenu: [
                { label: 'Yenile', accelerator: 'F5', click: () => win && win.webContents.reload() },
                { label: 'Geri', accelerator: 'Alt+Left', click: () => win && win.webContents.canGoBack() && win.webContents.goBack() },
                { label: 'Panel ana sayfa', click: () => win && win.loadURL(panelUrl()) },
                { type: 'separator' },
                {
                    label: 'Panel adresini değiştir…',
                    click: () => {
                        if (!fs.existsSync(configPath())) {
                            fs.writeFileSync(configPath(), JSON.stringify({ url: panelUrl() }, null, 2));
                        }
                        dialog.showMessageBox(win, {
                            type: 'info',
                            title: 'Güvenli Bordro',
                            message: 'Açılan dosyada "url" değerini yeni adresle değiştirip kaydedin, ardından uygulamayı yeniden başlatın.',
                        });
                        shell.openPath(configPath());
                    },
                },
                { type: 'separator' },
                { label: 'Yakınlaştır', accelerator: 'CmdOrCtrl+Plus', role: 'zoomIn' },
                { label: 'Uzaklaştır', accelerator: 'CmdOrCtrl+-', role: 'zoomOut' },
                { label: 'Normal boyut', accelerator: 'CmdOrCtrl+0', role: 'resetZoom' },
                { type: 'separator' },
                { label: 'Çıkış', role: 'quit' },
            ],
        },
    ];
    Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

if (!app.requestSingleInstanceLock()) {
    app.quit();
} else {
    app.on('second-instance', (e, argv) => {
        const p = protocolArg(argv);
        if (p) runSender(p);
        if (win) {
            if (win.isMinimized()) win.restore();
            win.focus();
        }
    });

    app.whenReady().then(() => {
        // Panelin "otomatik gönder" butonu tarayıcı dışında da bu uygulamayı bulabilsin
        app.setAsDefaultProtocolClient(PROTOCOL);
        buildMenu();
        createWindow();
        const p = protocolArg(process.argv);
        if (p) runSender(p);
    });

    app.on('window-all-closed', () => app.quit());
}
