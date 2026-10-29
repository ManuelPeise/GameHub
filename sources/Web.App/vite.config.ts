import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';

// Address of Services.Api for the dev proxy. When started by Aspire (Services.AppHost), the address
// is injected through environment variables of the referenced resource "api". Without Aspire, the
// https profile from Services.Api/Properties/launchSettings.json is used.
const apiTarget =
    process.env.API_HTTPS ??
    process.env.services__api__https__0 ??
    process.env.API_HTTP ??
    process.env.services__api__http__0 ??
    'https://localhost:55408';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    server: {
        // Aspire assigns the port through PORT; standalone the fixed port is used.
        port: Number(process.env.PORT ?? 57764),
        // Same-origin calls in development: cookies and SignalR work without CORS.
        proxy: {
            '/api': { target: apiTarget, changeOrigin: true, secure: false },
            '/hubs': { target: apiTarget, changeOrigin: true, secure: false, ws: true },
        },
    },
});
