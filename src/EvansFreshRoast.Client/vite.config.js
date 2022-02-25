import { defineConfig } from 'vite'

export default defineConfig({
    root: "fable",
    build: {
        outDir: "../../EvansFreshRoast.Api/wwwroot",
        emptyOutDir: true,
        sourcemap: true
    },
    server: {
        proxy: {
            "/api": {
                target: "https://localhost:5001",
                changeOrigin: true,
                cookieDomainRewrite: {
                    "localhost:5001": "localhost:3000"
                },
                secure: false,
                ws: true
            }
        }
    }
});